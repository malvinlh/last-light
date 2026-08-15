# Architecture

Diagrams are Mermaid, which GitHub renders inline — no image files to fall out of date.

## Layers

The one-way dependency is enforced by assembly definitions, not convention. The gameplay assembly
cannot reference the presentation assembly even by accident, which is what keeps the rules testable
with no scene loaded.

```mermaid
flowchart TD
    subgraph editor["LastLight.Editor  (Editor only)"]
        GEN["Generators<br/>cards · enemies · run · scenes"]
        VAL["ProjectValidator"]
        BLD["BuildScript"]
    end

    subgraph pres["LastLight.Presentation  (MonoBehaviours)"]
        SESS["GameSession"]
        ROUTE["ScreenRouter"]
        VIEWS["CombatScreen · CardView · ActorView<br/>RewardScreen · ShrineScreen · RunResultScreen"]
    end

    subgraph play["LastLight.Gameplay  (plain C#, zero MonoBehaviours)"]
        RUN["RunController · RunState"]
        COMBAT["CombatController · CombatState"]
        DECK["DeckService"]
        CARDS["CardDefinition · RuntimeCard"]
        FX["CardEffect atoms"]
    end

    TESTS["LastLight.Tests.EditMode"]

    SESS --> RUN
    VIEWS --> COMBAT
    ROUTE --> VIEWS
    RUN --> COMBAT
    COMBAT --> DECK
    COMBAT --> FX
    DECK --> CARDS
    FX --> CARDS
    GEN --> CARDS
    VAL --> RUN
    TESTS --> RUN

    style play fill:#1d2430,stroke:#5b7fa8,color:#e8e6df
    style pres fill:#2a2233,stroke:#a86ba8,color:#e8e6df
    style editor fill:#232a22,stroke:#7aa86b,color:#e8e6df
```

## Card definition to resolution

The path a card takes from authored data to an applied effect. Note that the definition asset is
only ever read.

```mermaid
flowchart LR
    CAT["CardCatalog<br/><i>C# table</i>"] -->|generated once| DEF["CardDefinition<br/><i>ScriptableObject, immutable</i>"]
    DEF -->|"referenced, never written"| RC["RuntimeCard<br/><i>InstanceId · IsUpgraded</i>"]
    RC -->|in the run deck| DECK["DeckService"]
    DECK -->|drawn to hand| PLAY["CombatController.TryPlayCard"]
    PLAY -->|validated| CTX["EffectContext<br/><i>the only surface an effect sees</i>"]
    CTX --> FX["CardEffect.Resolve"]
    FX --> STATE["CombatState<br/>Light · Ward · Focus · statuses"]
    DEF -.->|"Describe()"| TEXT["Rules text on the card"]
    FX -.->|same data| TEXT
```

The dotted path is why printed text cannot drift from behaviour: both come from the same effects.

## Turn flow

`PlayerAction` is the only phase in which a card can be played. Every other phase passes through in
a single synchronous step.

```mermaid
stateDiagram-v2
    [*] --> NotStarted
    NotStarted --> CombatStart: StartCombat()
    CombatStart --> PlayerTurnStart: telegraph intent
    PlayerTurnStart --> PlayerAction: expire Ward · tick statuses<br/>refill Focus · draw 5
    PlayerAction --> PlayerAction: TryPlayCard (repeatable)
    PlayerAction --> PlayerTurnEnd: EndPlayerTurn()
    PlayerTurnEnd --> ResolveCheck: discard hand
    ResolveCheck --> EnemyTurn: both alive
    EnemyTurn --> ResolveCheck: resolve intent · pick next
    ResolveCheck --> PlayerTurnStart: both alive
    ResolveCheck --> CombatEnd: someone died
    CombatEnd --> [*]
```

A card that kills the enemy mid-turn ends the combat immediately — `TryPlayCard` re-checks the
outcome after resolving, so the phase moves to `CombatEnd` without waiting for the turn to end.

## Deck lifecycle

```mermaid
flowchart LR
    RUNDECK[("Run deck<br/><i>persists all run</i>")] -->|"new combat: shuffle"| DRAW[("Draw pile")]
    DRAW -->|"draw 5 each turn"| HAND[("Hand")]
    HAND -->|"card played"| DISCARD[("Discard pile")]
    HAND -->|"end turn: discard hand"| DISCARD
    DISCARD -->|"draw pile empty:<br/>reshuffle"| DRAW
    DRAW -.->|"all piles empty:<br/>draw returns what it can"| STOP["no deadlock"]
```

A played card leaves the hand *before* it resolves and enters the discard *after*, so a card that
draws cards can never redraw itself mid-resolution.

## Run flow

```mermaid
flowchart TD
    MENU["MainMenu scene"] -->|"Begin the Watch"| START["RunController.StartNewRun()"]
    START --> NODE{"Current node kind?"}

    NODE -->|Combat| FIGHT["BeginCombat()"]
    NODE -->|CardReward| DRAFT["Draft 1 of 3, or skip"]
    NODE -->|Shrine| SHRINE["Sharpen · release · rest<br/><i>one only</i>"]

    FIGHT --> OUT{"Outcome"}
    OUT -->|Victory| CLEARED["Stage cleared"] --> ADV["AdvanceToNextNode()"]
    OUT -->|Defeat| LOST["RunEnded(Defeat)"]

    DRAFT --> ADV
    SHRINE --> ADV

    ADV --> PAST{"Past the last node?"}
    PAST -->|no| NODE
    PAST -->|yes| WON["RunEnded(Victory)"]

    LOST --> SUMMARY["Run summary"]
    WON --> SUMMARY
    SUMMARY -->|"New Run"| START
    SUMMARY -->|"Main Menu"| MENU
```

Both ways a run can end funnel through `RunEnded`, so neither needs a special case in the session.
Winning is *running off the end of the node list* rather than a flag set by the last fight.

## Where state lives

| State | Owner | Lifetime |
|---|---|---|
| Card rules, costs, effects | `CardDefinition` asset | forever, read-only |
| Which copy is upgraded | `RuntimeCard` | one run |
| Light, run deck, node cursor, summary | `RunState` | one run |
| Draw / hand / discard | `DeckService` | one combat |
| Phase, turn, Focus, Ward, statuses | `CombatState` | one combat |
| Anything on screen | views | one frame — always re-read |

The rule that keeps this honest: **the run owns the truth and combat borrows it.** A combat is built
over the run's card list and copies Light back at exactly one point, when it ends.
