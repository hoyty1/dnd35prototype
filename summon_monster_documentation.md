# Summon Monster Implementation (Extracted from Code)

Repository: `/home/ubuntu/dnd35prototype`  
Primary implementation: `Assets/Scripts/Core/GameManager.cs`

---

## 1) Architecture map

- **Core lifecycle + combat integration**: `Assets/Scripts/Core/GameManager.cs`
- **Command model**: `Assets/Scripts/Core/SummonCommand.cs`
- **AI routing**: `Assets/Scripts/Services/AIService.cs` (routes summons back into GameManager)
- **Summon AI behavior**: `AI_SummonedCreature(...)` in `GameManager.cs`
- **Visual effects and duration bar**: `Assets/Scripts/UI/SummonedCreatureVisual.cs`
- **Summon selection + right-click command menu UI**: `Assets/Scripts/UI/CombatUI.cs`
- **Spell registrations**: `Assets/Scripts/Magic/SpellDatabase_*`

---

## 2) Summon data model in GameManager

```csharp
private class ActiveSummonInstance
{
    public CharacterController Controller;
    public CharacterController Caster;
    public SummonTemplate Template;
    public int RemainingRounds;
    public int TotalDurationRounds;
    public string SourceSpellId;
    public bool IsAlliedToPCs;
    public bool SmiteUsed;
    public SummonCommand CurrentCommand;
}

private class SummonTemplate
{
    public string TemplateId;
    public string DisplayName;
    public string CharacterClass;
    public string TokenType;
    public Color TintColor;
    public int Level;
    public int STR, DEX, CON, WIS, INT, CHA;
    public int BAB;
    public int ArmorBonus;
    public int ShieldBonus;
    public int DamageDice;
    public int DamageCount;
    public int BonusDamage;
    public int BaseSpeed;
    public int AttackRange;
    public global::SizeCategory SizeCategory;
    public int BaseHitDieHP;
    public string CreatureTypeLine;
    public string AttackLabel;
    public bool IsCelestial;
    public bool IsFiendish;
    public bool HasTrip;
    public bool HasDisease;
    public bool HasMultiAttack;
    public List<string> SpecialTraits = new List<string>();
    public List<string> CreatureTags = new List<string>();
}
```

- `ActiveSummonInstance` tracks runtime state (owner/caster, rounds remaining, command mode, smite usage).
- `SummonTemplate` is the static stat/trait/template source used at spawn.

---

## 3) Spell IDs and template lookup

```csharp
private bool IsSummonMonsterSpell(SpellData spell)
{
    if (spell == null || string.IsNullOrEmpty(spell.SpellId)) return false;
    string normalized = NormalizeSummonSpellId(spell.SpellId);
    return normalized == "summon_monster_1" || normalized == "summon_monster_2";
}

private string NormalizeSummonSpellId(string spellId)
{
    if (string.IsNullOrEmpty(spellId)) return "";
    if (spellId == "summon_monster_1" || spellId == "summon_monster_1_clr") return "summon_monster_1";
    if (spellId == "summon_monster_2" || spellId == "summon_monster_2_clr") return "summon_monster_2";
    return spellId;
}

private List<SummonTemplate> GetSummonOptionsForSpell(SpellData spell)
{
    if (spell == null) return null;
    string normalized = NormalizeSummonSpellId(spell.SpellId);
    if (SummonMonsterOptions.TryGetValue(normalized, out var options)) return options;
    return null;
}
```

### Template library currently hardcoded in GameManager
- `summon_monster_1`: Celestial Dog, Fiendish Wolf, Small Air Elemental
- `summon_monster_2`: Celestial Wolf, Fiendish Boar, Small Fire Elemental

Each option is declared with stats, tags, traits, and flags like `IsCelestial` / `IsFiendish`.

---

## 4) Cast flow (from spell pick to summon spawn)

### 4.1 Spell selection enters summon placement mode

```csharp
if (IsSummonMonsterSpell(_pendingSpell))
{
    _pendingAttackMode = PendingAttackMode.CastSpell;
    CurrentSubPhase = PlayerSubPhase.SelectingAttackTarget;
    ShowSummonPlacementTargets(caster, _pendingSpell);
    return;
}
```

### 4.2 Valid summon tiles highlighted

```csharp
int range = spell != null && spell.RangeSquares > 0 ? spell.RangeSquares : 1;
List<SquareCell> cells = Grid.GetCellsInRange(caster.GridPosition, range);
...
if (!cell.IsOccupied)
{
    cell.SetHighlight(HighlightType.SpellTarget);
    _highlightedCells.Add(cell);
}
```

### 4.3 Clicking tile opens summon choice UI

```csharp
List<SummonTemplate> options = GetSummonOptionsForSpell(_pendingSpell);
...
CombatUI.ShowSummonCreatureSelection(_pendingSpell.Name,
    options.ConvertAll(o => o.DisplayName),
    onSelect: (idx) => { ... PerformSummonMonsterCast(pc, selectedCell, options[idx]); },
    onCancel: () => { ... });
```

### 4.4 Cast execution + resource/AoO handling + spawn

```csharp
CaptureSpellcastResourceSnapshot(caster);
if (!TryConsumePendingSpellCast(caster)) { ... }

ResolveSpellcastProvocation(caster, _pendingSpell, false, canProceed =>
{
    if (!canProceed) { ... }

    CharacterController summonCC = SpawnSummonedCreature(caster, targetCell.Coords, template);
    if (summonCC == null) { ... }

    InsertIntoInitiative(summonCC, caster);

    int durationRounds = Mathf.Max(1, caster.Stats.Level);
    var activeSummon = new ActiveSummonInstance
    {
        Controller = summonCC,
        Caster = caster,
        Template = template,
        RemainingRounds = durationRounds,
        TotalDurationRounds = durationRounds,
        SourceSpellId = _pendingSpell.SpellId,
        IsAlliedToPCs = summonCC.Team == CharacterTeam.Player,
        SmiteUsed = false,
        CurrentCommand = SummonCommand.AttackNearest()
    };
    _activeSummons.Add(activeSummon);
});
```

---

## 5) Spawn implementation details

`SpawnSummonedCreature(...)` builds a fresh `CharacterController` object and stat block from the selected `SummonTemplate`:

```csharp
CharacterStats stats = new CharacterStats(
    name: template.DisplayName,
    level: template.Level,
    characterClass: template.CharacterClass,
    str: template.STR, dex: template.DEX, con: template.CON,
    wis: template.WIS, intelligence: template.INT, cha: template.CHA,
    bab: template.BAB,
    armorBonus: template.ArmorBonus,
    shieldBonus: template.ShieldBonus,
    damageDice: template.DamageDice,
    damageCount: template.DamageCount,
    bonusDamage: template.BonusDamage,
    baseSpeed: template.BaseSpeed,
    atkRange: template.AttackRange,
    baseHitDieHP: template.BaseHitDieHP
);
```

### Celestial/Fiendish template application at spawn

```csharp
if (template.IsCelestial)
    stats.CharacterAlignment = Alignment.NeutralGood;
else if (template.IsFiendish)
    stats.CharacterAlignment = Alignment.NeutralEvil;
else
    stats.CharacterAlignment = Alignment.TrueNeutral;

stats.IsCelestialTemplate = template.IsCelestial;
stats.IsFiendishTemplate = template.IsFiendish;
stats.HasTemplateSmiteEvil = template.IsCelestial;
stats.HasTemplateSmiteGood = template.IsFiendish;
stats.TemplateSmiteUsed = false;
```

Then it initializes rendering/components and registration:
- `summon.Init(stats, cell, alive, dead)`
- adds `InventoryComponent`, `StatusEffectManager`, `ConcentrationManager`
- appends to `NPCs` + `_npcAIBehaviors`
- tracks team-side in `_summonedAllies` / `_summonedEnemies`
- initializes `SummonedCreatureVisual`

---

## 6) Duration and lifecycle management

### Round tick hook

```csharp
private void OnNewRound(int round)
{
    ...
    TickSummonDurations();
    ...
}
```

### Duration decrement + warning + expiry

```csharp
summon.RemainingRounds--;
...
if (summon.RemainingRounds == 2)
    CombatUI?.ShowCombatLog("... 2 rounds remaining ...");
else if (summon.RemainingRounds == 1)
    CombatUI?.ShowCombatLog("... 1 round remaining ...");

if (summon.RemainingRounds <= 0)
    expired.Add(summon);
```

### Despawn process (expiry, dismissal, death)

```csharp
private IEnumerator DespawnSummonWithEffect(ActiveSummonInstance summon, string reason)
{
    ...
    var summonVisual = cc.GetComponent<SummonedCreatureVisual>();
    if (summonVisual != null)
        yield return StartCoroutine(summonVisual.PlayDespawnEffect());

    Grid.ClearCreatureOccupancy(cc);
    NPCs.RemoveAt(npcIdx); // + AI behavior removal
    _summonedAllies.Remove(cc);
    _summonedEnemies.Remove(cc);
    _turnService?.RemoveFromInitiative(cc);
    Destroy(cc.gameObject);
}
```

### Manual dismissal

```csharp
public void RequestDismissSummon(CharacterController summon)
{
    ...
    onConfirm: () =>
    {
        StartCoroutine(DespawnSummonWithEffect(active, "dismissed"));
        _activeSummons.Remove(active);
        UpdateAllStatsUI();
    }
}
```

### Death cleanup

```csharp
private void HandleSummonDeathCleanup(CharacterController maybeSummon)
{
    ActiveSummonInstance summon = GetActiveSummon(maybeSummon);
    if (summon == null) return;

    _activeSummons.Remove(summon);
    StartCoroutine(DespawnSummonWithEffect(summon, "destroyed"));
}
```

---

## 7) Command system (Attack Nearest / Protect Caster)

`SummonCommand.cs`:

```csharp
public enum SummonCommandType
{
    AttackNearest,
    ProtectCaster
}

[Serializable]
public class SummonCommand
{
    public SummonCommandType Type;
    public string Description;

    public static SummonCommand AttackNearest() ...
    public static SummonCommand ProtectCaster() ...
}
```

GameManager command assignment:

```csharp
public void SetSummonCommand(CharacterController summon, SummonCommand command)
{
    ...
    active.CurrentCommand = command;
    CombatUI?.ShowCombatLog($"<color=#66E8FF>{summonName}: {command.Description}.</color>");
}
```

Right-click command entry point:

```csharp
CombatUI?.ShowSummonContextMenu(
    summon,
    active.RemainingRounds,
    active.TotalDurationRounds,
    active.CurrentCommand,
    () => SetSummonCommand(summon, SummonCommand.AttackNearest()),
    () => SetSummonCommand(summon, SummonCommand.ProtectCaster()),
    () => RequestDismissSummon(summon));
```

---

## 8) AI integration

### In AIService: summon turns are routed back to GameManager

```csharp
bool isSummon = _gameManager.IsSummonedCreature(npc);
...
if (isSummon)
{
    yield return _gameManager.StartCoroutine(_gameManager.ExecuteSummonedCreatureTurnForAI(npc));
    yield break;
}
```

### In GameManager: summon behavior logic (`AI_SummonedCreature`)

Behavior includes:
- command-aware target selection (`AttackNearest` vs `ProtectCaster`)
- low-HP retreat attempt
- move-to-engage when out of range
- special Trip use if template supports it
- one-time template smite attempt (`Smite Evil`/`Smite Good`) based on target alignment
- fallback normal NPC attack

Smite alignment gate:

```csharp
bool smiteEvil = summon.Stats.HasTemplateSmiteEvil && AlignmentHelper.IsEvil(target.Stats.CharacterAlignment);
bool smiteGood = summon.Stats.HasTemplateSmiteGood && AlignmentHelper.IsGood(target.Stats.CharacterAlignment);
if (!smiteEvil && !smiteGood)
    return false;
```

---

## 9) UI details for commanding summons

`CombatUI.ShowSummonContextMenu(...)` builds a context menu near summon screen position and shows:
- current HP/AC
- duration remaining
- current command text
- buttons:
  - `Attack Nearest Enemy`
  - `Protect Me`
  - `Dismiss Summon`
  - `Cancel`

It marks currently active command with a checkmark suffix (`✓`).

`CombatUI.ShowSummonCreatureSelection(...)` builds the summon-choice modal for spell cast.

---

## 10) Visual effect system (`SummonedCreatureVisual`)

`SummonedCreatureVisual` is visual-only support for summons:
- aura color based on template origin (celestial gold / fiendish red / default cyan)
- `[S]` badge
- duration bar with color transitions and low-duration pulsing
- summon pulse effect (`PlaySummonEffect`)
- despawn fade/scale effect (`PlayDespawnEffect`)

Initialized from GameManager:

```csharp
var summonVisual = summon.gameObject.GetComponent<SummonedCreatureVisual>();
if (summonVisual == null)
    summonVisual = summon.gameObject.AddComponent<SummonedCreatureVisual>();
summonVisual.Init(summon, template.IsCelestial, template.IsFiendish);
```

---

## 11) Spell database state vs runtime behavior

Spell DB entries for Summon Monster I/II (wizard + cleric variants) still contain:

```csharp
IsPlaceholder = true,
PlaceholderReason = "[PLACEHOLDER - Summoning not implemented]"
```

However, `GameManager.cs` now contains a working summon flow (placement, spawn, commands, AI, duration, despawn).  
So **runtime support exists**, while spell metadata placeholders are stale/outdated.

---

## 12) End-to-end flow (cast ➜ despawn)

1. Player clicks **Cast Spell** and selects Summon Monster.
2. `OnSpellSelectedWithMetamagic` sets `_pendingSpell` and calls `BeginPendingSpellTargeting`.
3. Summon spells route into `ShowSummonPlacementTargets`.
4. Player clicks valid empty tile; summon options modal appears.
5. Player selects creature; `PerformSummonMonsterCast` handles slot use + cast provocation.
6. `SpawnSummonedCreature` instantiates summon and applies template traits/alignment.
7. Summon inserted into initiative after caster; `ActiveSummonInstance` created with `1 round/level` duration.
8. Each round start `TickSummonDurations` decrements and updates visuals/logs.
9. On summon turn, AIService routes to GameManager summon AI behavior.
10. Summon can be re-commanded via right-click context menu or dismissed manually.
11. On expiry/death/dismiss, `DespawnSummonWithEffect` removes visual, grid, initiative, lists, and object.

---

## 13) Important implementation observations

- **Template selection is player-choice based**, not caster-alignment auto-filtered; celestial/fiendish options are both presented in the list for that spell tier.
- **Alignment is still mechanically relevant** after spawn (smite targeting checks good/evil).
- **Summons are AI-driven** (`ConfigureTeamControl(... controllable: false)`), even when allied to players.
- **Duration source is caster level at cast time**: `Mathf.Max(1, caster.Stats.Level)`.
