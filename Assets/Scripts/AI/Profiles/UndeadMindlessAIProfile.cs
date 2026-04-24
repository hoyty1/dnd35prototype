using System;
using UnityEngine;

namespace DND35.AI.Profiles
{
    /// <summary>
    /// Mindless undead behavior (skeletons, zombies, etc.):
    /// - Always pressure the nearest enemy
    /// - No tactical maneuvers
    /// - Ignores attacks of opportunity
    /// - Uses simple weapon fallback (equipped -> inventory weapon -> natural -> unarmed)
    /// </summary>
    [CreateAssetMenu(fileName = "Undead Mindless AI", menuName = "DND35/AI/Profiles/Undead Mindless")]
    public class UndeadMindlessAIProfile : AIProfile
    {
        private void OnEnable()
        {
            if (string.IsNullOrWhiteSpace(ProfileName))
                ProfileName = "Undead Mindless";

            if (string.IsNullOrWhiteSpace(Description))
                Description = "Mindless undead that shamble toward the nearest living target and attack without tactics.";

            CombatStyle = CombatStyle.Melee;
            Aggression = 1f;
            PrioritizeWounded = false;
            PrioritizeIsolated = false;
            SwitchTargetsOften = true; // re-evaluate nearest each turn

            if (Movement == null)
                Movement = new MovementPreferences();

            Movement.AvoidAoOs = false;
            Movement.PreferredRangeSquares = 0;
            Movement.MaintainDistance = false;
            Movement.SeekFlanking = false;
            Movement.UseCover = false;

            GrappleBehavior = GrappleBehavior.Avoid;

            if (Maneuvers == null)
                Maneuvers = new ManeuverPreferences();

            Maneuvers.AttemptTrip = false;
            Maneuvers.AttemptDisarm = false;
            Maneuvers.AttemptSunder = false;
            Maneuvers.AttemptBullRush = false;
            Maneuvers.AttemptOverrun = false;
            Maneuvers.UsePowerAttack = false;
        }

        public override float ScoreTarget(CharacterController target, CharacterController self)
        {
            if (target == null || target.Stats == null || target.Stats.IsDead || self == null)
                return float.MinValue;

            int distance = SquareGridUtils.GetDistance(self.GridPosition, target.GridPosition);

            // Keep this dominant over any perception/other additive modifiers so nearest always wins.
            return 100000f - (distance * 1000f);
        }

        public override bool ShouldIgnoreAoO(CharacterController self)
        {
            return true;
        }

        public override bool ShouldUseCoupDeGrace(CharacterController self)
        {
            return false;
        }

        public override bool ShouldInitiateGrapple(CharacterController self, CharacterController target)
        {
            return false;
        }

        public override SpecialAttackType? GetPreferredManeuver(CharacterController self, CharacterController target)
        {
            return null;
        }

        public override bool ShouldSwitchTargetsMidFullAttack(CharacterController self)
        {
            return false;
        }

        public override bool ShouldTakeFiveFootStepToContinueFullAttack(CharacterController self)
        {
            return false;
        }

        public override bool TryEnsureWeaponFallback(CharacterController self)
        {
            if (self == null || self.Stats == null)
                return false;

            if (self.GetEquippedMainWeapon() != null)
                return false;

            InventoryComponent inventoryComponent = self.GetComponent<InventoryComponent>();
            Inventory inventory = inventoryComponent != null ? inventoryComponent.CharacterInventory : null;
            if (inventory == null || inventory.GeneralSlots == null)
                return false;

            int weaponIndex = FindBestFallbackWeaponIndex(inventory);
            if (weaponIndex < 0)
            {
                // No manufactured weapon left: combat naturally falls back to natural attacks (if any), then unarmed.
                return false;
            }

            ItemData weapon = inventory.GeneralSlots[weaponIndex];
            if (weapon == null)
                return false;

            EquipSlot slot = ResolveFallbackEquipSlot(weapon);
            if (slot == EquipSlot.None)
                return false;

            bool equipped = inventory.EquipFromInventory(weaponIndex, slot);
            if (equipped)
                Debug.Log($"[AI][UndeadMindless] {self.Stats.CharacterName} re-equips {weapon.Name} in {slot}.");

            return equipped;
        }

        private static int FindBestFallbackWeaponIndex(Inventory inventory)
        {
            int firstWeapon = -1;

            for (int i = 0; i < inventory.GeneralSlots.Length; i++)
            {
                ItemData candidate = inventory.GeneralSlots[i];
                if (candidate == null || !candidate.IsWeapon)
                    continue;

                if (firstWeapon < 0)
                    firstWeapon = i;

                // Prefer melee for shambling undead pressure.
                if (candidate.WeaponCat == WeaponCategory.Melee)
                    return i;
            }

            return firstWeapon;
        }

        private static EquipSlot ResolveFallbackEquipSlot(ItemData weapon)
        {
            if (weapon == null)
                return EquipSlot.None;

            if (weapon.CanEquipIn(EquipSlot.RightHand))
                return EquipSlot.RightHand;

            if (weapon.CanEquipIn(EquipSlot.LeftHand))
                return EquipSlot.LeftHand;

            if (weapon.CanEquipIn(EquipSlot.Hands))
                return EquipSlot.Hands;

            return EquipSlot.None;
        }
    }
}
