# Card and enemy reference

Generated from the ScriptableObject assets by **Last Light → Write Card Reference**.
Do not edit by hand; change the catalog in `Assets/_Project/Editor/Generators/`,
regenerate the data, then regenerate this.

Rules text is produced by the cards' own effects, so what is printed here is exactly
what the card shows in game. The upgraded column is what a Shrine sharpening turns it into.

## Starter deck

| Copies | Card | Cost | Type | Effect | Sharpened |
|---|---|---|---|---|---|
| 5x | **Ember Strike** | 1 | Attack | Deal 6 damage. | Deal 9 damage. |
| 1x | **Kindle** | 1 | Skill | Draw 2 cards. | Draw 3 cards. |
| 4x | **Ward** | 1 | Skill | Gain 5 Ward. | Gain 8 Ward. |

## Reward pool

Drafted one of three after a victory.

| | Card | Cost | Type | Effect | Sharpened |
|---|---|---|---|---|---|
|  | **Binding Light** | 1 | Skill | Apply 3 Exposed. | Apply 4 Exposed. |
|  | **Bulwark** | 2 | Skill | Gain 12 Ward. | Gain 16 Ward. |
|  | **Focus Lens** | 1 | Skill | Gain 2 Kindled. | Gain 3 Kindled. |
|  | **Hearthguard** | 1 | Skill | Gain 6 Ward. Restore 3 Light. | Gain 9 Ward. Restore 5 Light. |
|  | **Lantern Flare** | 2 | Attack | Deal 12 damage. | Deal 16 damage. |
|  | **Long Watch** | 0 | Skill | Draw 1 card. | Draw 2 cards. |
|  | **Rekindle** | 1 | Skill | Restore 6 Light. | Restore 9 Light. |
|  | **Sear** | 1 | Attack | Deal 4 damage. Apply 2 Exposed. | Deal 6 damage. Apply 3 Exposed. |
|  | **Second Wind** | 1 | Skill | Gain 2 Focus. | Gain 3 Focus. |
|  | **Smother** | 2 | Attack | Deal 8 damage. Gain 4 Ward. | Deal 11 damage. Gain 6 Ward. |
|  | **Surge** | 2 | Attack | Deal 5 damage. Draw 1 card. | Deal 7 damage. Draw 2 cards. |
|  | **Twin Spark** | 1 | Attack | Deal 4 damage. Repeat 2 times in total. | Deal 4 damage. Repeat 3 times in total. |

## Enemies

Patterns loop, and the next action is always telegraphed a turn ahead.

### The Devouring Dark (55 Light)

*The thing the lighthouse was built against.*

| # | Intent | Action | Effect |
|---|---|---|---|
| 1 | Buff | Gather | Gain 2 Kindled. |
| 2 | Attack | Swallow | Deal 8 damage. |
| 3 | Attack | Swallow | Deal 8 damage. |
| 4 | Debuff | Unmake | Apply 2 Exposed. |
| 5 | Attack | Extinguish | Deal 12 damage. |

### Fledgling Shade (22 Light)

*A thin, hungry thing. It has not learned patience yet.*

| # | Intent | Action | Effect |
|---|---|---|---|
| 1 | Attack | Lunge | Deal 7 damage. |
| 2 | Attack | Lunge | Deal 7 damage. |
| 3 | Defend | Coil | Gain 6 Ward. |

### Grasping Mire (32 Light)

*It does not chase. It waits for the light to come to it.*

| # | Intent | Action | Effect |
|---|---|---|---|
| 1 | Debuff | Drag Under | Apply 2 Exposed. |
| 2 | Attack | Crush | Deal 9 damage. |
| 3 | Attack | Seep | Deal 5 damage. |
| 4 | Defend | Harden | Gain 8 Ward. |

## Run layout

Starting Light **50** · hand **5** · Focus **3** per turn · draft offers **3** · rest restores **12** · deck floor **5**

| Stage | Kind | Title | Enemy |
|---|---|---|---|
| 1 | Combat | The First Watch | Fledgling Shade |
| 2 | CardReward | Salvage | - |
| 3 | Combat | The Second Watch | Grasping Mire |
| 4 | Shrine | The Old Shrine | - |
| 5 | Combat | The Last Watch | The Devouring Dark |

