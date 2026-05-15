using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DND35e.Identifiers;

/// <summary>
/// Draws compact status effect icons above battlefield tokens.
/// Icons update in real-time based on active combat conditions.
/// </summary>
public class StatusEffectIndicator : MonoBehaviour
{
    [Header("Icon Layout")]
    public float iconSize = 0.28f;
    public float iconSpacing = 0.04f;
    public Vector3 iconOffset = new Vector3(0f, 0.72f, 0f);
    public int iconSortingOrder = 50;

    private CharacterController _character;
    private Transform _iconContainer;
    private readonly List<GameObject> _activeIcons = new List<GameObject>();
    private string _lastSignature = string.Empty;

    private static Sprite _badgeSprite;

    private struct IconData
    {
        public string Key;
        public string ShortLabel;
        public string Tooltip;
        public Color Color;
        public int Duration;
    }

    private void Start()
    {
        _character = GetComponent<CharacterController>();
        if (_character == null)
        {
            enabled = false;
            return;
        }

        if (_badgeSprite == null)
            _badgeSprite = CreateBadgeSprite();

        if (_iconContainer == null)
        {
            GameObject container = new GameObject("StatusIconContainer");
            _iconContainer = container.transform;
            _iconContainer.SetParent(transform, false);
            _iconContainer.localPosition = iconOffset;
        }

        StatusEffectTooltipUI.EnsureInstance();
    }

    private void LateUpdate()
    {
        if (_character == null || _character.Stats == null)
            return;

        _iconContainer.localPosition = iconOffset;

        List<IconData> iconData = BuildIconData();
        string signature = BuildSignature(iconData);

        if (signature != _lastSignature)
        {
            RebuildIcons(iconData);
            _lastSignature = signature;
        }

        UpdateTooltip(iconData);
    }

    private void OnDisable()
    {
        if (StatusEffectTooltipUI.Instance != null)
            StatusEffectTooltipUI.Instance.HideTooltip();
    }

    private List<IconData> BuildIconData()
    {
        var list = new List<IconData>();

        if (_character.Stats.ActiveConditions != null)
        {
            foreach (var condition in _character.Stats.ActiveConditions)
            {
                CombatConditionType normalized = ConditionRules.Normalize(condition.Type);
                list.Add(new IconData
                {
                    Key = normalized.ToString(),
                    ShortLabel = GetConditionLabel(normalized),
                    Tooltip = BuildConditionTooltip(condition),
                    Color = GetConditionColor(normalized),
                    Duration = condition.RemainingRounds
                });
            }
        }

        StatusEffectManager statusMgr = _character.GetComponent<StatusEffectManager>();
        if (statusMgr != null && statusMgr.HasEffect(SpellNames.EXPEDITIOUS_RETREAT))
        {
            int rounds = statusMgr.GetRemainingRounds(SpellNames.EXPEDITIOUS_RETREAT);
            int speedBonus = _character.ActiveExpeditiousRetreatEffect != null
                ? Mathf.Max(0, _character.ActiveExpeditiousRetreatEffect.SpeedBonusFeet)
                : 30;

            list.Add(new IconData
            {
                Key = "ExpeditiousRetreat",
                ShortLabel = "ER",
                Tooltip = $"Expeditious Retreat\n+{speedBonus} ft enhancement to land speed\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.28f, 0.72f, 1f, 0.9f),
                Duration = rounds
            });
        }

        // Display invisibility status from any source (spell, item, ability)
        if (_character != null && _character.HasActiveInvisibilityEffect)
        {
            var invisData = _character.ActiveInvisibilityEffect;
            int rounds = invisData.DurationRemainingRounds;
            int hideBonus = _character.GetInvisibilityHideBonus();
            bool moving = invisData.IsMoving;
            string sourceName = !string.IsNullOrEmpty(invisData.SourceName) ? invisData.SourceName : "Invisibility";
            string breakNote = invisData.BreaksOnAttack ? "Breaks on attack/hostile action" : "Does NOT break on attack";

            list.Add(new IconData
            {
                Key = "Invisibility",
                ShortLabel = "INV",
                Tooltip = $"{sourceName}\nTotal concealment ({invisData.ConcealmentMissChance}% miss chance)\n+2 attack bonus vs sighted opponents\nOpponents denied Dex to AC\nHide bonus: +{hideBonus} ({(moving ? "moving" : "stationary")})\n{breakNote}\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.38f, 0.72f, 0.95f, 0.92f),
                Duration = rounds
            });
        }
        else if (statusMgr != null && statusMgr.HasEffect(SpellNames.INVISIBILITY))
        {
            // Fallback for edge cases where StatusEffectManager has the effect but ActiveInvisibilityEffect is null
            int rounds = statusMgr.GetRemainingRounds(SpellNames.INVISIBILITY);
            list.Add(new IconData
            {
                Key = "Invisibility",
                ShortLabel = "INV",
                Tooltip = $"Invisibility\nTotal concealment (50% miss chance)\n+2 attack bonus vs sighted opponents\nOpponents denied Dex to AC\nBreaks on attack/hostile action\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.38f, 0.72f, 0.95f, 0.92f),
                Duration = rounds
            });
        }

        if (_character.HasActiveBlurEffect)
        {
            int rounds = _character.GetBlurRemainingRounds();
            list.Add(new IconData
            {
                Key = "Blur",
                ShortLabel = "BLR",
                Tooltip = $"Blur\nConcealment (20% miss chance)\nVisual distortion: wavering outline\nSee Invisible does not negate Blur\nDuration: {rounds} rounds",
                Color = new Color(0.62f, 0.82f, 1f, 0.93f),
                Duration = rounds
            });
        }

        if (_character.HasActiveBlinkEffect)
        {
            int rounds = _character.GetBlinkRemainingRounds();
            list.Add(new IconData
            {
                Key = "Blink",
                ShortLabel = "BK",
                Tooltip = $"Blink\nEthereal concealment (50% miss chance)\n20% miss chance on own attacks\n+2 attack vs targets that can't see invisible\nDeny target Dex to AC\n20% spell failure on own spells\nHalf damage from area attacks\nDuration: {rounds} rounds",
                Color = new Color(0.55f, 0.40f, 0.95f, 0.92f),
                Duration = rounds
            });
        }

        if (statusMgr != null && statusMgr.HasEffect(SpellNames.SEE_INVISIBLE))
        {
            int rounds = statusMgr.GetRemainingRounds(SpellNames.SEE_INVISIBLE);
            list.Add(new IconData
            {
                Key = "SeeInvisible",
                ShortLabel = "EYE",
                Tooltip = $"See Invisible\nYou can now see invisible creatures!\nNegates invisibility miss chance and invisibility Hide/AC bonuses\nDoes not negate mundane hiding or reveal illusions\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.22f, 0.88f, 0.96f, 0.92f),
                Duration = rounds
            });
        }

        if (_character.HasActiveGlitterdustEffect)
        {
            GlitterdustEffectData glitterdust = _character.ActiveGlitterdustEffect;
            int rounds = glitterdust != null ? Mathf.Max(0, glitterdust.DurationRemainingRounds) : 0;
            bool blinded = glitterdust != null && glitterdust.IsBlinded;
            list.Add(new IconData
            {
                Key = "Glitterdust",
                ShortLabel = "GD",
                Tooltip = $"Glitterdust (Outlined)\nGolden dust outline is visible to everyone\nInvisibility provides no concealment\nHide checks: -40 penalty\nCannot be removed before expiry\nBlinded from save failure: {(blinded ? "Yes" : "No")}\nDuration: {(rounds < 0 ? "∞" : $"{rounds} rounds")}",
                Color = new Color(1f, 0.82f, 0.18f, 0.95f),
                Duration = rounds
            });
        }

        if (_character.HasActiveMelfsAcidArrowEffect)
        {
            MelfsAcidArrowEffectData acid = _character.ActiveMelfsAcidArrowEffect;
            int rounds = acid != null ? Mathf.Max(0, acid.RemainingDamageRounds) : 0;
            list.Add(new IconData
            {
                Key = "MelfsAcidArrow",
                ShortLabel = "ACD",
                Tooltip = $"Melf's Acid Arrow\nAcid continues to burn for 2d4 damage at start of turn\nNo save, no spell resistance\nRounds remaining: {rounds}",
                Color = new Color(0.18f, 0.82f, 0.35f, 0.95f),
                Duration = rounds
            });
        }

        if (_character.HasActiveHasteEffect)
        {
            int rounds = _character.GetHasteRemainingRounds();
            list.Add(new IconData
            {
                Key = "Haste",
                ShortLabel = "HS",
                Tooltip = $"Haste\n+1 attack rolls\n+1 dodge AC\n+1 Reflex saves\n+30 ft movement speed\nExtra attack at full BAB on full attack\nCounters/dispels Slow\nDuration: {rounds} rounds",
                Color = new Color(0.3f, 0.9f, 0.4f, 0.92f),
                Duration = rounds
            });
        }

        if (_character.HasActiveSlowEffect)
        {
            int rounds = _character.GetSlowRemainingRounds();
            list.Add(new IconData
            {
                Key = "Slow",
                ShortLabel = "SL",
                Tooltip = $"Slow\n-1 attack rolls\n-1 AC\n-1 Reflex saves\nMovement speed halved\nNo full-round actions\nCounters/dispels Haste\nDuration: {rounds} rounds",
                Color = new Color(0.6f, 0.45f, 0.75f, 0.92f),
                Duration = rounds
            });
        }

        if (_character.ActiveTouchOfIdiocyEffect != null && _character.Stats != null)
        {
            TouchOfIdiocyConditionData idiocy = _character.ActiveTouchOfIdiocyEffect;
            int rounds = idiocy != null ? Mathf.Max(0, idiocy.RemainingRounds) : 0;
            int intDamage = _character.Stats.GetAbilityDamage(AbilityType.INT);
            int wisDamage = _character.Stats.GetAbilityDamage(AbilityType.WIS);
            int chaDamage = _character.Stats.GetAbilityDamage(AbilityType.CHA);
            list.Add(new IconData
            {
                Key = "TouchOfIdiocy",
                ShortLabel = "IDI",
                Tooltip = $"Touch of Idiocy\nIntelligence damage: {intDamage}\nWisdom damage: {wisDamage}\nCharisma damage: {chaDamage}\nSpecial: does not cause unconsciousness at 0 mental scores\nDuration: {rounds} rounds",
                Color = new Color(0.74f, 0.45f, 1f, 0.95f),
                Duration = rounds
            });
        }

        if (statusMgr != null && statusMgr.HasEffect(SpellNames.JUMP))
        {
            int rounds = statusMgr.GetRemainingRounds(SpellNames.JUMP);
            int jumpBonus = _character.Stats != null ? Mathf.Max(0, _character.Stats.JumpEnhancementBonus) : 0;

            list.Add(new IconData
            {
                Key = "Jump",
                ShortLabel = "JP",
                Tooltip = $"Jump\n+{jumpBonus} enhancement to Jump checks\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.35f, 0.85f, 0.55f, 0.9f),
                Duration = rounds
            });
        }

        // Size change spells: Enlarge/Reduce Person and Mass variants
        if (statusMgr != null && (statusMgr.HasEffect(SpellNames.ENLARGE_PERSON) || statusMgr.HasEffect(SpellNames.MASS_ENLARGE_PERSON)))
        {
            string enlargeSpellId = statusMgr.HasEffect(SpellNames.MASS_ENLARGE_PERSON) ? SpellNames.MASS_ENLARGE_PERSON : SpellNames.ENLARGE_PERSON;
            int rounds = statusMgr.GetRemainingRounds(enlargeSpellId);
            string spellLabel = enlargeSpellId == SpellNames.MASS_ENLARGE_PERSON ? "Mass Enlarge Person" : "Enlarge Person";
            list.Add(new IconData
            {
                Key = "EnlargePerson",
                ShortLabel = "EN",
                Tooltip = $"{spellLabel}\n+2 STR (size), -2 DEX (size)\n-1 size penalty to AC and attack\nSize category increased by 1\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.9f, 0.65f, 0.3f, 0.92f),
                Duration = rounds
            });
        }

        if (statusMgr != null && (statusMgr.HasEffect(SpellNames.REDUCE_PERSON) || statusMgr.HasEffect(SpellNames.MASS_REDUCE_PERSON)))
        {
            string reduceSpellId = statusMgr.HasEffect(SpellNames.MASS_REDUCE_PERSON) ? SpellNames.MASS_REDUCE_PERSON : SpellNames.REDUCE_PERSON;
            int rounds = statusMgr.GetRemainingRounds(reduceSpellId);
            string spellLabel = reduceSpellId == SpellNames.MASS_REDUCE_PERSON ? "Mass Reduce Person" : "Reduce Person";
            list.Add(new IconData
            {
                Key = "ReducePerson",
                ShortLabel = "RD",
                Tooltip = $"{spellLabel}\n-2 STR (size), +2 DEX (size)\n+1 size bonus to AC and attack\nSize category decreased by 1\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.5f, 0.75f, 0.9f, 0.92f),
                Duration = rounds
            });
        }

        if (statusMgr != null && statusMgr.HasEffect(SpellNames.PROTECTION_FROM_ARROWS))
        {
            int rounds = statusMgr.GetRemainingRounds(SpellNames.PROTECTION_FROM_ARROWS);
            ProtectionFromArrowsEffectData protection = _character.Stats != null ? _character.Stats.ActiveProtectionFromArrowsEffect : null;
            int totalPool = protection != null ? Mathf.Max(0, protection.TotalAbsorptionPool) : 0;
            int remainingPool = protection != null ? Mathf.Max(0, protection.RemainingAbsorptionPool) : 0;
            int blocked = protection != null ? Mathf.Max(0, protection.AttacksBlocked) : 0;
            int drAmount = protection != null ? Mathf.Max(0, protection.DamageReductionAmount) : 10;

            list.Add(new IconData
            {
                Key = "ProtectionFromArrows",
                ShortLabel = "PA",
                Tooltip = $"Protection from Arrows\nDR {drAmount}/magic vs ranged weapons\nAbsorption pool: {remainingPool}/{totalPool}\nAttacks blocked: {blocked}\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                Color = new Color(0.38f, 0.62f, 0.95f, 0.92f),
                Duration = rounds
            });
        }

        if (_character.Stats != null && _character.Stats.ActiveResistEnergyEffects != null && _character.Stats.ActiveResistEnergyEffects.Count > 0)
        {
            for (int i = 0; i < _character.Stats.ActiveResistEnergyEffects.Count; i++)
            {
                ResistEnergyEffectData resist = _character.Stats.ActiveResistEnergyEffects[i];
                if (resist == null || resist.ResistanceAmount <= 0)
                    continue;

                string typeLabel = DamageTextUtils.GetDamageTypeDisplay(resist.ToDamageType());
                int rounds = resist.DurationRemainingRounds;
                list.Add(new IconData
                {
                    Key = $"ResistEnergy_{typeLabel}_{resist.ResistanceAmount}",
                    ShortLabel = "RE",
                    Tooltip = $"Resist Energy ({char.ToUpperInvariant(typeLabel[0]) + typeLabel.Substring(1)} {resist.ResistanceAmount})\nDuration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                    Color = new Color(0.3f, 0.78f, 0.9f, 0.92f),
                    Duration = rounds
                });
            }
        }

        if (_character.Stats != null && _character.Stats.ActiveProtectionFromEnergyEffects != null && _character.Stats.ActiveProtectionFromEnergyEffects.Count > 0)
        {
            for (int i = 0; i < _character.Stats.ActiveProtectionFromEnergyEffects.Count; i++)
            {
                ProtectionFromEnergyEffectData protEnergy = _character.Stats.ActiveProtectionFromEnergyEffects[i];
                if (protEnergy == null || protEnergy.RemainingAbsorptionPoints <= 0)
                    continue;

                string typeLabel = protEnergy.GetDisplayLabel();
                int rounds = protEnergy.DurationRemainingRounds;
                list.Add(new IconData
                {
                    Key = $"ProtEnergy_{typeLabel}_{protEnergy.RemainingAbsorptionPoints}",
                    ShortLabel = "PE",
                    Tooltip = $"Protection from Energy ({char.ToUpperInvariant(typeLabel[0]) + typeLabel.Substring(1)})\n"
                            + $"Absorbs {typeLabel} damage\n"
                            + $"Points: {protEnergy.RemainingAbsorptionPoints}/{protEnergy.MaxAbsorptionPoints}\n"
                            + $"Duration: {(rounds < 0 ? "∞" : $"{Mathf.Max(0, rounds)} rounds")}",
                    Color = new Color(0.25f, 0.75f, 0.55f, 0.92f),
                    Duration = rounds
                });
            }
        }

        bool hasFatigueCondition = _character.Stats.HasFatiguedCondition;
        bool hasExhaustedCondition = _character.Stats.HasExhaustedCondition;

        // Legacy barbarian fatigue state may exist without an explicit combat condition.
        if (_character.Stats.IsFatigued && !hasFatigueCondition && !hasExhaustedCondition)
        {
            list.Add(new IconData
            {
                Key = "FatiguedLegacy",
                ShortLabel = "FT",
                Tooltip = "Fatigued\n-2 STR, -2 DEX\nCannot charge or run",
                Color = new Color(0.92f, 0.45f, 0.45f, 0.9f),
                Duration = -1
            });
        }

        // Negative Levels (Energy Drained)
        int negLevels = _character.NegativeLevelCount;
        if (negLevels > 0)
        {
            int enervationRounds = statusMgr != null && statusMgr.HasEffect(SpellNames.ENERVATION)
                ? statusMgr.GetRemainingRounds(SpellNames.ENERVATION)
                : -1;
            string durationStr = enervationRounds > 0
                ? $"\nDuration: {enervationRounds} rounds ({Mathf.CeilToInt(enervationRounds / 600f)} hour(s))"
                : "";
            list.Add(new IconData
            {
                Key = "NegativeLevels",
                ShortLabel = $"-{negLevels}",
                Tooltip = $"Energy Drained ({negLevels} negative level{(negLevels > 1 ? "s" : "")})\n"
                         + $"-{negLevels} on all attack rolls, saves, skill checks\n"
                         + $"-{negLevels * 5} hit points\n"
                         + $"-{negLevels} effective level(s){durationStr}",
                Color = new Color(0.55f, 0.2f, 0.6f, 0.95f),
                Duration = enervationRounds
            });
        }

        // Active Diseases
        if (_character.ActiveDiseases != null && _character.ActiveDiseases.Count > 0)
        {
            foreach (ActiveDisease disease in _character.ActiveDiseases)
            {
                if (disease == null || disease.DiseaseData == null) continue;
                string statusStr = disease.IsIncubating
                    ? $"Incubating ({disease.DaysUntilActive} day(s) remaining)"
                    : $"Active ({disease.ConsecutiveSuccessfulSaves}/2 saves toward cure)";
                list.Add(new IconData
                {
                    Key = $"Disease_{disease.DiseaseData.Name}",
                    ShortLabel = "DIS",
                    Tooltip = $"Disease: {disease.DiseaseData.Name}\n"
                             + $"Status: {statusStr}\n"
                             + $"Fort DC {disease.DiseaseData.FortitudeDC} daily\n"
                             + $"{disease.DiseaseData.Description}",
                    Color = new Color(0.75f, 0.55f, 0.2f, 0.95f),
                    Duration = -1
                });
            }
        }

        // Bestow Curse effects
        if (_character.HasCondition(CombatConditionType.BestowCurseGeneralPenalty))
        {
            list.Add(new IconData
            {
                Key = "BestowCurseGeneral",
                ShortLabel = "CRS",
                Tooltip = "Bestow Curse (General)\n-4 penalty on attack rolls, saves,\nability checks, and skill checks.\nPermanent until removed.",
                Color = new Color(0.55f, 0f, 0f, 0.95f),
                Duration = -1
            });
        }
        if (_character.HasCondition(CombatConditionType.BestowCurseActionLoss))
        {
            list.Add(new IconData
            {
                Key = "BestowCurseAction",
                ShortLabel = "CRS",
                Tooltip = "Bestow Curse (Action Loss)\n50% chance each turn to lose\nall actions.\nPermanent until removed.",
                Color = new Color(0.55f, 0f, 0f, 0.95f),
                Duration = -1
            });
        }

        // Greater Invisibility
        if (_character.HasActiveInvisibilityEffect && _character.ActiveInvisibilityEffect != null
            && !_character.ActiveInvisibilityEffect.BreaksOnAttack)
        {
            InvisibilityEffectData gInvisData = _character.ActiveInvisibilityEffect;
            int gInvisRounds = Mathf.Max(0, gInvisData.DurationRemainingRounds);
            list.Add(new IconData
            {
                Key = "GreaterInvisibility",
                ShortLabel = "GI",
                Tooltip = $"Greater Invisibility\n+2 attack, enemies denied Dex to AC\n50% miss chance\nDoes NOT break on attack\nDuration: {gInvisRounds} round(s)",
                Color = new Color(0.6f, 0.4f, 1f, 0.95f),
                Duration = gInvisRounds
            });
        }

        // Bestow Curse - ability penalty tracked via ability damage with "Bestow Curse" source
        if (statusMgr != null && statusMgr.GetRemainingRounds(SpellNames.BESTOW_CURSE) != 0)
        {
            // Only show if not already showing a condition-based curse icon
            if (!_character.HasCondition(CombatConditionType.BestowCurseGeneralPenalty)
                && !_character.HasCondition(CombatConditionType.BestowCurseActionLoss))
            {
                list.Add(new IconData
                {
                    Key = "BestowCurseAbility",
                    ShortLabel = "CRS",
                    Tooltip = "Bestow Curse (Ability Penalty)\n-6 to one ability score.\nPermanent until removed.",
                    Color = new Color(0.55f, 0f, 0f, 0.95f),
                    Duration = -1
                });
            }
        }

        return list;
    }

    private string BuildSignature(List<IconData> data)
    {
        if (data == null || data.Count == 0)
            return "none";

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < data.Count; i++)
        {
            sb.Append(data[i].Key);
            sb.Append(':');
            sb.Append(data[i].Duration);
            sb.Append('|');
        }
        return sb.ToString();
    }

    private void RebuildIcons(List<IconData> data)
    {
        for (int i = 0; i < _activeIcons.Count; i++)
        {
            if (_activeIcons[i] != null)
                Destroy(_activeIcons[i]);
        }
        _activeIcons.Clear();

        if (data == null || data.Count == 0)
            return;

        float totalWidth = data.Count * iconSize + (data.Count - 1) * iconSpacing;
        float startX = -totalWidth * 0.5f + iconSize * 0.5f;

        for (int i = 0; i < data.Count; i++)
        {
            IconData iconData = data[i];
            GameObject icon = CreateIconObject(iconData);
            icon.transform.localPosition = new Vector3(startX + i * (iconSize + iconSpacing), 0f, 0f);
            _activeIcons.Add(icon);
        }
    }

    private GameObject CreateIconObject(IconData data)
    {
        GameObject go = new GameObject($"Status_{data.Key}");
        go.transform.SetParent(_iconContainer, false);
        go.transform.localScale = new Vector3(iconSize, iconSize, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = _badgeSprite;
        sr.color = data.Color;
        sr.sortingOrder = iconSortingOrder;

        GameObject textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        textGO.transform.localPosition = new Vector3(0f, 0f, 0f);

        TextMesh text = textGO.AddComponent<TextMesh>();
        text.text = data.ShortLabel;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.16f;
        text.fontSize = 48;
        text.color = new Color(1f, 1f, 1f, 0.96f);

        MeshRenderer textRenderer = textGO.GetComponent<MeshRenderer>();
        if (textRenderer != null)
            textRenderer.sortingOrder = iconSortingOrder + 1;

        return go;
    }

    private void UpdateTooltip(List<IconData> iconData)
    {
        if (StatusEffectTooltipUI.Instance == null)
            return;

        if (iconData == null || iconData.Count == 0)
            return;

        int hoveredIconIndex = GetHoveredIconIndex();
        if (hoveredIconIndex < 0 || hoveredIconIndex >= iconData.Count)
            return;

        IconData hoveredIcon = iconData[hoveredIconIndex];
        StringBuilder sb = new StringBuilder();
        sb.Append(_character.Stats.CharacterName);
        sb.Append("\n• ");
        sb.Append(hoveredIcon.Tooltip);

        StatusEffectTooltipUI.Instance.ShowTooltip(sb.ToString(), GetMouseScreenPosition());
    }

    private int GetHoveredIconIndex()
    {
        Camera cam = Camera.main;
        if (cam == null || _activeIcons == null || _activeIcons.Count == 0)
            return -1;

        Vector3 mouseScreen = GetMouseScreenPosition();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(mouseScreen);

        for (int i = 0; i < _activeIcons.Count; i++)
        {
            GameObject icon = _activeIcons[i];
            if (icon == null)
                continue;

            SpriteRenderer iconRenderer = icon.GetComponent<SpriteRenderer>();
            if (iconRenderer == null || !iconRenderer.enabled)
                continue;

            Bounds iconBounds = iconRenderer.bounds;
            Vector3 testPoint = mouseWorld;
            testPoint.z = iconBounds.center.z;
            if (iconBounds.Contains(testPoint))
                return i;
        }

        return -1;
    }

    private static Vector3 GetMouseScreenPosition()
    {
        Vector3 mouseScreenPos = Vector3.zero;

#if ENABLE_LEGACY_INPUT_MANAGER
        mouseScreenPos = Input.mousePosition;
#endif

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
            mouseScreenPos = mouse.position.ReadValue();
#endif

        return mouseScreenPos;
    }

    private static string GetConditionLabel(CombatConditionType type)
    {
        CombatConditionType normalized = ConditionRules.Normalize(type);
        if (normalized == CombatConditionType.Blinded)
            return "BLIND";
        if (normalized == CombatConditionType.Dazed)
            return "★";
        if (normalized == CombatConditionType.HideousLaughter)
            return "HA";

        var def = ConditionRules.GetDefinition(type);
        if (!string.IsNullOrEmpty(def.ShortLabel))
            return def.ShortLabel;

        string raw = def.DisplayName;
        if (string.IsNullOrEmpty(raw))
            raw = type.ToString();
        return raw.Length <= 2 ? raw.ToUpperInvariant() : raw.Substring(0, 2).ToUpperInvariant();
    }

    private static Color GetConditionColor(CombatConditionType type)
    {
        switch (ConditionRules.Normalize(type))
        {
            case CombatConditionType.Invisible:
                return new Color(0.38f, 0.72f, 0.95f, 0.88f);
            case CombatConditionType.Asleep:
                return new Color(0.45f, 0.65f, 1f, 0.9f);
            case CombatConditionType.Flanked:
                return new Color(1f, 0.55f, 0.2f, 0.9f);
            case CombatConditionType.Turned:
                return new Color(1f, 0.95f, 0.66f, 0.92f);
            case CombatConditionType.Dazed:
                return new Color(1f, 0.85f, 0.35f, 0.9f);
            case CombatConditionType.HideousLaughter:
                return new Color(0.72f, 0.42f, 1f, 0.92f);
            case CombatConditionType.Prone:
            case CombatConditionType.Grappled:
            case CombatConditionType.Pinned:
            case CombatConditionType.Feinted:
            case CombatConditionType.ChargePenalty:
            case CombatConditionType.Disarmed:
            case CombatConditionType.Blinded:
            case CombatConditionType.Entangled:
            case CombatConditionType.Stunned:
            case CombatConditionType.Paralyzed:
            case CombatConditionType.Helpless:
            case CombatConditionType.Nauseated:
            case CombatConditionType.Panicked:
                return new Color(0.9f, 0.35f, 0.35f, 0.88f);
            default:
                return new Color(0.95f, 0.82f, 0.35f, 0.88f);
        }
    }

    private static string BuildConditionTooltip(StatusEffect condition)
    {
        string duration = condition.RemainingRounds < 0 ? "∞" : $"{Mathf.Max(0, condition.RemainingRounds)} rounds";
        var def = ConditionRules.GetDefinition(condition.Type);
        string source = string.IsNullOrEmpty(condition.SourceName) ? "Unknown" : condition.SourceName;

        if (!string.IsNullOrEmpty(def.Description))
            return $"{def.DisplayName}\n{def.Description}\nSource: {source}\nDuration: {duration}";

        return $"{def.DisplayName}\nSource: {source}\nDuration: {duration}";
    }

    private static Sprite CreateBadgeSprite()
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                pixels[y * size + x] = border
                    ? new Color(0f, 0f, 0f, 0.65f)
                    : Color.white;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}