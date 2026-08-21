# Decisions and Risks

## Decision discipline

Major choices use short architecture decision records in this document until a dedicated ADR directory is
needed.

Status values:

- `PROPOSED`
- `ACCEPTED`
- `SUPERSEDED`
- `REJECTED`

## Decision log

### D-001: Unity production engine

- **Status:** ACCEPTED
- **Decision:** EntropyTag production development will use Unity.
- **Reason:** Installed tooling, C# support, third-person ecosystem, rendering flexibility, test tooling, and
  practical project automation.
- **Consequence:** The Pygame implementation becomes a mechanics reference.

### D-002: True 3D gameplay with retro presentation

- **Status:** ACCEPTED
- **Decision:** Use true 3D world, collision, camera, and characters with low-poly/pixel-inspired rendering.
- **Reason:** Pseudo-3D creates unnecessary constraints for third-person aim, surface painting, animation, and
  future multiplayer.
- **Consequence:** Art budgets remain constrained while gameplay retains full 3D capability.

### D-003: Offline vertical slice before networking

- **Status:** ACCEPTED
- **Decision:** Do not implement transport, prediction, rollback, accounts, or matchmaking in the first slice.
- **Reason:** Local gameplay and painting performance must be proven first.
- **Consequence:** Architecture preserves clean authority boundaries without networking implementation.

### D-004: Floor-only painting first

- **Status:** ACCEPTED
- **Decision:** Restrict the first slice to designated floor surfaces.
- **Reason:** Reduces coordinate, shader, camera, scoring, navigation, and performance complexity.
- **Consequence:** Arbitrary walls and moving surfaces require a later decision.

### D-005: Territory authority model

- **Status:** ACCEPTED
- **Decision:** CPU logical field is authoritative; GPU mask is visual.
- **Alternatives:** GPU-authoritative mask; discrete paintable panels.
- **Reason:** Deterministic CPU ownership supports scoring, bots, tests, replays, and future network authority
  without GPU readback.
- **Consequence:** The GPU representation must never become the source of gameplay truth. Territory Painting
  must still pass its performance and visual/logical alignment gate before this model is technically proven.

### D-006: Rendering pipeline

- **Status:** ACCEPTED
- **Decision:** Use URP `14.0.11`.
- **Reason:** Broad platform support and sufficient shader/custom-rendering flexibility.
- **Evidence:** Pipeline and renderer assets exist under `Assets/Settings/Rendering`; Graphics and every
  Quality level reference `URP_EntropyTag`; the user observed no pink materials.
- **Consequence:** Territory materials and rendering work may target URP. Pipeline changes now require a new
  decision record.

### D-007: First playable team count

- **Status:** ACCEPTED
- **Decision:** The first vertical slice uses Ice versus Fire. Bots are required so a single developer can
  test opposing-team territory and elemental interactions.
- **Reason:** Two teams reduce balance and readability variables while bots make interaction testing
  repeatable without requiring another human player.
- **Consequence:** Domain Rules implement Ice and Fire first. Bot work must provide at least an opposing-team
  test participant before interaction acceptance; exact final team size remains a later content decision.

### D-008: First pressure system

- **Status:** ACCEPTED
- **Decision:** The first slice uses a shrinking safe boundary.
- **Reason:** It provides a clear, continuously increasing match pressure while preserving the prototype's
  proven final-scramble structure.
- **Consequence:** Match Flow and Arena work must support a readable shrinking boundary. Closing lanes and a
  rotating score zone remain documented alternatives for later gray-box experiments.

### D-009: Nested Unity project root

- **Status:** ACCEPTED
- **Decision:** The Unity project root is `EntropyTag/` inside the Git repository root.
- **Reason:** Unity created the nested folder when the repository was selected as the parent location; the user
  explicitly chose to retain this path.
- **Consequence:** Repository documentation and TODOs remain at root; Unity `Assets`, `Packages`, and
  `ProjectSettings` live under `EntropyTag/`.

### D-010: First-slice minimum PC

- **Status:** ACCEPTED
- **Decision:** Target 1080p at approximately 60 FPS on GTX 1060/RX 580-class graphics, a four-core CPU, and
  8 GB system RAM.
- **Reason:** This older mainstream tier keeps the slice accessible and forces territory rendering,
  projectiles, bots, and effects to remain bounded.
- **Consequence:** Performance evidence must state test hardware and compare CPU, GPU, allocation, and memory
  results against this tier rather than relying only on the development machine.

### D-011: Required input devices

- **Status:** ACCEPTED
- **Decision:** Keyboard/mouse and gamepad are both required for the first slice. Generic gamepad bindings
  must support Xbox-style and PlayStation-style controllers.
- **Reason:** The developer explicitly requires both controller families and the accepted player sandbox
  already validates generic Input System bindings.
- **Consequence:** Every playable feature must remain operable with both device groups; device-specific glyphs
  and rebinding UI remain later UI/accessibility work.

### D-012: Non-lethal first-slice disruption

- **Status:** ACCEPTED
- **Decision:** Direct player hits can slow, knock back, or temporarily disable, but cannot eliminate players
  in the first slice.
- **Reason:** Territory transformation remains the primary interaction and players stay engaged throughout
  the short match.
- **Consequence:** Health, death, respawn, revive, and elimination scoring are excluded. Effects must remain
  brief, readable, and resistant to repeated hard-locking.

### D-013: First-slice bank behavior

- **Status:** ACCEPTED
- **Decision:** Team bank accumulates from contested actions and is displayed, but cannot be spent in the
  first slice.
- **Reason:** This proves deterministic earning, comeback tuning, UI communication, and results reporting
  without expanding scope into active ability design.
- **Consequence:** Domain Rules define bank awards for contested steals and reactions. Match UI and results
  display bank. Ability spending is deferred and bank does not replace territory percentage as the primary
  score.

### D-014: Initial match duration

- **Status:** ACCEPTED
- **Decision:** Target a two-minute match for the first vertical slice.
- **Reason:** Two minutes supports rapid iteration, immediate rematches, and the prototype's proven compact
  escalation arc.
- **Consequence:** Opening, contest, shrinking-boundary compression, and resolution must fit within two
  minutes. Exact phase thresholds remain data-driven tuning values.

### D-015: Working title

- **Status:** ACCEPTED
- **Decision:** Keep `EntropyTag` as the working title for the first vertical slice.
- **Reason:** The title already anchors the repository, documentation, prototype lineage, and project identity.
- **Consequence:** No rename work is required during the slice. Public naming can still be reviewed before a
  public release milestone if trademark or positioning evidence requires it.

### D-016: Future single-player platformer direction

- **Status:** ACCEPTED
- **Decision:** After the main vertical slice, explore an original comedic, rage-inducing precision-platformer
  single-player mode built around bounce, momentum, timing, and hostile-environment slapstick.
- **Reason:** The developer received feedback that a dedicated single-player experience may broaden the
  game's audience and wants a contrasting mode with strong replayable failure humor.
- **Consequence:** This does not expand the current vertical slice. Detailed design and implementation remain
  deferred under `TODO/18_SINGLE_PLAYER_PLATFORMER_FUTURE.todo`. Genre references are inspiration only;
  levels, characters, art, audio, terminology, narrative, and mechanical expression must remain original.

## Risk register

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---:|---:|---|
| R-001 | Territory painting misses frame budget | Medium | Critical | Prototype early; logical grid; bounded surfaces; profile continuously |
| R-002 | Visual mask diverges from logical score | Medium | Critical | Shared stamp commands; debug overlay; deterministic tests |
| R-003 | Scope expands toward Splatoon-scale production | High | Critical | Enforce slice exclusions and milestone gates |
| R-004 | Game feels derivative | Medium | High | Original elemental reactions, world, silhouettes, UI, audio, maps |
| R-005 | Movement and aim feel weak | Medium | Critical | Dedicated sandbox and playtest gate before content |
| R-006 | Reaction states become unreadable | High | High | Two teams first; patterns/icons; unique VFX/audio |
| R-007 | Bots require complex navigation over changing territory | Medium | High | Static NavMesh plus territory-aware utility; no dynamic nav rebuild initially |
| R-008 | Bank creates runaway leader | Medium | High | Reward contested actions; telegraph spending; comeback gain for steals |
| R-009 | Art pipeline cannot support 3D scope | Medium | Critical | Shared rig, modular kit, constrained low-poly style, placeholders |
| R-010 | Networking retrofit is expensive | Medium | High | Explicit authority and commands; defer actual networking |
| R-011 | Unity version/package instability | Low | High | Lock editor and packages; review upgrades deliberately |
| R-012 | Third-party asset licensing blocks release | Medium | High | License registry and isolated ThirdParty folder |
| R-013 | Local context store disappears during branch operations | Medium | Medium | `.agent-context` is ignored; verify after branch changes; persist zmem episodics |

## Open product questions

1. What exact player count is required for the first public build?
2. Is local split-screen a launch requirement?
3. Should player disruption include elimination?
4. Does bank power active abilities in the first public build?
5. Is Water included before or after the first polished map?
6. What original world and character premise supports the elements?

## Change protocol

When accepting a decision:

1. Record evidence.
2. Update status and consequences.
3. Update affected design/architecture documents.
4. Update related TODOs.
5. Update `.agent-context`.
