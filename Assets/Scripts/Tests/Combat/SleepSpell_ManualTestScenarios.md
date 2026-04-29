# Sleep Spell - Manual Test Scenarios

## Setup
- Encounter: **💤 Sleep Spell Test** (`sleep_spell_test`)
- Caster: PC wizard with Sleep prepared.
- Enemies: mixed HD targets (includes at least one 5+ HD target expected to be immune).

## Test 1: HD Pool = 4d4
1. Cast **Sleep** centered to include all enemies.
2. Verify combat log shows a 4d4 pool value and remaining pool accounting.
3. Repeat several casts and confirm pool varies in expected 4–16 range.

## Test 2: Lowest-HD-First + 4 HD cap
1. Cast **Sleep** on mixed-HD cluster.
2. Confirm lower-HD targets are processed before higher-HD targets.
3. Confirm 5+ HD target is never put to sleep.

## Test 3: Wake on damage
1. Put a target to sleep.
2. Deal damage to sleeping target.
3. Confirm target wakes immediately and loses Asleep state.

## Test 4: Aid Another → Wake Sleeping Ally
1. Ensure an adjacent ally is asleep.
2. Use **Aid Another** and choose **Wake Sleeping Ally**.
3. Confirm:
   - Standard action consumed.
   - Combat log: `X shakes Y awake`.
   - Asleep/Unconscious removed.

## Test 5: Duration expiration
1. Put a target to sleep and wait out duration.
2. Confirm target wakes when duration ends and status is removed.
