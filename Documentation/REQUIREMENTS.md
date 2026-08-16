# Requirement audit

Every requirement in the brief, audited against what actually shipped. Re-read the brief and walked
this line by line on **17 Aug 2026**, after the Windows build was produced.

**Verified in build** means observed by running `Build/LastLight/LastLight.exe` outside the Editor,
not inferred from a passing test.

## Mandatory

| # | Requirement | Implemented | Tested | Verified in build | Evidence |
|---|---|---|---|---|---|
| M1 | Unity Engine, editor `6000.0.75f1` | Yes | Yes | Yes | `ProjectSettings/ProjectVersion.txt`; asserted by `ProjectValidator`; player log line `Initialize engine version: 6000.0.75f1` |
| M2 | 2D PC game | Yes | Yes | Yes | URP 2D renderer, orthographic camera, sprite actors; `Documentation/screenshots/` |
| M3 | Windows `.exe` build | Yes | Yes | Yes | `Build/LastLight/LastLight.exe`, 99 MB, built by `BuildScript`; launched and played |
| M4 | Turn-based | Yes | Yes | Yes | `CombatPhase` machine; `TurnFlowTests.ThePhasesRunInTheDocumentedOrder` |
| M5 | Player actions driven by cards from a hand | Yes | Yes | Yes | `TryPlayCard` is the only action verb; End Turn is the only other input |
| M6 | Run-based structure | Yes | Yes | Yes | `RunController` + `RunState`; `RunLoopTests` walks a full run |
| M7 | Deck progression: added, removed **or** upgraded | Yes (all three) | Yes | Yes | Draft adds; Shrine sharpens or releases; `RunProgressionTests`, `RunLoopTests` |
| M8 | Loop: draw → decide → resolve → progress | Yes | Yes | Yes | Turn machine + node advance |
| M9 | ≥2 distinct stages or decision points | Yes (5 nodes, 3 kinds) | Yes | Yes | 3 combats + a draft + a shrine, from `RunConfig` |
| M10 | Clear win condition | Yes | Yes | Yes | Clearing every node → `RunOutcome.Victory` → summary |
| M11 | Clear loss condition | Yes | Yes | Yes | Light reaches 0 → `RunOutcome.Defeat`, immediately, mid-run |
| M12 | Start a new run after finishing | Yes | Yes | Yes | **New Run** on the summary; `NewRunFromTheSummaryResetsEverything` |
| M13 | Full project source in the repo | Yes | n/a | n/a | `Assets/`, `Packages/`, `ProjectSettings/` all tracked |
| M14 | Root `README.md` in the brief's structure | Yes | n/a | n/a | Five required sections, in order |
| M15 | Build in `/Build/` or an external link | Yes (both) | n/a | Yes | `/Build/LastLight/` committed; 36 MB mirror zip prepared |
| M16 | Meaningful commit history | Yes | n/a | n/a | 60+ commits across five milestones, no squashing |
| M17 | All gameplay code is my own | Yes | n/a | n/a | Only third-party content is CC0 art; see `ATTRIBUTION.md` |
| M18 | No paid or inaccessible dependencies | Yes | n/a | n/a | `Packages/manifest.json` is Unity packages only |
| M19 | No high-level gameplay framework | Yes | n/a | n/a | FSM, deck, effect system all hand-written |
| M20 | Free assets for visuals/audio only | Yes | n/a | Yes | Kenney CC0 sprites + one font. Music is synthesised by `Tools/audio/generate_music.py`, so it is original rather than third-party. No third-party logic anywhere. |

## Stretch goals

| # | Goal | Status | Evidence |
|---|---|---|---|
| S1 | Card draft / reward screen between stages | **Done** | Salvage node: 1 of 3, or skip. The drafted card is in the deck the next stage is played with: asserted by test. |
| S2 | Card upgrade or removal | **Done, both** | Shrine: sharpen one copy, release one card, or rest. One boon per visit; removal refuses to shrink the deck below a floor. |
| S3 | Synergies between card combinations | **Done** | Kindled applies per hit, so it is worth more on multi-hit cards (Focus Lens → Twin Spark). Exposed multiplies incoming damage (Sear/Binding Light → a big hit). Covered by `Repeat_AppliesKindledToEveryHit`. |
| S4 | Run history / end-of-run summary | **Partial** | End-of-run summary with stages, turns, Light, cards drafted/sharpened/released, and a beat-by-beat log. No history *across* runs: nothing is persisted between sessions. |

## Explicitly not built

Documented in the README rather than left for the reviewer to discover: multiple enemies per
encounter, relics or passive items, save/load between sessions, a branching map, sound effects,
localisation, gamepad input, procedural encounter generation.

## Test and tooling summary

| Gate | Count | What it covers |
|---|---|---|
| EditMode tests | 91 | Rules, with no scene: deck, card play validation, every effect atom, statuses, turn order, run progression, tooltip placement maths, data content signatures |
| PlayMode tests | 23 (+1 explicit) | The real scenes driven through real buttons: vertical slice, full run loop, main menu transition, UI layout bounds |
| `ProjectValidator` | 6 groups | Editor version, build settings, card ids/costs/effects, enemy patterns, run config integrity, scene components |
| Screenshot fixture | 10 images | Every screen, rendered headlessly from a real playthrough |

## Honest gaps

Things a reviewer could reasonably mark down, listed here rather than hidden:

- Scene generation is not idempotent (fileID churn). Data generation is.
- Turn resolution is not sequenced for presentation: the enemy's turn applies in one frame.
- Balance is lightly tuned; verified winnable and losable, not curve-tested.
- Music is synthesised rather than composed, and there are no sound effects.
- The EditMode assembly references the presentation assembly to reach one pure function.

Full list with context in the README's Known Issues.
