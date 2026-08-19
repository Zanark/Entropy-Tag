# Technical Architecture

## Architecture objective

Build a Unity game whose rules can be tested without scenes, whose presentation can change without rewriting
domain logic, and whose first vertical slice does not create accidental networking or content dependencies.

## Layer model

```mermaid
flowchart TB
    accTitle: EntropyTag Unity architecture layers
    accDescr: Pure C sharp domain rules are orchestrated by simulation services, adapted to Unity through scene components, presented by visual and audio systems, and integrated with platform infrastructure.

    presentation[Presentation<br/>Camera, animation, VFX, audio, UI]
    unity[Unity adapters<br/>MonoBehaviours, physics, input, scene binding]
    application[Application and simulation<br/>Match flow, commands, queries]
    domain[Domain<br/>Elements, territory, reactions, scoring]
    infrastructure[Infrastructure<br/>Settings, persistence, diagnostics, build]

    presentation --> unity
    unity --> application
    application --> domain
    infrastructure --> application
    infrastructure --> unity
```

### Domain

Pure C# types with no dependency on `UnityEngine`.

Responsibilities:

- Element identities.
- Territory ownership and reaction state.
- Reaction resolution.
- Score and bank rules.
- Match phase and winner rules.
- Deterministic configuration validation.

### Application and simulation

Coordinates domain operations and exposes commands/events.

Responsibilities:

- Start, tick, pause, and complete a match.
- Accept player/bot intent.
- Apply territory mutations.
- Advance timers and match pressure.
- Emit typed events for presentation.
- Provide read-only snapshots or queries.

### Unity adapters

Connect Unity-specific systems to the simulation.

Responsibilities:

- Input System actions.
- CharacterController or chosen motor.
- Physics queries.
- Territory world-coordinate conversion.
- Scene object registration.
- MonoBehaviour lifecycle.

### Presentation

Consumes state and events; does not own game rules.

Responsibilities:

- Character visuals and animation.
- Spray VFX.
- Territory materials.
- Reaction feedback.
- Camera.
- UI and result presentation.
- Audio and rumble.

### Infrastructure

Cross-cutting services.

Responsibilities:

- Logging and diagnostics.
- Local settings.
- Save schema.
- Build metadata.
- Performance markers.
- Dependency composition.

## Foundation assemblies

| Assembly | Unity dependency | Purpose |
|---|---|---|
| `EntropyTag.Domain` | No | Rules, value objects, deterministic reactions; `noEngineReferences` is enabled |
| `EntropyTag.Application` | No | Match orchestration, commands, events; currently depends only on Domain |
| `EntropyTag.Unity` | Yes | Character, physics, input, territory adapters |
| `EntropyTag.Presentation` | Yes | Camera, UI, VFX, audio, animation |
| `EntropyTag.Infrastructure` | Yes | Settings, diagnostics, build integration |
| `EntropyTag.Editor` | Editor only | Foundation setup, scene generation, validation, and build commands |
| `EntropyTag.Tests.EditMode` | Test only | Domain and application tests |
| `EntropyTag.Tests.PlayMode` | Test only | Scene, input, physics, rendering smoke tests |

These assemblies now exist under `EntropyTag/Assets/EntropyTag/`. The initial compilation, EditMode tests,
PlayMode test, and Windows development build pass. The architecture remains intentionally skeletal: the
assembly boundaries are real, while most gameplay types shown later in this document remain proposals until
their corresponding tasks begin.

Dependencies flow inward. Domain must never reference presentation.

## Runtime composition

```mermaid
sequenceDiagram
    accTitle: EntropyTag runtime frame flow
    accDescr: Unity input and bot decisions become intent, the simulation updates authoritative state, territory and player events are emitted, and presentation systems render the result.

    autonumber
    participant Input as Input and bots
    participant Adapter as Unity adapters
    participant Match as Match simulation
    participant Domain as Domain rules
    participant Events as Event stream
    participant View as Presentation

    Input->>Adapter: Movement, aim, spray intent
    Adapter->>Match: Submit intent
    Match->>Domain: Resolve movement-independent rules
    Match->>Domain: Resolve territory and reactions
    Domain-->>Match: Mutations and score changes
    Match-->>Events: Publish typed events
    Events-->>View: Paint, reaction, hit, phase events
    View->>View: Update material, VFX, audio, UI
```

## Core domain model

```mermaid
classDiagram
    accTitle: EntropyTag core domain model
    accDescr: A match owns teams, players, a territory field, score state, and match pressure while reaction rules convert element applications into territory results.

    class MatchState {
        +MatchPhase phase
        +float remainingTime
        +Tick()
    }

    class TeamState {
        +ElementId element
        +int bank
        +float territoryPercent
    }

    class PlayerState {
        +PlayerId id
        +TeamId team
        +StatusEffects effects
    }

    class TerritoryField {
        +TerritoryCell[] cells
        +Apply()
        +CalculateCoverage()
    }

    class TerritoryCell {
        +ElementId owner
        +TerrainState state
        +float strength
    }

    class ReactionResolver {
        +Resolve(application)
    }

    class MatchPressure {
        +GetActiveBounds()
        +Advance()
    }

    MatchState "1" *-- "2..3" TeamState
    MatchState "1" *-- "2..6" PlayerState
    MatchState "1" *-- "1" TerritoryField
    TerritoryField "1" *-- "*" TerritoryCell
    MatchState --> MatchPressure
    TerritoryField --> ReactionResolver
```

Names are proposals until the corresponding assemblies and types are created.

## Territory representation

### Logical ownership

Use a deterministic logical field as the authority:

- Grid or sparse tiled grid aligned to designated paintable surfaces.
- Cell stores owner, reaction state, strength, and optional timestamps.
- Scoring reads logical cells.
- Movement samples logical cells.
- Tests can construct fields without a GPU.

### Visual mask

Render a higher-resolution mask:

- RenderTexture, splat map, or texture array.
- Updated from logical mutations.
- Blended in a custom shader.
- May smooth edges without changing authoritative ownership.

### Why not make pixels authoritative

- GPU readback complicates deterministic scoring.
- Different hardware may produce small sampling differences.
- Testing becomes expensive.
- Networking becomes substantially harder.
- Visual resolution becomes coupled to game rules.

### Prototype decision gate

Compare two vertical-slice implementations:

1. CPU logical grid plus GPU visual stamping.
2. GPU mask with CPU logical coverage written from the same stamp commands.

Choose based on frame time, memory, edge quality, complexity, and testability.

## Character system

Start with a single motor abstraction:

- Camera-relative movement input.
- Separate aim vector.
- Ground detection.
- Acceleration and deceleration.
- Friendly/hostile terrain modifiers.
- External impulses for reactions.
- Status-effect modifiers.

Do not place match scoring or elemental resolution inside the character controller.

## Spray system

Proposed pipeline:

1. Input requests spray.
2. Resource/cooldown policy approves or rejects.
3. Aim solution produces origin and direction.
4. Physics query produces valid surface contacts.
5. Stamp command converts contacts into logical territory coordinates.
6. Domain resolver calculates resulting state and ownership.
7. Territory visual receives matching stamps.
8. Events drive VFX, audio, UI, and bank feedback.

The visible stream and authoritative contact must derive from the same aim solution.

## Configuration

Use ScriptableObjects only as authored configuration and asset references.

Recommended data:

- Element definitions.
- Reaction rules.
- Movement tuning.
- Spray tuning.
- Match rules.
- Bot profiles.
- Map metadata.
- Audio/VFX references.

At runtime, validate and copy configuration into immutable or controlled domain values. Avoid using mutable
global ScriptableObjects as live match state.

## Scene strategy

Initial scenes:

- `Bootstrap` - persistent services and transition entry.
- `FrontEnd` - title, settings, play flow.
- `VerticalSlice_Arena01` - first playable arena.
- `Test_*` scenes - isolated feature and performance validation.

Use additive loading only when a concrete need appears. The first slice can use simple scene transitions.

## Observability

- Structured logs for match start/end and invalid state.
- Unity Profiler markers around territory stamping, scoring, bot decisions, and rendering.
- On-screen development overlay for FPS, frame time, active stamps, territory cells, and bot state.
- Deterministic seed displayed in development builds.
- No silent catch-and-continue behavior for invalid configuration.

## Performance budgets

Initial PC targets:

- 60 FPS target.
- 16.67 ms total frame budget.
- Territory simulation target under 2 ms on representative hardware.
- Territory visual updates target under 3 ms during sustained spray.
- No per-frame managed allocations in core gameplay after warm-up.
- Bounded paint-mask memory documented per arena.

These are provisional and must be replaced with measured budgets after the first implementation.

## Networking boundary

Networking is deferred, but architecture should avoid obvious blockers:

- Commands represent player intent.
- Domain rules remain deterministic where practical.
- Match state has explicit ownership.
- Visual effects consume events and are not authoritative.
- Randomness is injected and seedable.

Do not build prediction, rollback, serialization, or transport in the first slice.

## Failure handling

- Invalid authored data fails at bootstrap with actionable messages.
- Missing required services block scene start rather than producing partial gameplay.
- Territory coordinate errors are asserted in development and safely rejected in release.
- Save/settings corruption falls back to versioned defaults and reports the failure.
- Build validation checks required scenes, input actions, and configuration assets.
