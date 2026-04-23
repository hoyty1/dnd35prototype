# D&D 3.5e AI Framework

## Overview
This folder contains a data-driven AI profile system for NPC behavior.

## Core Types
- `AIProfile` (`ScriptableObject`): base profile class.
- `AIBehaviorData`: shared enums/data (`CombatStyle`, `GrappleBehavior`, movement + maneuver preferences, tag priorities).

## Default Profiles
- **Berserk**: melee aggression, willing to eat AoOs, prioritizes wounded targets.
- **Ranged**: keep distance, avoid AoOs, prioritize unarmored/light targets.
- **Grappler**: initiate/maintain grapples, control-focused maneuvering.

## Custom AI
Create a new class inheriting `AIProfile` and override:
- `ScoreTarget(target, self)`
- `ShouldEscapeGrapple(self)`
- `ShouldInitiateGrapple(self, target)`
- `GetPreferredManeuver(self, target)`

See `Custom/CustomAIExample.cs` for a template.

## Assigning Profiles
Assign `CharacterController.aiProfile` on any NPC. During NPC turns, `AIService` reads this profile and adjusts:
- target scoring
- movement (including AoO avoidance preference)
- maneuver selection hints (trip/disarm/grapple)

## Tag Priority Examples
- `Race: Elf`
- `Armor: Light Armor`
- `Armor: Unarmored`
- `Wielding: Unarmed`
- `HP State: Staggered`
