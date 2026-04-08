# Hex RPG Prototype

A 2D sprite-based turn-based RPG prototype built with Unity.

## Features
- **20x20 Hexagonal Grid** - Pointy-top hex grid for tactical movement
- **Turn-Based System** - Alternating turns between PC (player) and NPC (enemy)
- **Movement System** - Click highlighted hexes to move during your turn
- **Menu-Based Combat** - Attack button with d20 roll vs Armor Class
- **Stats System** - HP and AC for both characters with UI display
- **Death System** - Sprite swaps to dead version when HP reaches 0

## Character Stats

### Hero (PC)
| Stat | Value |
|------|-------|
| HP | 30 |
| AC | 14 |
| Attack Bonus | +5 |
| Damage | 3-8 |
| Move Range | 4 hexes |
| Attack Range | 1 hex (melee) |

### Goblin (NPC)
| Stat | Value |
|------|-------|
| HP | 20 |
| AC | 12 |
| Attack Bonus | +3 |
| Damage | 2-6 |
| Move Range | 3 hexes |
| Attack Range | 1 hex (melee) |

## How to Play

1. **Open in Unity** (Unity 6.4 / 6000.4.0f1 or newer recommended)
2. **Open** `Assets/Scenes/MainScene.unity`
3. **Press Play**

### Gameplay Flow
1. **Your Turn - Movement**: Blue highlighted tiles show where you can move. Click one to move, or click your own tile to skip movement.
2. **Your Turn - Action**: After moving, the action panel appears:
   - **Attack**: Highlights enemies in range (red). Click an enemy to attack.
   - **End Turn**: Skip your action and end your turn.
3. **Enemy Turn**: The Goblin AI moves toward you and attacks if in melee range.
4. **Combat Resolution**: Attacks roll d20 + Attack Bonus vs target AC. Hit = deal damage. Miss = no effect.
5. **Victory/Defeat**: When either character reaches 0 HP, their sprite changes to the dead version and combat ends.

## Project Structure

```
Assets/
├── Scenes/
│   └── MainScene.unity          # Main game scene
├── Scripts/
│   ├── Grid/
│   │   ├── HexUtils.cs          # Hex math utilities
│   │   ├── HexCell.cs           # Individual hex cell component
│   │   └── HexGrid.cs           # Grid generation & management
│   ├── Character/
│   │   ├── CharacterStats.cs    # RPG stat block
│   │   └── CharacterController.cs # Character movement & actions
│   ├── Combat/
│   │   └── CombatResult.cs      # Attack result data
│   ├── UI/
│   │   └── CombatUI.cs          # UI management
│   └── Core/
│       ├── GameManager.cs       # Central game/turn manager
│       ├── SceneBootstrap.cs    # Runtime scene construction
│       └── SpriteLoader.cs      # Sprite loading utility
├── Resources/
│   └── Sprites/                 # Sprites for runtime loading
│       ├── pc_alive.png
│       ├── pc_dead.png
│       ├── npc_enemy_alive.png
│       └── npc_enemy_dead.png
└── Sprites/                     # Original sprite assets
    ├── pc_alive.png
    ├── pc_dead.png
    ├── npc_enemy_alive.png
    └── npc_enemy_dead.png
```

## Scene Setup

The scene uses a `SceneBootstrap` component that constructs all game objects at runtime:
- Hex grid (400 tiles)
- PC and NPC characters with sprites
- Full UI (stats panels, combat log, action buttons, turn indicator)
- Camera positioning

This approach ensures the game runs immediately without manual Editor setup.

## Technical Notes
- Hex coordinates use **axial (q, r)** system with **pointy-top** orientation
- Grid cells have `BoxCollider2D` for mouse click detection  
- NPC AI uses simple "move toward player, attack if in range" behavior
- All UI is created programmatically via `SceneBootstrap.cs`
- Sprites are loaded from `Resources/Sprites/` at runtime
