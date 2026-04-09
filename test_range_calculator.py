#!/usr/bin/env python3
"""
Python test runner to verify D&D 3.5 range increment calculations.
Mirrors the logic in RangeCalculator.cs to validate the thrown weapon fix.
"""

FEET_PER_HEX = 5
MAX_INCREMENTS_PROJECTILE = 10
MAX_INCREMENTS_THROWN = 5
PENALTY_PER_INCREMENT = -2

def get_max_increments(is_thrown):
    return MAX_INCREMENTS_THROWN if is_thrown else MAX_INCREMENTS_PROJECTILE

def get_range_increment(distance_feet, range_increment_feet, is_thrown=False):
    if range_increment_feet <= 0 or distance_feet <= 0:
        return 0
    increment = (distance_feet + range_increment_feet - 1) // range_increment_feet
    max_inc = get_max_increments(is_thrown)
    if increment > max_inc:
        return 0
    return increment

def get_range_penalty(distance_feet, range_increment_feet, is_thrown=False):
    inc = get_range_increment(distance_feet, range_increment_feet, is_thrown)
    if inc == 0:
        return None  # out of range
    return (inc - 1) * PENALTY_PER_INCREMENT

def is_within_max_range(distance_feet, range_increment_feet, is_thrown=False):
    if range_increment_feet <= 0:
        return False
    max_inc = get_max_increments(is_thrown)
    return distance_feet <= range_increment_feet * max_inc

def get_max_range_feet(range_increment_feet, is_thrown=False):
    return range_increment_feet * get_max_increments(is_thrown)

def get_range_zone(hex_distance, range_increment_feet, is_thrown=False):
    if range_increment_feet <= 0:
        return 0
    dist_feet = hex_distance * FEET_PER_HEX
    increment = get_range_increment(dist_feet, range_increment_feet, is_thrown)
    if increment == 0:
        return 0
    if increment == 1:
        return 1
    if is_thrown:
        if increment <= 3:
            return 2
        return 3
    else:
        if increment <= 5:
            return 2
        return 3

def get_range_info(hex_distance, range_increment_feet, is_thrown=False):
    dist_feet = hex_distance * FEET_PER_HEX
    max_range = get_max_range_feet(range_increment_feet, is_thrown)
    inc = get_range_increment(dist_feet, range_increment_feet, is_thrown)
    is_in_range = inc > 0
    penalty = (inc - 1) * PENALTY_PER_INCREMENT if is_in_range else 0
    return {
        'hex_distance': hex_distance,
        'distance_feet': dist_feet,
        'increment': inc,
        'penalty': penalty,
        'is_in_range': is_in_range,
        'max_range_feet': max_range,
        'is_thrown': is_thrown
    }

passed = 0
failed = 0

def assert_test(name, hex_dist, range_inc, is_thrown, exp_inc, exp_penalty, exp_in_range):
    global passed, failed
    info = get_range_info(hex_dist, range_inc, is_thrown)
    ok = (info['increment'] == exp_inc and info['penalty'] == exp_penalty and info['is_in_range'] == exp_in_range)
    if ok:
        passed += 1
        print(f"  PASS: {name} - inc={info['increment']}, penalty={info['penalty']}")
    else:
        failed += 1
        print(f"  FAIL: {name} - got inc={info['increment']} (exp {exp_inc}), penalty={info['penalty']} (exp {exp_penalty}), inRange={info['is_in_range']} (exp {exp_in_range})")

def assert_out_of_range(name, hex_dist, range_inc, is_thrown):
    global passed, failed
    info = get_range_info(hex_dist, range_inc, is_thrown)
    if not info['is_in_range'] and info['increment'] == 0:
        passed += 1
        print(f"  PASS: {name} - out of range as expected")
    else:
        failed += 1
        print(f"  FAIL: {name} - expected out of range but got inc={info['increment']}, inRange={info['is_in_range']}")

print("===== PROJECTILE WEAPON TESTS (10 increment max) =====")
assert_test("Shortbow 50ft", 10, 60, False, 1, 0, True)
assert_test("Shortbow 90ft", 18, 60, False, 2, -2, True)
assert_test("Shortbow 150ft", 30, 60, False, 3, -4, True)
assert_out_of_range("Shortbow 650ft", 130, 60, False)
assert_test("Longbow 200ft", 40, 100, False, 2, -2, True)
assert_test("Longbow 250ft", 50, 100, False, 3, -4, True)
assert_test("LtCrossbow 80ft", 16, 80, False, 1, 0, True)
assert_test("Sling 500ft", 100, 50, False, 10, -18, True)
assert_out_of_range("Sling 505ft", 101, 50, False)
assert_test("Longbow 1000ft (10th inc)", 200, 100, False, 10, -18, True)
assert_out_of_range("Longbow 1005ft", 201, 100, False)

print("\n===== THROWN WEAPON TESTS (5 increment max per D&D 3.5) =====")
assert_test("Javelin 30ft (1st inc, thrown)", 6, 30, True, 1, 0, True)
assert_test("Javelin 35ft (2nd inc, thrown)", 7, 30, True, 2, -2, True)
assert_test("Javelin 150ft (5th inc, thrown)", 30, 30, True, 5, -8, True)
assert_out_of_range("Javelin 160ft (6th inc, thrown)", 32, 30, True)
assert_test("Dagger 10ft (1st inc, thrown)", 2, 10, True, 1, 0, True)
assert_test("Dagger 50ft (5th inc, thrown)", 10, 10, True, 5, -8, True)
assert_out_of_range("Dagger 55ft (6th inc, thrown)", 11, 10, True)
assert_test("Dart 100ft (5th inc, thrown)", 20, 20, True, 5, -8, True)
assert_out_of_range("Dart 105ft (6th inc, thrown)", 21, 20, True)
assert_test("Handaxe 50ft (5th inc, thrown)", 10, 10, True, 5, -8, True)
assert_test("Shortspear 100ft (5th inc, thrown)", 20, 20, True, 5, -8, True)
assert_test("Trident 50ft (5th inc, thrown)", 10, 10, True, 5, -8, True)

print("\n===== MELEE AND UTILITY TESTS =====")
# Melee test
info = get_range_info(1, 0)
if info['increment'] == 0:
    passed += 1
    print("  PASS: Melee at 1 hex")
else:
    failed += 1
    print(f"  FAIL: Melee at 1 hex")

# Range zones for projectile weapons
zone_pass = True
z1 = get_range_zone(10, 60, False)
z2 = get_range_zone(18, 60, False)
z3 = get_range_zone(100, 60, False)
z0 = get_range_zone(130, 60, False)
if z1 != 1: zone_pass = False; print(f"  FAIL: Projectile zone z1={z1}, expected 1")
if z2 != 2: zone_pass = False; print(f"  FAIL: Projectile zone z2={z2}, expected 2")
if z3 != 3: zone_pass = False; print(f"  FAIL: Projectile zone z3={z3}, expected 3")
if z0 != 0: zone_pass = False; print(f"  FAIL: Projectile zone z0={z0}, expected 0")
if zone_pass: passed += 1; print("  PASS: Projectile range zones")
else: failed += 1

# Range zones for thrown weapons
zone_pass = True
z1 = get_range_zone(5, 30, True)
z2 = get_range_zone(10, 30, True)
z3 = get_range_zone(25, 30, True)
z0 = get_range_zone(35, 30, True)
if z1 != 1: zone_pass = False; print(f"  FAIL: Thrown zone z1={z1}, expected 1")
if z2 != 2: zone_pass = False; print(f"  FAIL: Thrown zone z2={z2}, expected 2")
if z3 != 3: zone_pass = False; print(f"  FAIL: Thrown zone z3={z3}, expected 3")
if z0 != 0: zone_pass = False; print(f"  FAIL: Thrown zone z0={z0}, expected 0")
if zone_pass: passed += 1; print("  PASS: Thrown range zones")
else: failed += 1

# IsWithinMaxRange tests
range_pass = True
if not is_within_max_range(150, 30, True): range_pass = False; print("  FAIL: Javelin in range at 150 ft")
if is_within_max_range(151, 30, True): range_pass = False; print("  FAIL: Javelin out of range at 151 ft")
if not is_within_max_range(1000, 100, False): range_pass = False; print("  FAIL: Longbow in range at 1000 ft")
if is_within_max_range(1001, 100, False): range_pass = False; print("  FAIL: Longbow out of range at 1001 ft")
if range_pass: passed += 1; print("  PASS: IsWithinMaxRange thrown vs projectile")
else: failed += 1

# GetMaxRangeFeet tests
max_pass = True
if get_max_range_feet(30, True) != 150: max_pass = False; print("  FAIL: Javelin max should be 150")
if get_max_range_feet(30, False) != 300: max_pass = False; print("  FAIL: Projectile 30ft max should be 300")
if get_max_range_feet(100, False) != 1000: max_pass = False; print("  FAIL: Longbow max should be 1000")
if get_max_range_feet(10, True) != 50: max_pass = False; print("  FAIL: Dagger max should be 50")
if max_pass: passed += 1; print("  PASS: GetMaxRangeFeet thrown vs projectile")
else: failed += 1

print(f"\n{'='*50}")
print(f"RESULTS: {passed} passed, {failed} failed")
if failed == 0:
    print("ALL TESTS PASSED!")
else:
    print("SOME TESTS FAILED!")
    exit(1)
