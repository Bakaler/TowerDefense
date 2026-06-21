# Tower Defense - Game Design Document

---

## Overview

A tower defense game built in Unity 2D. The player places towers along a path to stop waves of enemies from reaching the end. The core loop is built around strategic tower variety, passive income management, a balance system that punishes over-reliance on any single tower type, and an active ability bar that rewards player attention during waves.

All towers, units, abilities, and effects are fully data-driven via JSON. No prefabs. Players can create custom units and towers by editing definition files.

---

## Core Loop

1. A wave of enemies spawns and follows a spline-based path toward the exit.
2. The player spends gold to place towers before and between waves.
3. Towers autocast their basic attacks. Player activates cooldown abilities from the ability bar.
4. Kills generate bounty drops (gold or tech, randomly). Ability kills drop tech. Normal kills drop gold.
5. After each wave, Balance score is distributed into resource pools by damage type.
6. Players spend those pools on income tower upgrades, research unlocks, and tower leveling.

---

## Economy

### Gold

Gold is the primary resource used to place towers.

**Starting gold:** 150

**Sources:**
- Income towers (primary, player-activated)
- Bounty drops from normal kills (supplement, never sustaining)

**Tower costs:**

| Tower | Cost | Balance Type |
|---|---|---|
| Basic Tower | 3 | Physical |
| Income Tower | 5 | Elemental |
| Boomerang Tower | 5 | Physical |
| Chain Tower | 8 | Arcane |

### Income Towers

The income tower uses a PvZ-style orb collection mechanic. Orbs accumulate in four slots above the tower. The player clicks the tower body to collect all orbs at once.

**Orb spawn rate:** one orb every 8 seconds, maximum 4 orbs. Multiple income towers stagger their spawn timers automatically.

**Payout table:**

| Orbs held | Gold earned |
|---|---|
| 1 | 1 |
| 2 | 3 |
| 3 | 5 |
| 4 | 8 |

Collecting at full capacity rewards patience. Early collection is always a net loss per second. Elemental balance increases orb spawn rate, making income towers more productive without changing the patience decision.

### Bounty Drops

Each kill has a chance to drop a resource. Bounty is a supplement, never a primary income source. Drop rates are intentionally low.

**Drop types:**
- Normal kills (basic attack) drop gold
- Ability kills (any cooldown ability, whether player-activated or autocasting) drop tech

**Drop chance:** base rate is low. Physical balance increases drop chance slightly. Even at maximum Physical investment, bounty should feel like a bonus, not a plan.

**Drop size:** small. One or two gold or one tech point per drop. Value comes from frequency over a long wave.

---

## Damage and Resistance System

### Damage Types

There are six damage types split into two groups.

**Standard damage types** interact with the resistance and amplification system:

| Damage Type | Reduced By | Amplified By | Balance Pool |
|---|---|---|---|
| Elemental | Fortitude | Spirit | Income |
| Arcane | Resilience | Potential | Research |
| Physical | Armor | Vigor | Drop Chance |

**Bypass damage types** ignore standard resistances and can only be reduced by specific buffs:

| Damage Type | Notes |
|---|---|
| Poison | Bypass. Resistable only by dedicated buffs. |
| Pure | Bypass. Resistable only by dedicated buffs. |
| Piercing | Bypass. Resistable only by dedicated buffs. |

### Resistance Stats

**Fortitude** reduces Elemental damage taken.
**Resilience** reduces Arcane damage taken.
**Armor** reduces Physical damage taken.

### Amplification Stats

**Spirit** increases Elemental damage dealt.
**Potential** increases Arcane damage dealt.
**Vigor** increases Physical damage dealt.

---

## Balance System

### Concept

Each tower contributes Balance score every round. Balance flows into pools determined by the tower's damage type. The system rewards tower variety and penalizes stacking identical types, while leveling increases each tower's individual output value.

### Balance Output Formula

    Output = Level Value × Type Ratio

Each tower's individual contribution is `1 × ratio`, where ratio depends on how many towers of the same definition ID exist. The cumulative Balance for a type displayed in the HUD is the sum of all individual tower contributions.

**Level Value** doubles each level:

| Level | Value |
|---|---|
| L1 | 1 |
| L2 | 2 |
| L3 | 4 |
| L4 | 8 |
| L5 | 16 |

**Type Ratio** is based on how many towers of the same definition ID are placed. All levels of a type share one count:

| Count of same ID | Ratio |
|---|---|
| 1 to 4 | 1.00 (100%) |
| 5 | 0.64 |
| 6 | 0.44 |
| 8 | 0.25 |
| 10 | 0.16 |

The formula for count > 4 is `(4 / count)²`.

### Grouping Rules

- Uniqueness is per tower definition ID, not per tier or damage type.
- All levels of a tower type share one count. A Basic Tower L1 and a Basic Tower L4 are both counted as Basic Tower.
- A T1 Physical tower and a T2 Physical tower are different types and tracked independently.
- 4x Basic Tower at any level mix plus 4x Sniper Tower at any level mix means both types sit at 100%.

### Why Leveling Matters

Leveling does not widen Balance diversity but dramatically increases output at the same percentage. Four L4 Basic Towers at 100% contribute 32 Balance. Four L1 Basic Towers contribute 4. A fifth Basic Tower at any level reduces the percentage for all of them, making the high level multiplier suddenly costly.

### Balance Pools

**Elemental Balance feeds Income**
Increases orb spawn rate on income towers. More Elemental investment means more frequent collection opportunities.

**Arcane Balance feeds Research**
Generates tech points used to unlock tower batches. See Tower Tiers and Research.

**Physical Balance feeds Drop Chance**
Slightly increases the chance that kills drop gold or tech. Never the primary source of either, just a meaningful combat bonus for players who invest in Physical towers.

---

## Tower Tiers and Research

### Tier Structure

| Tier | Towers per damage type | Total unique types |
|---|---|---|
| T1 | 1 | 3 |
| T2 | 2 | 6 |

T1 towers are available from the start. Higher tiers are unlocked by spending tech earned from ability kills and Arcane Balance.

### Unlocking

T2 unlocks are organized into 3 batches, one per damage type, unlockable in any order with no prerequisites. Unlocking a batch makes all towers in it immediately buildable.

### Tower Levels

Any placed tower can be leveled up. Leveling increases Balance output value (doubling each level) and combat effectiveness. All levels of the same tower type share one Balance group count.

---

## Ability System

### Overview

Towers have two categories of abilities:

**Autocasting and passive abilities** fire automatically or apply constant effects. The player does not interact with them directly. The basic attack missile is an example. Kills from autocasting abilities still count as ability kills for tech drop purposes.

**Player-activated abilities** appear on the ability bar and require the player to trigger them manually.

### Ability Bar

A persistent UI bar showing the player's available activated abilities during a wave. Each ability on the bar comes from a tower type the player has placed. Building a Sniper Tower adds its snipe ability to the bar.

**Charges** equal the number of towers of that type that are currently ready (not on cooldown). 4 Sniper Towers means up to 4 snipe charges. If 2 are on cooldown, 2 charges are available. The bar is a live dashboard of combat readiness.

### Casting

1. Player presses an ability on the bar.
2. Game enters targeting mode. Only valid targets within range of at least one ready tower of that type are selectable. Out of range targets cannot be clicked.
3. Player clicks a valid target.
4. The closest ready tower of that type with the target in range fires the ability.
5. That specific tower goes on cooldown. Other towers of the same type keep their charges.

If a tower is selected when the player activates an ability, that specific tower fires instead of the closest one, provided it has the target in range and is not on cooldown.

### Tower Placement and Ability Coverage

A tower can only contribute its ability to a target it has range on. Four Sniper Towers in the back of the map cannot snipe targets near the entrance. Tower placement determines not just autocast coverage but ability reach, making positioning a deeper decision.

---

## Kill Economy Summary

| Kill type | Drop | Physical balance effect |
|---|---|---|
| Normal kill (basic attack) | Chance to drop gold | Increases drop chance |
| Ability kill (any ability) | Chance to drop tech | Increases drop chance |

---

## Enemies

Enemies follow a Catmull-Rom spline path between PathNodes. At junctions the path branches and a random edge is chosen. At the terminus the enemy deals a death blow to player lives and is destroyed.

| ID | Life | Speed | Defense | Bounty | Death Blow |
|---|---|---|---|---|---|
| basic_enemy | 100 | 3.0 | 0 | 10 | 1 |
| fast_enemy | 50 | 6.0 | 0 | 15 | 1 |
| armored_enemy | 200 | 1.5 | 20 | 25 | 1 |
| boss_enemy | 1000 | 1.0 | 30 | 150 | 3 |

---

## Technical Architecture

### Data-Driven Design

All gameplay objects are defined in JSON and built entirely in code. No prefabs.

| Definition file | Controls |
|---|---|
| units.json | Enemy stats, sprite, collider, scale |
| towers.json | Tower cost, sprite, components, ability reference |
| abilities.json | Cooldown, range, effect reference |
| effects.json | Effect type, damage values, missile config, cross-references |

### Ability and Effect System

Abilities reference effects by string ID. Effects reference other effects by string ID. All cross-references are resolved at runtime by EffectLibrary and AbilityLibrary. No ScriptableObject direct references are serialized.

**Effect types implemented:**
- `damage` — applies damage with crit, min/max clamping, and damage type
- `launch_missile` — spawns a homing ProjectileUnit, applies impactEffect on hit; supports custom color tint
- `search_area` — area-of-effect search with arc and max target filtering; used for chain lightning bounces
- `set` — executes a list of effects in sequence on the same context
- `launch_boomerang` — fires a projectile in a full 360° arc; pierces and multi-hits; clears hit list at 180° to allow re-hits on the return leg

### Tower Info Panel

Clicking a placed tower opens a panel showing:
- **Name** — tower display name
- **Balance** — balance type initial + individual contribution: `P  1 (0.85)` (value = 1, ratio in parens)
- **Damage** — base damage per hit resolved from the effect chain
- **Fire Rate** — shots per second
- **Kills** — lifetime kill count for that specific tower instance

### HUD Header Balance Bar

A second row in the top bar shows cumulative Balance per type:
- `E  {sum}` `A  {sum}` `P  {sum}`
- Each value is the sum of all non-ghost tower contributions for that type: `count × ratio`
- Ghost preview towers are excluded from all counts

### Path System

Paths are directed graphs of PathNode MonoBehaviours. PathGraph walks the graph and samples a Catmull-Rom spline between nodes. RouteFollower moves enemies along the pre-built waypoint list and exposes a Progress value (0 to 1) for tower targeting.

### Tower Targeting

Turrets detect enemies via a trigger CircleCollider2D sized to the ability range. Every frame the turret picks the enemy with the highest RouteFollower.Progress (closest to the exit) and fires at them. Target is re-evaluated every frame.

---

## Open Questions

- Balance ratio curve is `(4/count)²` — may need tuning as more tower types are added
- Tower level cap
- How tech accumulates and exact Research costs per tier batch
- Ability bar slot limit, whether building too many tower types forces the player to choose
- Whether autocasting ability kills count the same as player-activated ability kills for tech drops (current assumption: yes)
- Lives system and lose condition
- Win condition and campaign structure
- Wave structure beyond the demo wave
- Whether bypass damage types have any counter-play beyond dedicated buffs
- T3 and beyond: tier count, batch structure
