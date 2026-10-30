using UnityEngine;

/// <summary>
/// The AI's weapon fallback (batch 13 step 15, D8; AI-073, ITM-069): a creature that holds no weapon it can fight with
/// (empty hands after a throw or a disarm, or a projectile weapon with no ammunition) draws one from its pack through the
/// catalog's draw entry, at the draw's RAW cost (a move action that does not provoke; free with Quick Draw, or with a
/// regular move at base attack bonus +1; PHB p.142, p.98). A useless weapon in the way (a bow with an empty quiver) is
/// dropped first, a free action (PHB p.141). AIProfile.TryEnsureWeaponFallback calls it at the AI turn's start and in the
/// NPC attack prelude (GameManager.NPCPerformAttack), so the attack guard judges the drawn weapon.
///
/// AI choice, not a rule (an interim rule, replaced by batch 22's evaluator): a melee-style creature draws a melee weapon
/// first, a Ranged-style one a ranged weapon it can use first; a creature with natural attacks fights with them instead
/// (unless the caller asks, as mindless undead do), an empty-handed creature with Improved Unarmed Strike (a monk) fights
/// unarmed, and a casting profile does not spend its move action this way. It never trades its standard action for the
/// draw's move action (PHB p.141 allows it, but the creature would lose its attack): with its move action gone and no free
/// draw (Quick Draw, the free draw of a regular move at base attack bonus +1, the paired Two-Weapon Fighting draw) it
/// fights with what it has and draws next turn. It draws before it moves at the turn's start, so it does not wait for the
/// free draw of its move (AI-081).
/// </summary>
public static class AiWeaponFallback
{
    /// <summary>
    /// Draws a weapon for <paramref name="self"/> when it holds none it can fight with (see the class summary). True when
    /// a weapon was drawn.
    /// </summary>
    public static bool TryDraw(CharacterController self, bool preferMelee, bool evenWithNaturalAttacks = false)
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || self == null || self.Stats == null || self.IsDead || self.IsGrappling())
            return false;
        if (!evenWithNaturalAttacks && self.Stats.HasNaturalAttacks)
            return false;
        // An unarmed fighter (a monk, or any creature with Improved Unarmed Strike) fights with its empty hands: its
        // unarmed strike is a weapon it does not need to draw (PHB p.40, p.96).
        if (self.GetEquippedMainWeapon() == null && FeatManager.HasImprovedUnarmedStrike(self.Stats))
            return false;
        if (HoldsUsableWeapon(self))
            return false;
        Inventory inv = EquipmentActionRules.InventoryOf(self);
        if (inv == null || inv.GeneralSlots == null)
            return false;
        int pick = PickWeapon(self, inv, preferMelee);
        if (pick < 0)
            return false;
        ItemData weapon = inv.GeneralSlots[pick];

        // Nothing is dropped unless the draw can follow: no condition bars handling items, and the draw is free or the move
        // action is left (the standard action is never traded for it, see the class summary).
        if (GameManager.ItemManipulationConditionRefusal(self) != null)
            return false;
        DrawPayment payment = EquipmentActionRules.DrawPaymentFor(self, weapon);
        if (payment == DrawPayment.MoveAction && (self.Actions == null || !self.Actions.HasMoveAction))
        {
            Debug.Log($"[AI][Weapon] {self.Stats.CharacterName} does not draw {weapon.Name}: no move action left and no free draw (it keeps its standard action)");
            return false;
        }

        // A useless weapon that takes the hand the drawn one needs is dropped first (free, PHB p.141).
        if (EquipmentActionRules.HandForDraw(self, weapon, EquipSlot.None, out _) == EquipSlot.None)
        {
            ItemData held = self.GetEquippedMainWeapon();
            if (held == null || HoldsUsableWeapon(self))
                return false;
            var drop = ActionContext.For(ActionIds.DropItem, self, ActorSource.AI);
            drop.Item = held;
            if (!gm.RunAIActionNow(drop).Performed)
                return false;
            if (EquipmentActionRules.HandForDraw(self, weapon, EquipSlot.None, out _) == EquipSlot.None)
                return false;
            pick = System.Array.IndexOf(inv.GeneralSlots, weapon);
            if (pick < 0)
                return false;
        }

        var draw = ActionContext.For(ActionIds.DrawWeapon, self, ActorSource.AI);
        draw.Item = weapon;
        draw.InventoryIndex = pick;
        ActionOutcome o = gm.RunAIActionNow(draw);
        if (!o.Performed)
            Debug.Log($"[AI][Weapon] {self.Stats.CharacterName} does not draw {weapon.Name}: {o.Refusal.Reason}");
        return o.Performed;
    }

    /// <summary>
    /// True when <paramref name="self"/> holds a weapon it can attack with: any weapon in hand but a projectile weapon that
    /// cannot shoot this turn (no ammunition; an unloaded crossbow that can be reloaded passes, AiAttackGuard.CheckWeapon).
    /// </summary>
    public static bool HoldsUsableWeapon(CharacterController self)
    {
        ItemData weapon = self != null ? self.GetEquippedMainWeapon() : null;
        if (weapon == null)
            return false;
        if (!weapon.IsProjectileWeapon)
            return true;
        return AiAttackGuard.CheckWeapon(self, AttackIntent.ForMode(AttackMode.Ranged, weapon), anyReload: true).Ok;
    }

    /// <summary>
    /// True when <paramref name="self"/>'s pack holds a weapon it could draw and fight with: a melee weapon, a thrown
    /// weapon, or a projectile weapon with ammunition (an empty bow or sling does not count; AI-074's guard,
    /// AttackIntent.KeepsAWeaponAfterThrowing).
    /// </summary>
    public static bool HasUsablePackWeapon(CharacterController self)
    {
        Inventory inv = EquipmentActionRules.InventoryOf(self);
        return inv != null && inv.GeneralSlots != null && PickWeapon(self, inv, preferMelee: true) >= 0;
    }

    /// <summary>
    /// The pack weapon to draw: a melee weapon first when <paramref name="preferMelee"/>, else a ranged one it can use (a
    /// thrown weapon, or a projectile weapon with ammunition); then the other kind. -1 when the pack has none it can use.
    /// </summary>
    private static int PickWeapon(CharacterController self, Inventory inv, bool preferMelee)
    {
        int melee = -1, ranged = -1;
        for (int i = 0; i < inv.GeneralSlots.Length; i++)
        {
            ItemData item = inv.GeneralSlots[i];
            if (item == null || !item.IsWeapon)
                continue;
            if (item.WeaponCat == WeaponCategory.Ranged)
            {
                bool usable = !item.IsProjectileWeapon
                    || AiAttackGuard.CheckWeapon(self, AttackIntent.ForMode(AttackMode.Ranged, item), anyReload: true).Ok;
                if (usable && ranged < 0)
                    ranged = i;
            }
            else if (melee < 0)
            {
                melee = i;
            }
        }
        if (preferMelee)
            return melee >= 0 ? melee : ranged;
        return ranged >= 0 ? ranged : melee;
    }
}
