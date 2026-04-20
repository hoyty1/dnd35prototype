# Aid Another Manual Test Scenarios

## 1) Aid Defense success and consume on enemy attack (hit)
- Aider selects Ally A -> Aid Defense -> Enemy X.
- Touch attack hits AC 10.
- Verify Ally A gets +2 AC vs Enemy X next attack.
- Enemy X attacks Ally A and hits.
- Verify +2 AC was applied and then removed.

## 2) Aid Defense success and consume on enemy attack (miss)
- Same setup as #1, but attack misses.
- Verify defense aid is still consumed after that one attack.

## 3) Aid Offense success and consume on ally attack (hit)
- Aider selects Ally A -> Aid Offense -> Enemy X.
- Touch attack hits AC 10.
- Ally A attacks Enemy X and hits.
- Verify +2 attack applied and then removed.

## 4) Aid Offense success and consume on ally attack (miss)
- Same setup as #3, but attack misses.
- Verify offense aid is still consumed after that one attack.

## 5) Aid Another failure on touch attack
- Aider performs Aid Another but touch attack total < 10.
- Verify no bonus is created.
- Verify standard action is consumed.

## 6) Defense bonus does not trigger on wrong attacker
- Aider grants Ally A defense vs Enemy X.
- Enemy Y attacks Ally A.
- Verify aid remains active (not consumed).

## 7) Offense bonus does not trigger on wrong target
- Aider grants Ally A offense vs Enemy X.
- Ally A attacks Enemy Y.
- Verify aid remains active (not consumed).

## 8) Expires at beneficiary next turn start (defense)
- Grant Ally A defense vs Enemy X.
- Ensure Enemy X does not attack Ally A before Ally A next turn.
- Verify bonus expires when Ally A turn starts.

## 9) Expires at beneficiary next turn start (offense)
- Grant Ally A offense vs Enemy X.
- Ensure Ally A does not attack Enemy X before Ally A next turn.
- Verify bonus expires when Ally A turn starts.

## 10) Stacking aid from multiple aiders (offense)
- Two aiders both grant Ally A offense vs Enemy X.
- Verify next Ally A attack vs Enemy X gets +4 total and both entries are consumed.

## 11) Stacking aid from multiple aiders (defense)
- Two aiders both grant Ally A defense vs Enemy X.
- Verify Enemy X next attack vs Ally A applies +4 AC and both entries are consumed.

## 12) UI flow + cancel paths
- Click Aid Another.
- Verify dialogs appear in order: Ally -> Aid Type -> Enemy.
- Cancel at each stage and verify return path works.
