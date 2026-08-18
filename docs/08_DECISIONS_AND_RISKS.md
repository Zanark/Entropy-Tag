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

- **Status:** PROPOSED
- **Decision:** CPU logical field is authoritative; GPU mask is visual.
- **Alternatives:** GPU-authoritative mask; discrete paintable panels.
- **Decision evidence needed:** Performance and alignment proof from Gate 3.

### D-006: Rendering pipeline

- **Status:** PROPOSED
- **Decision:** Use URP for the first project.
- **Reason:** Broad platform support and sufficient shader/custom-rendering flexibility.
- **Decision evidence needed:** Confirm Unity version, target platform, and required territory/VFX capabilities.

### D-007: First playable team count

- **Status:** PROPOSED
- **Decision:** Ice versus Fire for the vertical slice; add Water after the loop is stable.
- **Reason:** Two-team balance and presentation reduce variables.

### D-008: First pressure system

- **Status:** PROPOSED
- **Options:** Shrinking safe boundary, closing lanes, rotating score zone.
- **Decision evidence needed:** Gray-box map tests.

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
3. What PC hardware defines the minimum target?
4. Should player disruption include elimination?
5. Does bank power active abilities in the first public build?
6. How is pressure represented in 3D?
7. Is Water included before or after the first polished map?
8. What original world and character premise supports the elements?

## Change protocol

When accepting a decision:

1. Record evidence.
2. Update status and consequences.
3. Update affected design/architecture documents.
4. Update related TODOs.
5. Update `.agent-context`.

