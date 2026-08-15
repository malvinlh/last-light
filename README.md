# Last Light

A 2D turn-based deckbuilder built for the Bumi Studio Game Programmer technical test (Option A).

*Development window: 15–17 August 2026. The brief arrived on 11 August, but 11–14 August were
committed to a full-time offline internship, so this was built in three days against a brief scoped
for five to seven. Noting it for context, not as an excuse — the scope was chosen to fit.*

![Combat](Documentation/screenshots/02-combat.png)

---

## Game Overview

You are the last Lampwright. The Dark has swallowed the coast and your lantern is the only thing
still burning. Hold the light for three nights.

**Genre:** single-player 2D turn-based deckbuilder, one run at a time.

**Core mechanic.** Everything you do is a card. Each turn you draw five cards and get three
**Focus**; every card costs Focus to play, and unspent Focus is lost when the turn ends. Cards deal
damage, grant **Ward** (temporary shielding that expires at the start of your next turn), restore
**Light**, draw more cards, or apply one of two statuses — **Kindled** (+1 damage per stack) and
**Exposed** (+50% damage taken). Those two statuses are what make cards combo: Kindled applies to
*every hit*, so it is worth far more on a multi-hit card like Twin Spark than on a single big swing.

The enemy announces its next move a full turn in advance, with the exact number it will use. That
number is computed by the same code that will later apply the damage, so the telegraph cannot lie
to you. Planning around it is the game.

**A run** is five stops: fight, draft, fight, shrine, boss.

```
The First Watch  →  Salvage        →  The Second Watch  →  The Old Shrine   →  The Last Watch
(Fledgling Shade)   (take 1 of 3)     (Grasping Mire)      (sharpen / release   (The Devouring Dark)
                                                            / rest)
```

Your **Light is your health and it carries between stages** — there is no free heal between fights,
which is what makes a sloppy early win cost you later. Your deck carries too, so the card you draft
after stage one is in the deck you fight stage two with.

**You win** by clearing all three fights. **You lose** the moment your Light reaches zero, at any
point in the run. Either way you get a summary of the run and can start a new one immediately.

---

## How to Run

**Engine:** Unity **6000.0.75f1** (the version required by the brief).

### Play the build

1. Download the build — it is in [`/Build/LastLight/`](Build/LastLight/) in this repository, or from
   the mirror below if you would rather not clone.
2. Run `LastLight.exe`.
3. Windows SmartScreen will warn about an unsigned executable — *More info* → *Run anyway*.

The game opens windowed at 1600×900 and is resizable; the UI scales to any resolution.

- **Build location:** [`/Build/LastLight/LastLight.exe`](Build/LastLight/)
- **Mirror:** `TODO — Google Drive link to be added before submission` *(35 MB zip)*

### Open the project

1. Clone, then open the folder with Unity **6000.0.75f1**.
2. Open `Assets/_Project/Scenes/MainMenu.unity` and press Play.

### Controls

Mouse only. Click a card to play it, click **End Turn** when you are done. **Hover anything** —
Light, Ward, Focus, the piles, the enemy's intent — to get an explanation of the rule behind it.

---

## Technical Decisions

### The gameplay layer contains no MonoBehaviours

The rules live in plain C# classes, and presentation is a thin layer on top. This is enforced at
compile time by four assembly definitions with a one-way dependency graph:

```
LastLight.Gameplay          data + rules, zero MonoBehaviours
      ▲
LastLight.Presentation      views, references Gameplay
LastLight.Editor            generators, validator, build (Editor only)
LastLight.Tests.EditMode    references Gameplay
```

The gameplay assembly *cannot* reference the UI, even by accident. The payoff is that the entire
rule set is testable headlessly: 91 EditMode tests run with no scene loaded and no Play mode.

### Cards are data composed from reusable effects, not scripts

A `CardDefinition` holds `[SerializeReference] List<CardEffect>` — a polymorphic list of atoms:
`DealDamage`, `GainWard`, `Heal`, `DrawCards`, `GainFocus`, `ApplyStatus`, `Repeat`. Adding a card
is data entry. Adding a new *kind* of behaviour is one small class. There is no card-name switch
anywhere in the codebase, which was the main thing I wanted the architecture to demonstrate.

`Repeat` is the clearest argument for the approach: repeating is orthogonal to what is repeated, so
composing it gives multi-hit to every atom for free — and because Kindled applies per hit, that
composition is also where the card synergy comes from.

Enemy actions are built from the **same** atoms. An enemy gaining Ward and a card gaining Ward run
identical code, so there is only ever one implementation to get right.

### `CardDefinition` and `RuntimeCard` are separate on purpose

The definition is a ScriptableObject shared by every copy of that card in every run, and it is never
written to. Per-copy state — "this particular copy has been sharpened" — lives on `RuntimeCard`.
Without that split, upgrading a card at a Shrine would mutate the asset on disk and leak into your
next run. Two tests exist purely to guard this.

Card instance ids come from the run rather than a static counter, so nothing survives between runs
or between tests.

### Rules text is generated from the effects

A card's description is the concatenation of `effect.Describe(isUpgraded)`. The text on the card and
the behaviour it performs are produced from the same data, so they cannot drift apart — changing a
number changes the printed card in the same edit. The same applies to the enemy's intent number,
which is read from the action's own effects.

### One damage pipeline, used by the hit and by the preview

`CombatController.ComputeDamage` is the only place modifiers are applied. `PreviewIntentValue()`
calls it too, which is why the number on the enemy's intent is exactly what you will take, buffs
included. A test asserts the two agree.

### The UI asks; the controller decides

`TryPlayCard` is the single gate every play passes through, and it returns a *reason*
(`NotEnoughFocus`, `NotPlayerTurn`, `CardNotInHand`, `InvalidTarget`, `CombatOver`) rather than a
bool, so a refusal can be explained instead of swallowed. The hand greys a card out by calling the
same `ValidatePlay` the click will hit, so the UI cannot disagree with the rules it is describing —
and an unaffordable card stays clickable precisely so the refusal gets explained.

No view writes to Light, Ward, Focus or any pile. They subscribe to events and re-read state.

### A run is a list of nodes in an asset

`RunConfig` describes the whole run: starting Light, starter deck, reward pool, and the node
sequence. Nothing in the code knows there are three fights; it knows there is a list. Reordering
stages or adding one is an asset edit.

The run owns the truth and combat borrows it: each fight builds a fresh `CombatController` over the
run's card list and copies Light back when it ends. That direction of flow is why a drafted card is
simply *present* in the next stage with no syncing code.

**New Run constructs a new `RunState` rather than resetting fields**, so stale state is impossible by
construction instead of by remembering to clear everything. It is an in-place reset, not a scene
reload — a reload would hide whether run state is actually owned correctly.

### Content and scenes are generated by editor tools

Cards and enemies are authored once in a C# catalog and generated into ScriptableObject assets; the
scenes and the card prefab are built by `SceneBuilder`. Three reasons: the whole card set is
reviewable in one diff, a balance pass is a single-file edit, and wiring dozens of serialized
references by hand is the most error-prone part of this workflow. Views expose an editor-only
`Bind()` so the generator assigns references through a compiler-checked call rather than by name.

Data generation is **idempotent** — assets whose content already matches are left untouched. That
took a second pass to get right: `[SerializeReference]` mints new reference ids whenever a list is
replaced, so the first version rewrote all 19 assets on every run and produced a diff of pure noise.
Each effect now reports a content signature and the generator compares before writing. Scene
generation is *not* idempotent, and that is a known issue below.

### Randomness is injected and seeded

Nothing calls `UnityEngine.Random`. Every system that needs randomness takes a `GameRng`, so a run
is reproducible from one seed and every test is deterministic without stubbing.

### How this was tested

- **91 EditMode tests** — the rules, with no scene: deck reshuffling and exhaustion, every rejection
  reason, each effect atom, damage clamping, the Kindled/Exposed interactions, the exact phase
  sequence, Light carrying between stages, and that a new run resets everything.
- **23 PlayMode tests** — the real scenes, driven the way a player drives them: clicking actual
  buttons and card views, walking a whole run from fight through draft and shrine to the summary.
  These exist because with a code-generated UI the realistic failure is an unassigned reference — a
  break that compiles, passes every unit test, and shows up as a dead button.
- **`ProjectValidator`** — editor version, build settings, duplicate card ids, effect-less cards,
  combat nodes with no enemy, missing scene components. The class of bug that survives a green test
  run.
- **A screenshot fixture** that renders each screen to a PNG. Two purely visual bugs,
  command-line driven, and two purely visual bugs — a health bar rendering as a lens, a display font
  whose `7` reads as `⌐` — passed the entire test suite and were only caught by looking.

### What I deliberately did not build

Multiple enemies per encounter, relics or passive items, save/load between sessions, a branching
map, audio, localisation, and gamepad input. Each is a real system, none of them demonstrate
anything the card architecture does not already show, and the time went into testing instead.

---

## What I Would Do With More Time

Roughly in the order I would pick them up:

1. **Sequence the presentation of a turn.** Combat resolves instantly and the views re-read state,
   so when the enemy acts the numbers snap while only flourishes animate. The architecture is
   already right for the fix — the controller emits an event stream — so this is a presentation-side
   queue that replays those events with delays and blocks input while it plays. It is the single
   biggest gap between this and something that feels finished.
2. **Make scene generation idempotent, or stop generating scenes.** See Known Issues. Most likely by
   generating prefabs and composing a thin hand-authored scene from them, so regenerating touches
   prefabs whose ids are stable.
3. **More enemies and a real encounter table**, so a run is not the same three fights. The data model
   already supports it; it is content plus a weighted picker.
4. **A proper card reward pool weighted by rarity**, plus a "remove a card" reward, so deck
   *thinning* competes with deck *growth* as a strategy.
5. **Balance from data.** A headless simulator that plays a few thousand runs with a simple policy
   and reports win rate per stage — the deterministic seeded RNG and MonoBehaviour-free rules layer
   make this cheap to write, and it would replace my guesses about the boss ramp.
6. **Per-card artwork.** `CardDefinition` already has an `artwork` field; nothing populates it.
7. **Keyboard support** — number keys to play cards, space to end turn — and making the hover
   explanations reachable without a mouse.

---

## Known Issues

- **Scene generation is not idempotent.** Rebuilding the scenes rewrites `Game.unity` in full
  (~2,700 lines) because Unity assigns fresh local file ids to objects created in a new scene. The
  content is identical; the diff is noise. It is kept off the routine path as its own menu item.
  The *data* generator does not have this problem — it compares content signatures first.
- **Turn resolution is not animated.** The enemy's whole turn applies in one frame. Numbers jump;
  only the hit flash and floating damage animate. See item 1 above.
- **The hand overlaps rather than fans** once it holds more than about seven cards. Readable, but
  the tighter spacing is a compromise, not a design.
- **Balance is lightly tuned.** I played the run enough to know it is winnable and losable, not
  enough to call the curve good. The Devouring Dark ramps itself with Kindled, so a slow start
  against it can spiral.
- **No audio at all.**
- **Enemy patterns are fixed loops.** Deliberate — it makes fights learnable and tests deterministic
  — but it does mean a second run against the same enemy holds no surprises.
- **Cards have no individual art**, only colour coding by type.
- **Hover explanations are mouse-only.**
- **The screenshot fixture temporarily switches the canvas to camera space** to render, because
  overlay canvases bypass cameras and `ScreenCapture` writes nothing in batch mode. It is a
  test-only hack and it does touch scene state while it runs.
- **The EditMode test assembly references the presentation assembly**, purely to reach one pure
  static function (tooltip placement). The tests are still headless and scene-free, but it is a
  crack in the "tests only reference gameplay" rule I set myself.

---

## Credits

All gameplay code is my own.

Visual assets are by [Kenney](https://kenney.nl) and released under **CC0** (public domain) — the UI
panels, buttons and the display font. Only the specific files used are committed; see
[`Assets/_Project/Art/Kenney/ATTRIBUTION.md`](Assets/_Project/Art/Kenney/ATTRIBUTION.md) for the
list and the original licences. The actor discs and their glow are generated procedurally by the
scene builder.

## Further reading

- [Documentation/ARCHITECTURE.md](Documentation/ARCHITECTURE.md) — diagrams of the layers, the turn
  machine, deck lifecycle and run flow
- [Documentation/REQUIREMENTS.md](Documentation/REQUIREMENTS.md) — the brief's requirements audited
  line by line against what shipped
- [Documentation/QA-CHECKLIST.md](Documentation/QA-CHECKLIST.md) — the manual pass run against the
  built executable
- [Documentation/CARD-REFERENCE.md](Documentation/CARD-REFERENCE.md) — every card and enemy
- [Documentation/screenshots/](Documentation/screenshots/) — every screen
