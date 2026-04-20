# Aid Another - Manual Test Scenarios

## Updated Flow (Post-Refinement)

### Selection Order
1. **Select Enemy** - Only enemies threatened by initiator's weapon
2. **Select Aid Type** - Defense (+2 AC) or Offense (+2 Attack)
3. **Select Ally** - Only allies within melee range of selected enemy

## Test Case 1: Basic Aid Defense (Adjacent)
**Setup:**
- Fighter (initiator) at (5, 5) with longsword (5ft reach)
- Rogue (ally) at (6, 6)
- Orc (enemy) at (7, 6)

**Expected:**
1. Fighter clicks **Aid Another**
2. Enemy list shows: Orc (threatened by Fighter? No - distance=2)
3. If no enemies threatened: `Fighter doesn't threaten any enemies!`

**Fix Setup:**
- Fighter at (6, 5) - now adjacent to Orc
- Enemy list shows: Orc ✓
- Aid type: Defense
- Ally list shows: Rogue (within melee range of Orc? Yes - distance=1) ✓

## Test Case 2: Reach Weapon
**Setup:**
- Fighter (initiator) at (5, 5) with longspear (10ft reach)
- Rogue (ally) at (7, 6)
- Orc (enemy) at (7, 5)

**Expected:**
1. Fighter clicks **Aid Another**
2. Enemy list shows: Orc (distance=2, reach=2) ✓
3. Aid type: Offense
4. Ally list shows: Rogue (distance=1 from Orc) ✓

## Test Case 3: No Allies in Range
**Setup:**
- Fighter (initiator) at (5, 5) with longsword
- Rogue (ally) at (10, 10) - far away
- Orc (enemy) at (6, 5)

**Expected:**
1. Fighter clicks **Aid Another**
2. Enemy list shows: Orc ✓
3. Aid type: Defense
4. Ally list: Empty
5. Message: `No allies in melee range of Orc!`

## Test Case 4: Multiple Enemies Threatened
**Setup:**
- Fighter (initiator) at (5, 5) with longsword
- Orc1 at (6, 5)
- Orc2 at (5, 6)
- Goblin at (10, 10) - far away

**Expected:**
1. Fighter clicks **Aid Another**
2. Enemy list shows: Orc1, Orc2 (not Goblin)
3. Select Orc1
4. Aid type: Offense
5. Ally list shows allies near Orc1

## Test Case 5: Multiple Allies in Range
**Setup:**
- Fighter (initiator) at (5, 5)
- Rogue (ally) at (7, 5)
- Wizard (ally) at (6, 6)
- Cleric (ally) at (10, 10) - far away
- Orc (enemy) at (6, 5)

**Expected:**
1. Fighter clicks **Aid Another**
2. Enemy list shows: Orc ✓
3. Aid type: Defense
4. Ally list shows: Rogue, Wizard (not Cleric - too far)

## Verification Checklist
1. ✓ Click **Aid Another**
2. ✓ Verify only threatened enemies shown
3. ✓ Select enemy
4. ✓ Verify aid type selection appears
5. ✓ Select aid type
6. ✓ Verify only allies in melee range shown
7. ✓ Select ally
8. ✓ Verify melee touch attack rolls
9. ✓ Verify bonus applied correctly
10. ✓ Test with reach weapons
11. ✓ Test with no valid targets
12. ✓ Test with multiple valid targets
