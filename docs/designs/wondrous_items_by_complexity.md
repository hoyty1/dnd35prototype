# D&D 3.5e Wondrous Items — Organized by Complexity Tier

> **Source:** Dungeon Master's Guide 3.5e, Pages 246–265  
> **Companion to:** `wondrous_items_implementation_plan.md`  
> **Date:** May 2026

---

## COMPLEXITY TIER SUMMARY

| Tier | Rating | Description | Item Count | Est. Time | Dependencies |
|------|--------|-------------|-----------|-----------|-------------|
| 1 | ⭐ | Simple Passive Bonuses | ~45 | 1–2 weeks | Equipment slot system |
| 2 | ⭐⭐ | Active Single Ability | ~30 | 2–3 weeks | Activation framework, daily use tracker |
| 3 | ⭐⭐⭐ | Storage & Containers | ~15 | 2 weeks | Container system, inventory management |
| 4 | ⭐⭐⭐⭐ | Combat & Summoning | ~25 | 3–4 weeks | AoE mechanics, creature system, random tables |
| 5 | ⭐⭐⭐⭐⭐ | Complex Multi-Ability | ~15 | 3–4 weeks | Multiple subsystems, patch tracking |
| **Total** | | | **~130** | **11–15 weeks** | |

---

## TIER 1: SIMPLE PASSIVE BONUSES (⭐)

**Defining Characteristics:**
- Static bonus applied when item is equipped
- No activation action required
- Bonus removed when item is unequipped
- No charges, uses/day, or duration tracking
- No special mechanics beyond bonus application

**Implementation Pattern:**
```python
class SimplePassiveWondrousItem(WondrousItem):
    bonus_type: BonusType      # enhancement, competence, resistance, luck, insight
    bonus_target: BonusTarget  # ability_score, saving_throw, skill, ac, attack, damage
    bonus_value: int
    
    def on_equip(self, character):
        character.add_bonus(self.bonus_target, self.bonus_type, self.bonus_value, source=self)
    
    def on_unequip(self, character):
        character.remove_bonus(source=self)
```

**Testing Requirements:**
- ✅ Bonus correctly applied on equip
- ✅ Bonus correctly removed on unequip
- ✅ Same-type bonuses don't stack (highest wins)
- ✅ Different-type bonuses stack correctly
- ✅ Dependent values recalculate (e.g., ability modifier → skills, saves, AC)

---

### Ability Score Enhancement Items (18 items)

| Item | Slot | Ability | Bonus | Price |
|------|------|---------|-------|-------|
| Gauntlets of Ogre Power | Hands | Str | +2 enhancement | 4,000 gp |
| Gloves of Dexterity +2 | Hands | Dex | +2 enhancement | 4,000 gp |
| Amulet of Health +2 | Throat | Con | +2 enhancement | 4,000 gp |
| Headband of Intellect +2 | Head | Int | +2 enhancement | 4,000 gp |
| Periapt of Wisdom +2 | Throat | Wis | +2 enhancement | 4,000 gp |
| Cloak of Charisma +2 | Shoulders | Cha | +2 enhancement | 4,000 gp |
| Belt of Giant Strength +4 | Waist | Str | +4 enhancement | 16,000 gp |
| Gloves of Dexterity +4 | Hands | Dex | +4 enhancement | 16,000 gp |
| Amulet of Health +4 | Throat | Con | +4 enhancement | 16,000 gp |
| Headband of Intellect +4 | Head | Int | +4 enhancement | 16,000 gp |
| Periapt of Wisdom +4 | Throat | Wis | +4 enhancement | 16,000 gp |
| Cloak of Charisma +4 | Shoulders | Cha | +4 enhancement | 16,000 gp |
| Belt of Giant Strength +6 | Waist | Str | +6 enhancement | 36,000 gp |
| Gloves of Dexterity +6 | Hands | Dex | +6 enhancement | 36,000 gp |
| Amulet of Health +6 | Throat | Con | +6 enhancement | 36,000 gp |
| Headband of Intellect +6 | Head | Int | +6 enhancement | 36,000 gp |
| Periapt of Wisdom +6 | Throat | Wis | +6 enhancement | 36,000 gp |
| Cloak of Charisma +6 | Shoulders | Cha | +6 enhancement | 36,000 gp |

### AC Enhancement Items (14 items)

| Item | Slot | AC Type | Bonus | Price |
|------|------|---------|-------|-------|
| Bracers of Armor +1 | Arms | Armor | +1 | 1,000 gp |
| Bracers of Armor +2 | Arms | Armor | +2 | 4,000 gp |
| Bracers of Armor +3 | Arms | Armor | +3 | 9,000 gp |
| Bracers of Armor +4 | Arms | Armor | +4 | 16,000 gp |
| Bracers of Armor +5 | Arms | Armor | +5 | 25,000 gp |
| Bracers of Armor +6 | Arms | Armor | +6 | 36,000 gp |
| Bracers of Armor +7 | Arms | Armor | +7 | 49,000 gp |
| Bracers of Armor +8 | Arms | Armor | +8 | 64,000 gp |
| Amulet of Natural Armor +1 | Throat | Natural (enh) | +1 | 2,000 gp |
| Amulet of Natural Armor +2 | Throat | Natural (enh) | +2 | 8,000 gp |
| Amulet of Natural Armor +3 | Throat | Natural (enh) | +3 | 18,000 gp |
| Amulet of Natural Armor +4 | Throat | Natural (enh) | +4 | 32,000 gp |
| Amulet of Natural Armor +5 | Throat | Natural (enh) | +5 | 50,000 gp |
| Dusty Rose Prism (Ioun) | Slotless | Insight | +1 | 5,000 gp |

### Saving Throw Enhancement Items (5 items)

| Item | Slot | Saves | Bonus | Price |
|------|------|-------|-------|-------|
| Cloak of Resistance +1 | Shoulders | All | +1 resistance | 1,000 gp |
| Cloak of Resistance +2 | Shoulders | All | +2 resistance | 4,000 gp |
| Cloak of Resistance +3 | Shoulders | All | +3 resistance | 9,000 gp |
| Cloak of Resistance +4 | Shoulders | All | +4 resistance | 16,000 gp |
| Cloak of Resistance +5 | Shoulders | All | +5 resistance | 25,000 gp |

### Skill Bonus Items (9 items)

| Item | Slot | Skill(s) | Bonus | Price |
|------|------|----------|-------|-------|
| Goggles of Minute Seeing | Face | Search | +5 competence | 1,250 gp |
| Boots of Elvenkind | Feet | Move Silently | +5 competence | 2,500 gp |
| Cloak of Elvenkind | Shoulders | Hide | +5 competence | 2,500 gp |
| Eyes of the Eagle | Face | Spot | +5 competence | 2,500 gp |
| Lens of Detection | Face | Search, Survival (tracking) | +5 competence | 3,500 gp |
| Circlet of Persuasion | Head | All Cha-based checks | +3 competence | 4,500 gp |
| Vest of Escape | Torso | Escape Artist (+6), Open Lock (+4) | competence | 5,200 gp |
| Gloves of Swimming and Climbing | Hands | Swim, Climb | +5 competence | 6,250 gp |

### Other Simple Passive Items (5 items)

| Item | Slot | Effect | Price |
|------|------|--------|-------|
| Phylactery of Faithfulness | Throat | Alignment warning for clerics | 1,000 gp |
| Horseshoes of Speed | Slotless | +30 ft horse speed | 3,000 gp |
| Boots of Striding and Springing | Feet | +10 ft speed, +5 Jump | 5,500 gp |
| Periapt of Health | Throat | Disease immunity | 7,500 gp |
| Amulet of Mighty Fists +1/+2/+3 | Throat | Enhancement to unarmed/natural attacks | 6,000–54,000 gp |

**Tier 1 Total: ~45 items**

---

## TIER 2: ACTIVE SINGLE ABILITY (⭐⭐)

**Defining Characteristics:**
- One primary activated ability (or one at-will spell effect)
- May have uses/day or duration tracking
- Single spell effect replicated
- Activation requires an action (standard, move, free, or command word)

**Implementation Pattern:**
```python
class ActiveSingleAbilityItem(WondrousItem):
    spell_effect: SpellEffect
    activation_type: ActivationType  # command_word, standard_action, free_action
    uses_per_day: int               # -1 = at will
    duration_per_use: Duration
    
    def activate(self, character) -> bool:
        if not self.daily_tracker.can_use():
            return False
        self.daily_tracker.use()
        character.apply_effect(self.spell_effect, self.duration_per_use)
        return True
    
    def on_new_day(self):
        self.daily_tracker.reset()
```

**Testing Requirements:**
- ✅ Activation consumes correct action type
- ✅ Uses/day tracked and limited correctly
- ✅ Duration tracking accurate
- ✅ Effect correctly applied and removed
- ✅ Daily reset works properly

---

### At-Will Items (5 items)

| Item | Slot | Spell Effect | Activation | Price |
|------|------|-------------|-----------|-------|
| Hand of the Mage | Slotless | Mage hand | Standard | 900 gp |
| Hat of Disguise | Head | Disguise self | Standard | 1,800 gp |
| Boots of Levitation | Feet | Levitate (self) | Command word | 7,500 gp |
| Slippers of Spider Climbing | Feet | Spider climb | Continuous | 4,800 gp |
| Robe of Blending | Torso | Disguise self | Standard | 8,400 gp |

### X/Day Items (10 items)

| Item | Slot | Effect | Uses | Activation | Price |
|------|------|--------|------|-----------|-------|
| Horn of Goodness/Evil | Slotless | Magic circle vs evil/good, 1 hr | 1/day | Standard | 6,500 gp |
| Eyes of Charming | Face | Charm person DC 16 | 3/day | Standard | 56,000 gp |
| Winged Boots | Feet | Fly 60 ft good, 5 min | 3/day | Command word | 16,000 gp |
| Boots of Teleportation | Feet | Teleport (self + 50 lbs) | 3/day | Command word | 49,000 gp |
| Helm of Telepathy — Suggest | Head | Suggestion DC 14 | 1/day | Standard | (part of 27,000 gp) |
| Cloak of Arachnida — Web | Shoulders | Web DC 14 | 1/day | Standard | (part of 14,000 gp) |
| Wind Fan | Slotless | Gust of wind | 1/day | Standard | 5,500 gp |

### Duration-Tracked Items (5 items)

| Item | Slot | Effect | Total Duration | Tracking | Price |
|------|------|--------|---------------|---------|-------|
| Boots of Speed | Feet | Haste | 10 rounds/day | Round-by-round | 12,000 gp |
| Cloak of Etherealness | Shoulders | Ethereal jaunt | 10 min/day | Minute-by-minute | 55,000 gp |
| Wings of Flying | Shoulders | Fly 60 ft good | At will (unlimited) | N/A | 54,000 gp |
| Robe of Scintillating Colors | Torso | Hypnotic pattern DC 16, 30 ft | 10 rounds/day | Round-by-round | 27,000 gp |

### Continuous Special Effect Items (10 items)

| Item | Slot | Effect | Price |
|------|------|--------|-------|
| Goggles of Night | Face | Darkvision 60 ft | 2,500 gp |
| Helm of Comprehend Languages | Head | Comprehend languages + read magic | 5,200 gp |
| Helm of Underwater Action | Head | Water breathing, swim 30 ft, clear vision underwater | 24,000 gp |
| Necklace of Adaptation | Throat | Breathe in any environment | 9,000 gp |
| Cloak of Displacement, Minor | Shoulders | 20% miss chance | 24,000 gp |
| Cloak of Displacement, Major | Shoulders | 50% miss chance | 50,000 gp |
| Brooch of Shielding | Throat | Absorbs magic missiles (101 HP) | 1,500 gp |
| Periapt of Proof Against Poison | Throat | Poison immunity | 27,000 gp |
| Amulet of Proof Against Detection | Throat | Divination immunity | 35,000 gp |
| Boots of the Winterlands | Feet | Endure cold, snow mobility | 2,500 gp |

### Consumable Single-Use Items (20+ items)

| Category | Items | Count | Price Range |
|----------|-------|-------|-------------|
| Dusts | Tracelessness, Dryness, Illusion, Appearance, Disappearance | 5 | 250–3,500 gp |
| Elixirs | Love, Hiding, Swimming, Tumbling, Vision, Truth, Fire Breath | 7 | 150–1,100 gp |
| Feather Tokens | Anchor, Fan, Bird, Swan Boat, Tree, Whip | 6 | 50–500 gp |
| Misc Consumables | Silversheen, Salve of Slipperiness, Sovereign Glue, Universal Solvent, Restorative Ointment, Stone Salve, Incense of Meditation, Unguent of Timelessness | 8 | 50–4,900 gp |

**Tier 2 Total: ~30 items (not counting consumable variants)**

---

## TIER 3: STORAGE & CONTAINERS (⭐⭐⭐)

**Defining Characteristics:**
- Interact with inventory/weight system
- Extradimensional storage spaces
- Weight reduction mechanics
- Dangerous interaction rules (bag-in-bag → Astral rift)
- Retrieval action economy differences

**Implementation Pattern:**
```python
class MagicContainer(WondrousItem):
    weight_limit: float
    volume_limit: float
    carried_weight: float  # actual weight when carried
    is_extradimensional: bool
    contents: List[Item]
    retrieval_action: ActionType  # standard or move
    
    def add_item(self, item):
        # Check weight and volume limits
        # Check extradimensional nesting
        # Add to contents
    
    def retrieve_item(self, item):
        # Return item and action cost
    
    def get_total_contents_weight(self):
        return sum(item.weight for item in self.contents)
```

**Testing Requirements:**
- ✅ Weight limit enforcement
- ✅ Volume limit enforcement
- ✅ Carried weight is correct (not contents weight)
- ✅ Extradimensional nesting detection
- ✅ Astral rift consequence on nesting
- ✅ Retrieval action economy correct
- ✅ Item tracking within container

---

### Container Items (7 items)

| Item | Weight Limit | Volume | Carried Weight | Retrieval | Price |
|------|-------------|--------|---------------|-----------|-------|
| Efficient Quiver | 60 arrows + 18 javelins + 6 long | Compartmented | 2 lbs | Move | 1,800 gp |
| Handy Haversack | 120 lbs total | 2+1+1 cu ft | 5 lbs | Move (desired item on top) | 2,000 gp |
| Bag of Holding I | 250 lbs | 30 cu ft | 15 lbs | Standard | 2,500 gp |
| Bag of Holding II | 500 lbs | 70 cu ft | 25 lbs | Standard | 5,000 gp |
| Bag of Holding III | 1,000 lbs | 150 cu ft | 35 lbs | Standard | 7,400 gp |
| Bag of Holding IV | 1,500 lbs | 250 cu ft | 60 lbs | Standard | 10,000 gp |
| Portable Hole | 10,000+ lbs | 283 cu ft (10×10×10) | Negligible | Standard | 20,000 gp |

### Utility Items Requiring Special Systems (8 items)

| Item | System Needed | Effect | Price |
|------|--------------|--------|-------|
| Folding Boat | Transform/state | 12-ft rowboat ↔ 24-ft ship ↔ box | 7,200 gp |
| Decanter of Endless Water | Resource generation | Stream/fountain/geyser modes | 9,000 gp |
| Eversmoking Bottle | Area effect toggle | 50-ft obscuring mist on/off | 5,400 gp |
| Bottle of Air | Resource generation | Unlimited air supply underwater | 7,250 gp |
| Sustaining Spoon | Resource generation | 1 creature's sustenance/day | 5,400 gp |
| Rope of Climbing | Animated object | Self-animating 60-ft rope | 3,000 gp |
| Horseshoes of a Zephyr | Mount movement | Walk on air/water, no tracks | 6,000 gp |
| Candle of Truth | Area effect | Zone of truth DC 13, 5-ft radius, 1 hr | 2,500 gp |

**Tier 3 Total: ~15 items**

---

## TIER 4: COMBAT & SUMMONING (⭐⭐⭐⭐)

**Defining Characteristics:**
- Deal damage or create combat effects
- Summon creatures requiring stat blocks
- Area of effect targeting
- Grapple/binding mechanics
- Duration tracking for summoned creatures
- Random table rolls

**Implementation Pattern:**
```python
class CombatWondrousItem(WondrousItem):
    attack_type: AttackType       # ranged_touch, cone, burst, etc.
    damage: DiceRoll              # e.g., 5d6
    damage_type: DamageType       # fire, sonic, force, etc.
    save_dc: int
    save_type: SaveType           # reflex, fort, will
    range: int                    # feet
    area: AreaType                # radius, cone, single target
    
class SummoningItem(WondrousItem):
    creature_table: List[CreatureEntry]
    max_duration: Duration
    usage_period: TimePeriod
    summoning_method: str         # random, specific, choice
```

**Testing Requirements:**
- ✅ Damage calculation correct
- ✅ AoE targeting correct
- ✅ Save DC and consequences correct
- ✅ Summoned creature stat blocks complete
- ✅ Duration tracking per usage period
- ✅ Random table distribution correct
- ✅ Charge/use depletion correct

---

### Necklace of Fireballs (7 types)

| Type | Beads | Damage Dice | Price |
|------|-------|-------------|-------|
| I | 1 bead | 5d6 | 1,650 gp |
| II | 2 beads | 5d6, 3d6 | 2,700 gp |
| III | 3 beads | 5d6, 3d6, 3d6 | 4,350 gp |
| IV | 3 beads | 7d6, 5d6, 3d6 | 5,400 gp |
| V | 4 beads | 7d6, 5d6, 3d6, 3d6 | 5,850 gp |
| VI | 4 beads | 9d6, 5d6, 5d6, 3d6 | 8,100 gp |
| VII | 5 beads | 9d6, 7d6, 5d6, 3d6, 3d6 | 8,700 gp |

**Special:** If wearer takes fire damage, remaining beads explode simultaneously on wearer (Reflex DC 14 each).

### Combat Miscellaneous (5 items)

| Item | Attack | Damage/Effect | Price |
|------|--------|--------------|-------|
| Bead of Force | Thrown (60 ft) | 5d6 force + resilient sphere (Ref DC 16) | 3,000 gp |
| Horn of Blasting | 100-ft cone | 5d6 sonic, Fort DC 16 or deaf 2d6 rds | 20,000 gp |
| Iron Bands of Binding | Ranged touch (60 ft) | Bind Large or smaller (Str DC 30) | 26,000 gp |
| Bracers of Archery, Lesser | Passive | +1 competence attack with bows | 5,000 gp |
| Bracers of Archery, Greater | Passive | +2 attack, +1 damage with bows | 25,000 gp |
| Gauntlet of Rust | Touch | Rusting grasp (destroy metal) | 11,500 gp |
| Pipes of Pain | 30-ft area | 2d4 damage + cause fear DC 14 | 12,000 gp |

### Bags of Tricks (3 types)

| Bag | Animals Available | Price |
|-----|------------------|-------|
| Gray | Bat, Rat, Cat, Weasel, Riding Dog | 900 gp |
| Rust | Wolverine, Wolf, Boar, Panther, Giant Wasp | 3,000 gp |
| Tan | Brown Bear, Lion, Heavy Horse, Tiger, Rhinoceros | 6,900 gp |

**Mechanic:** Pull fuzzy ball → throw → becomes random animal for 10 min. Up to 10 uses, then empties for 1 week.

### Elemental Gems (4 types)

| Gem | Creature Summoned | Duration | Price |
|-----|------------------|----------|-------|
| Air | Large Air Elemental | Until destroyed or dismissed | 2,250 gp |
| Earth | Large Earth Elemental | Until destroyed or dismissed | 2,250 gp |
| Fire | Large Fire Elemental | Until destroyed or dismissed | 2,250 gp |
| Water | Large Water Elemental | Until destroyed or dismissed | 2,250 gp |

**Single-use consumable.** Creature serves for 1 encounter then returns.

### Figurines of Wondrous Power (9 types)

| Figurine | Creature | Max Duration | Usage Period | Price |
|----------|----------|-------------|-------------|-------|
| Silver Raven | Raven (animal messenger) | 24 hours | Per use | 3,800 gp |
| Serpentine Owl | Giant owl / tiny owl | 8 hrs (giant) / unlimited (tiny) | Per day | 9,100 gp |
| Bronze Griffon | Griffon | 6 hours | Per week | 10,000 gp |
| Ebony Fly | Giant fly (mount) | 12 hours | Per week | 10,000 gp |
| Onyx Dog | Riding dog (enhanced) | 6 hours | Per week | 15,500 gp |
| Golden Lions | 2 lions | 1 hour | Per day | 16,500 gp |
| Marble Elephant | Elephant | 24 hours | Per month | 17,000 gp |
| Ivory Goats | 3 goats (travel/travail/terror) | Varies | Varies | 21,000 gp |
| Obsidian Steed | Nightmare / heavy warhorse | 24 hours | Per week | 28,500 gp |

### Horns of Valhalla (3 in scope)

| Horn | Warriors | Level | Prerequisite | Price |
|------|---------|-------|-------------|-------|
| Brass | 2d4+2 | 3rd | Martial weapon proficiency (all) | 34,000 gp |
| Bronze | 2d4+2 | 4th | Medium armor proficiency | 40,000 gp |
| Silver | 2d4+2 | 2nd | None | 50,000 gp |

**Duration:** 1 hour. Summoned barbarians fight and follow commands. If wrong person blows horn, summoned barbarians attack the blower.

### Other Summoning (2 items)

| Item | Effect | Price |
|------|--------|-------|
| Pipes of the Sewers | Summon/control rat swarm | 1,150 gp |
| Pipes of Haunting | Fear effect 30-ft radius, DC 13 Will | 6,500 gp |

**Tier 4 Total: ~25 items (not counting sub-variants)**

---

## TIER 5: COMPLEX MULTI-ABILITY (⭐⭐⭐⭐⭐)

**Defining Characteristics:**
- Multiple distinct abilities on a single item
- Require new subsystems or complex state management
- Conditional abilities (work only in certain circumstances)
- Patch/component tracking systems
- Multiple interacting effects

**Implementation Pattern:**
```python
class ComplexWondrousItem(WondrousItem):
    abilities: List[ItemAbility]    # Multiple distinct abilities
    conditions: List[Condition]     # When abilities are available
    components: List[Component]     # Trackable sub-parts (patches, gems, etc.)
    
    class ItemAbility:
        name: str
        activation: ActivationType
        effect: Effect
        uses: UsageTracker
        condition: Optional[Condition]
    
    class Component:
        name: str
        is_consumed: bool
        is_available: bool
        effect: Effect
```

**Testing Requirements:**
- ✅ Each ability works independently
- ✅ Abilities interact correctly when combined
- ✅ Conditional abilities only activate under correct conditions
- ✅ Component tracking (patches used/remaining)
- ✅ Complex state transitions correct
- ✅ Edge cases handled (all patches used, etc.)

---

### Ioun Stones System (16 types)

**Why Tier 5:** Requires entirely new slotless orbiting subsystem, 16 different effect types, physical targeting (AC 24), burn-out tracking for absorption stones, spell storage for Vibrant Purple Prism.

| Stone | Effect | Subsystem Needed | Price |
|-------|--------|-----------------|-------|
| Clear Spindle | Sustenance (no food/water) | Sustenance tracking | 4,000 gp |
| Dusty Rose Prism | +1 insight AC | Insight AC bonus | 5,000 gp |
| Deep Red Sphere | +2 Dex | Ability enhancement | 8,000 gp |
| Incandescent Blue Sphere | +2 Wis | Ability enhancement | 8,000 gp |
| Pale Blue Rhomboid | +2 Str | Ability enhancement | 8,000 gp |
| Pink Rhomboid | +2 Con | Ability enhancement | 8,000 gp |
| Pink and Green Sphere | +2 Cha | Ability enhancement | 8,000 gp |
| Scarlet and Blue Sphere | +2 Int | Ability enhancement | 8,000 gp |
| Dark Blue Rhomboid | Alertness feat | Feat granting | 10,000 gp |
| Iridescent Spindle | No air needed | Sustenance tracking | 18,000 gp |
| Pale Lavender Ellipsoid | Absorb ≤4th spells (20 levels) | Spell absorption + burnout | 20,000 gp |
| Pearly White Spindle | Regenerate 1 HP/10 min | Healing ticker | 20,000 gp |
| Orange Prism | +1 caster level | Caster level modifier | 30,000 gp |
| Pale Green Prism | +1 all attacks/saves/skills/ability checks | Universal competence bonus | 30,000 gp |
| Vibrant Purple Prism | Store 3 spell levels | Spell storing (like ring) | 36,000 gp |
| Lavender and Green Ellipsoid | Absorb ≤8th spells (50 levels) | Spell absorption + burnout | 40,000 gp |

### Multi-Ability Cloaks (3 items)

| Item | Abilities | Price |
|------|----------|-------|
| Cloak of Arachnida | ① Spider climb continuous ② Web immunity ③ Web 1/day DC 14 | 14,000 gp |
| Cloak of the Bat | ① +5 Hide ② Fly in dim light ③ Polymorph bat outdoors at night ④ Hang like bat | 26,000 gp |
| Scarab of Protection | ① +3 resistance saves ② Absorb 12 death/energy drain effects | 38,000 gp |

### Multi-Ability Robes (5 items)

| Item | Abilities | Price |
|------|----------|-------|
| Robe of Bones | 12 undead patches (each becomes specific undead when detached) | 2,400 gp |
| Robe of Useful Items | Default patches + 4d4 random patches (each becomes real item) | 7,000 gp |
| Robe of Scintillating Colors | AoE daze 30-ft, DC 16 Will, 10 rounds/day | 27,000 gp |
| Robe of Stars | ① +1 luck saves ② 6 star missiles (5d4 force each) ③ Astral projection 1/day | 58,000 gp |
| Robe of the Archmagi | ① +5 armor AC ② SR 18 ③ +4 resistance saves ④ 0% arcane failure ⑤ Alignment-restricted | 75,000 gp |

### Multi-Ability Helms (1 item)

| Item | Abilities | Price |
|------|----------|-------|
| Helm of Telepathy | ① Detect thoughts at will DC 13 ② Suggest 1/day DC 14 ③ Telepathy 60 ft | 27,000 gp |

### Complex Slotless Items (4 items)

| Item | Abilities/Complexity | Price |
|------|---------------------|-------|
| Cube of Force | 6 face modes, 36 charges, recharge 1d6/day, wall of force mechanics | 62,000 gp |
| Rope of Entanglement | Animated grapple (+15), entangle on command | 21,000 gp |
| Cube of Frost Resistance | Cold absorption, 10-ft radius protection | 27,000 gp |
| Monk's Belt | Grants monk abilities to non-monks, +5 effective monk levels to monks | 13,000 gp |

**Tier 5 Total: ~15 items (base) + 16 Ioun Stone subtypes**

---

## COMPLEXITY COMPARISON MATRIX

| Feature | Tier 1 | Tier 2 | Tier 3 | Tier 4 | Tier 5 |
|---------|--------|--------|--------|--------|--------|
| Passive bonus | ✅ | ✅ | — | — | ✅ |
| Activation action | — | ✅ | ✅ | ✅ | ✅ |
| Uses/day tracking | — | ✅ | — | ✅ | ✅ |
| Duration tracking | — | ✅ | — | ✅ | ✅ |
| Charge system | — | — | — | ✅ | ✅ |
| Inventory management | — | — | ✅ | — | — |
| Creature summoning | — | — | — | ✅ | — |
| AoE mechanics | — | — | — | ✅ | ✅ |
| Random tables | — | — | — | ✅ | — |
| Multiple abilities | — | — | — | — | ✅ |
| Conditional logic | — | — | — | — | ✅ |
| Component tracking | — | — | — | — | ✅ |
| New subsystem needed | — | — | ✅ | ✅ | ✅ |

---

## RECOMMENDED IMPLEMENTATION ORDER

```
Week 1-2:   Tier 1 (Simple Passive) ──────────────────── 45 items ✓
Week 3-5:   Tier 2 (Active Single) ───────────────────── 30 items ✓
Week 6-7:   Tier 3 (Storage & Containers) ─────────────── 15 items ✓
Week 8-11:  Tier 4 (Combat & Summoning) ──────────────── 25 items ✓
Week 12-15: Tier 5 (Complex Multi-Ability) ────────────── 15 items ✓
                                                    Total: ~130 items
```

Each tier builds on the infrastructure established by previous tiers:
- **Tier 1** establishes the bonus management system
- **Tier 2** adds activation and daily-use tracking
- **Tier 3** adds container/inventory management
- **Tier 4** adds combat integration and summoning
- **Tier 5** combines all systems for complex items

---

*Document created: May 2026*
