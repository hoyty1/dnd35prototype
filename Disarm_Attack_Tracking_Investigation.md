# Attack Tracking System (Disarm vs Available Attacks)

## Quick note on variable names
The exact variables from your grep examples (`_remainingAttacks`, `_remainingMainHandAttacks`, `_remainingOffHandAttacks`, `currentAttackIndex`) are **not present** in the current code.

This system now tracks attack usage with a newer model:
- `_totalAttackBudget` (how many main-hand iterative attacks are available in the active sequence)
- `_totalAttacksUsed` (how many main-hand iterative attacks have been consumed)
- `_currentAttackBAB` (the BAB being used for the current/next main-hand iterative attack)
- `_offHandAttackAvailableThisTurn` and `_offHandAttackUsedThisTurn` (off-hand gate)

**Primary file:** `Assets/Scripts/Core/GameManager.cs`

---

## 1. Turn Start - Calculate Total Attacks

### Code locations
- Turn reset and per-turn attack flags: `GameManager.cs:2591-2653`
- Main-hand sequence initialization: `GameManager.cs:5736-5752`
- BAB-derived iterative count/values source: `CharacterController.cs:377-389`
- BAB ladder definition (+6/+11/+16): `CharacterController.cs:356-363` and `365-373`

### What happens
At turn start (`StartPCTurn`), GameManager resets off-hand and dual-wield turn state:
- `_offHandAttackUsedThisTurn = false`
- `_offHandAttackAvailableThisTurn = pc.HasOffHandWeaponEquipped()`
- `_dualWieldingChoiceMade = false`, `_isDualWielding = false`
- penalties reset (`_mainHandPenalty`, `_offHandPenalty`)

The **main-hand iterative attack budget** is created only when an attack sequence starts (`StartAttackSequence`):
- `_totalAttackBudget = Mathf.Max(1, attacker.GetIterativeAttackCount())`
- `_totalAttacksUsed = 0`

`GetIterativeAttackCount()` comes from BAB breakpoints:
- BAB 0 to +5 => 1 attack
- BAB +6 to +10 => 2 attacks
- BAB +11 to +15 => 3 attacks
- BAB +16+ => 4 attacks

Example (BAB +11): attack bonuses = `+11, +6, +1`; budget = 3.

---

## 2. Shared Attack Pool (Normal + Disarm)

### Code locations
- Disarm remaining-action calculation: `GameManager.cs:5879-5902`
- Disarm gating: `GameManager.cs:5937-5950`
- Main-hand sequence state declaration: `GameManager.cs:145-153`
- Main-hand normal attack consumption: `GameManager.cs:14403-14433`
- Main-hand disarm consumption: `GameManager.cs:6014-6047`

### Core behavior
Disarm uses the **same main-hand iterative sequence pool** as normal attacks:
- same budget counter (`_totalAttackBudget`)
- same usage counter (`_totalAttacksUsed`)
- same iterative BAB progression (`GetIterativeAttackBAB(index)` and `_currentAttackBAB`)

There is **no separate disarm-only main-hand counter**. `GetRemainingDisarmAttempts()` explicitly derives remaining disarm actions from:
1. main-hand sequence remaining (`_totalAttackBudget - _totalAttacksUsed`) when in sequence, plus
2. possible off-hand disarm availability (at most +1 in this GameManager flow)

So disarm and normal main-hand attacks compete for the same iterative slots.

---

## 3. Disarm-Specific Methods

### A) `CanUseDisarmAttackOption()`
**Location:** `GameManager.cs:5937-5950`

Checks:
1. attacker/actions exist
2. attacker has a disarm-capable held weapon (`main-hand OR off-hand`)
3. no conflicting sequence owner
4. `GetRemainingDisarmAttempts(attacker) > 0`

### B) `GetRemainingDisarmAttackActions()`
**Location:** `GameManager.cs:5952-5955`

Returns `GetRemainingDisarmAttempts(attacker)` directly (shared pool accounting).

### C) `GetCurrentDisarmAttackBonus()`
**Location:** `GameManager.cs:5957-5959` (backed by `5904-5935`)

Behavior:
- If attacker is in active shared main-hand sequence and has attacks left: return `_currentAttackBAB`
- Else if full-round still available: return first iterative BAB (`GetIterativeAttackBAB(0)`), plus main-hand dual-wield penalty if enabled
- Else if only standard action remains: return `BaseAttackBonus`
- Else if off-hand disarm is available: return off-hand BAB (`BaseAttackBonus` + off-hand penalty when dual-wielding)

### D) `TryConsumeDisarmAttackAction()`
**Location:** `GameManager.cs:6050-6102`

Flow:
1. Prefer main-hand iterative sequence:
   - if sequence already active and has room, consume from it
   - otherwise start shared main-hand disarm sequence (`TryStartMainHandDisarmSequence`) and consume
2. On main-hand consume:
   - use `_currentAttackBAB` as attack bonus
   - call `AdvanceMainHandSequenceAfterDisarmUse()` which increments `_totalAttacksUsed`
3. If main-hand not available, try off-hand disarm:
   - use off-hand BAB (base + off-hand penalty if dual-wielding)
   - set `_offHandAttackUsedThisTurn = true`

So disarm consumption mirrors normal sequence consumption for main hand, then uses off-hand gate as an extra lane.

---

## 4. Complete Flow Diagram

### Main-hand shared sequence flow (normal + disarm)
```text
Turn Start
├─ Reset off-hand and dual-wield turn flags
└─ No main-hand budget committed yet

First main-hand attack/disarm action
├─ StartAttackSequence / TryStartMainHandDisarmSequence
│  ├─ _totalAttackBudget = GetIterativeAttackCount()  (or 1 if standard-only path)
│  ├─ _totalAttacksUsed = 0
│  └─ _currentAttackBAB = iterative BAB at index 0 (+ penalties if dual wielding)

Each main-hand normal attack
├─ Perform attack using _currentAttackBAB
├─ _totalAttacksUsed++
├─ Prepare next _currentAttackBAB via GetIterativeAttackBAB(_totalAttacksUsed)
└─ End sequence when _totalAttacksUsed >= _totalAttackBudget

Each main-hand disarm
├─ TryConsumeDisarmAttackAction -> main-hand branch
├─ uses _currentAttackBAB
├─ AdvanceMainHandSequenceAfterDisarmUse -> _totalAttacksUsed++
├─ Prepare next _currentAttackBAB
└─ End sequence when exhausted
```

### Mixed example (BAB +11, dual wield off)
```text
Initial: budget=3, used=0, next BAB +11
Action 1 normal attack: used=1, next BAB +6
Action 2 disarm:        used=2, next BAB +1
Action 3 disarm:        used=3, sequence ends
Remaining main-hand iterative attacks: 0
```

---

## 5. How Remaining Attacks Are Calculated

### Main-hand remaining
`HasMoreAttacksAvailable()` (`GameManager.cs:5864-5871`):
- true when `_totalAttacksUsed < _totalAttackBudget`

### Disarm remaining
`GetRemainingDisarmAttempts()` (`GameManager.cs:5879-5902`):
- Main-hand part:
  - active sequence: `_totalAttackBudget - _totalAttacksUsed`
  - else full-round available: `GetIterativeAttackCount()`
  - else standard available: `1`
- Off-hand part:
  - adds `1` if `CanUseOffHandAttackOption(attacker)` and not blocked by standard-only/no-sequence edge check

Total disarm-capable attacks = main-hand remaining + off-hand remaining.

---

## 6. How Iterative Attacks (BAB) Are Tracked

### Code locations
- BAB ladder/bonuses: `CharacterController.cs:356-389`
- Main-hand current BAB set before each attack: `GameManager.cs:5842-5857`
- Next BAB after normal attack: `GameManager.cs:14425-14433`
- Next BAB after disarm: `GameManager.cs:6037-6042`

### Behavior
- Iterative BAB values come from `GetAttackBonuses()` (`BAB`, `BAB-5`, `BAB-10`, `BAB-15` as available)
- Sequence position is effectively `_totalAttacksUsed` (replaces old `currentAttackIndex` concept)
- Current displayed/used value is `_currentAttackBAB`
- Dual-wield main-hand penalty is applied on top when enabled (`_mainHandPenalty`)

---

## 7. How Dual Wielding Is Tracked

### Code locations
- Turn reset for dual-wield flags: `GameManager.cs:2645-2653`
- Prompt and choice: `GameManager.cs:5208-5274`
- Penalty calculation: `GameManager.cs:5276-5307`
- Off-hand availability gate: `GameManager.cs:5636-5689`
- Off-hand consume in disarm path: `GameManager.cs:6075-6095`

### Behavior
Dual-wielding is represented by:
- `_isDualWielding`
- `_mainHandPenalty`, `_offHandPenalty`
- `_offHandAttackAvailableThisTurn`, `_offHandAttackUsedThisTurn`

Important distinction:
- Main-hand iterative chain uses `_totalAttackBudget/_totalAttacksUsed`
- Off-hand is tracked separately by availability/used flags (not by `_totalAttackBudget`)

For disarm:
- Main-hand disarm consumes iterative pool first
- Off-hand disarm can still be consumed if off-hand gate is open

---

## 8. How the UI Shows Remaining Attacks

### Code locations
- Turn indicator messaging with remaining iterative/disarm attacks: `GameManager.cs:2827-2856`
- Main action panel Special Attack label: `CombatUI.cs:1033-1063`
- Special Attack menu Disarm label: `CombatUI.cs:1747-1753`
- Post-disarm combat log summary: `GameManager.cs:13440-13456`

### UI outputs
1. **Turn indicator** (top/status line) shows:
   - "Disarm-capable attacks remaining: X (next BAB +Y)"
2. **Special Attack button label** in main actions can show:
   - `Special Attack (Disarm Iterative +Y, X left)`
3. **Disarm option in submenu** can show:
   - `Disarm (BAB +Y, X left)`
4. **Combat log** after each disarm attempt prints:
   - attempt number, hand used (main/off), BAB used, remaining count

---

## 9. Key Code Snippets

### 9.1 Main-hand budget init
- `GameManager.cs:5744-5745`
```csharp
_totalAttackBudget = Mathf.Max(1, attacker.GetIterativeAttackCount());
_totalAttacksUsed = 0;
```

### 9.2 Main-hand normal attack consumption
- `GameManager.cs:14403-14404`
```csharp
_totalAttacksUsed++;
```

### 9.3 Main-hand disarm consumption
- `GameManager.cs:6019-6020`
```csharp
_totalAttacksUsed++;
Debug.Log($"[Disarm][Flow] Main-hand disarm consumed iterative attack {_totalAttacksUsed}/{_totalAttackBudget}.");
```

### 9.4 Remaining disarm-capable attacks
- `GameManager.cs:5901`
```csharp
return Mathf.Max(0, mainHandRemaining + offHandRemaining);
```

### 9.5 Iterative BAB source
- `CharacterController.cs:370-372`
```csharp
if (bab >= 6) bonuses.Add(bab - 5);
if (bab >= 11) bonuses.Add(bab - 10);
if (bab >= 16) bonuses.Add(bab - 15);
```

---

## 10. Conclusion
The disarm system in current `GameManager` is integrated into a **shared iterative main-hand attack economy** plus a **separate off-hand gate**:
- normal attacks and main-hand disarms consume the same `_totalAttackBudget/_totalAttacksUsed` pool,
- iterative BAB progression is shared (`_currentAttackBAB` + `GetIterativeAttackBAB(index)`),
- off-hand disarm is tracked independently with `_offHandAttackAvailableThisTurn/_offHandAttackUsedThisTurn`,
- UI uses `GetRemainingDisarmAttackActions()` and `GetCurrentDisarmAttackBonus()` to present remaining count and next BAB consistently.
