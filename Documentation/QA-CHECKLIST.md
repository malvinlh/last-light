# QA checklist

The manual pass for the built executable. Automated coverage is listed alongside each item so it is
clear what is *proven* versus what is *observed by hand*: a green test suite does not prove the
build launches, and launching does not prove the rules are right.

**Legend:** ✅ covered by an automated test · 👁 must be seen by a human · ⚙ checked by
`ProjectValidator`

## Before the build

| # | Check | How |
|---|---|---|
| 1 | EditMode suite green | ⚙ `-runTests -testPlatform EditMode` → 91/91 |
| 2 | PlayMode suite green | ✅ `-runTests -testPlatform PlayMode` → 23/23 (+1 explicit, skipped) |
| 3 | Validator clean | ⚙ `-executeMethod …ProjectValidator.ValidateFromCLI` → exit 0 |
| 4 | Regenerating data produces no diff | ⚙ run the generator twice, `git status` clean both times |

## Launch

| # | Check | Status |
|---|---|---|
| 6 | `LastLight.exe` starts outside the Editor | ✅ launched, survived 12 s, `Player.log` has zero exceptions |
| 7 | Player log reports engine `6000.0.75f1` | ✅ `Initialize engine version: 6000.0.75f1` |
| 8 | Opens on the main menu, windowed, resizable | 👁 |
| 9 | **Quit** closes the game | 👁 |
| 9a | Menu music plays on the title screen | 👁 |
| 9b | **Music: On/Off** silences it and the label updates | 👁 |
| 9c | The preference survives restarting the game | 👁 |
| 9d | A different, denser track plays in the run | 👁 |
| 9e | Neither track clicks at the loop point (wait ~60 s) | 👁 |

## Combat: stage one

| # | Check | Status |
|---|---|---|
| 10 | **Begin the Watch** loads the game and starts a fight | ✅ `BeginningTheWatchLoadsTheGameScene` |
| 11 | Opening hand is 5 cards, Focus 3/3, Turn 1, Light 50/50 | ✅ `TheSceneOpensIntoAStartedCombat` |
| 12 | Every card in hand has a visible card view | ✅ `TheHandIsRenderedAsOneCardViewPerCard` |
| 13 | Playing an attack reduces enemy Light and spends Focus | ✅ `PlayingACardThroughTheSessionHurtsTheEnemy` |
| 14 | Played card moves to the discard pile; counter increments | ✅ + 👁 |
| 15 | A card you cannot afford is greyed but still clickable | ✅ `ClickingAnUnaffordableCardExplainsWhyItWasRefused` |
| 16 | Clicking it shows "Not enough Focus." and keeps the card | ✅ same test |
| 17 | Enemy intent shows a kind and a number before you act | ✅ `TheEnemyIntentIsKnownBeforeThePlayerActs` |
| 18 | Damage taken equals the number the intent advertised | ✅ `EndingTheTurnLetsTheEnemyActAndDealsAFreshHand` |
| 19 | Ward absorbs a hit and is gone next turn | ✅ `WardSurvivesTheEnemyTurnAndExpiresOnYourNext` |
| 20 | Hit flash and floating damage number appear | 👁 |
| 21 | Actors drift and their glow pulses | 👁 |
| 22 | Hovering Light / Ward / Focus / Draw / Discard / intent explains the rule | ✅ (Focus box) + 👁 (the rest) |
| 23 | A tooltip near a screen edge stays fully on screen | ✅ `TooltipPlacementTests` + 👁 |

## Run progression

| # | Check | Status |
|---|---|---|
| 24 | Clearing a stage shows the overlay, then routes to the draft | ✅ `ClearingAStageRoutesToTheRewardDraft` |
| 25 | Draft offers 3 distinct cards plus **Take nothing** | ✅ `ARewardNodeOffersDistinctChoices` |
| 26 | A drafted card is in the deck the next stage is played with | ✅ `ADraftedCardIsInTheDeckTheNextStageIsPlayedWith` |
| 27 | **Take nothing** adds nothing | ✅ `SkippingTheDraftTakesNothing` |
| 28 | Light carries from stage one into stage two | ✅ `LightCarriesAcrossStages` |
| 29 | Shrine reached after stage two | ✅ `TheShrineIsReachedAfterTheSecondStage` |
| 30 | Sharpen upgrades exactly one copy; card face shows `+` and new numbers | ✅ `TheShrineSharpensExactlyOneCopy` + 👁 |
| 31 | Release removes a card | ✅ `TheShrineCanReleaseACard` |
| 32 | Rest restores Light, clamped to maximum | ✅ `TheShrineCanRestoreLight` |
| 33 | Shrine grants exactly one boon, then advances | ✅ `AShrineGrantsExactlyOneBoon` |
| 34 | Boss ramps itself with Kindled over the fight | 👁 |

## Ending and restarting

| # | Check | Status |
|---|---|---|
| 35 | Clearing all three fights wins the run | ✅ `ClearingEveryStageWinsTheRun` |
| 36 | Light reaching 0 ends the run immediately, mid-run | ✅ `DyingEndsTheRunStraightAway` |
| 37 | Summary reports stages, turns, Light, cards drafted/sharpened/released | ✅ `TheSummaryRecordsWhatHappened` + 👁 |
| 38 | No input is accepted after a combat ends | ✅ `ClearingTheStageRaisesTheOverlayAndStopsAcceptingInput` |
| 39 | **New Run** restores starter deck, full Light, node 0, clears upgrades | ✅ `NewRunFromTheSummaryResetsEverything` |
| 40 | **Main Menu** returns to the title screen | 👁 |

## Presentation

| # | Check | Status |
|---|---|---|
| 41 | Key panels have size and sit inside the canvas | ✅ `EveryKeyPanelHasSizeAndSitsInsideTheCanvas` |
| 42 | Resizing the window keeps the UI laid out correctly | 👁 |
| 43 | No element mixes two fonts | 👁 |
| 44 | All numbers legible (the display face is not used for digits) | 👁 |

---

## The acceptance run

One pass through the built `.exe`, in order. This is the sequence to run before submitting:

1. Launch `Build/LastLight/LastLight.exe` → **Begin the Watch**
2. Note the enemy's intent number, end the turn without playing, confirm you lose exactly that much
   Light
3. Spend all Focus, click a greyed card, confirm the refusal message
4. Hover Ward, Focus and the intent badge: confirm each explains itself and stays on screen
5. Win stage one → take a specific reward card, note its name
6. Confirm you draw that card during stage two
7. Win stage two → Shrine → **Sharpen**, pick a card, confirm the `+` and the higher numbers
8. Fight the boss; watch it gain Kindled and hit harder each cycle
9. Win → read the summary → **New Run** → confirm 10 cards, 50 Light, stage 1
10. Start again and deliberately lose → confirm the defeat summary appears immediately

**Status of the acceptance run:** steps 1–2 verified automatically and the executable confirmed to
launch cleanly; the full click-through is the manual pass, run against the build before submission.
