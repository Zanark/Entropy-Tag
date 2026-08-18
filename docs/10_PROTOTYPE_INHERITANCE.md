# Prototype Inheritance

## Historical location

```text
C:\devdesk\gamedesk\EntropyTag_Prototype
```

The prototype is a separate Python/Pygame project. It remains useful as a verified mechanics reference but is
not the foundation of the Unity architecture.

## What it proved

- Three elemental team identities can coexist.
- Territory ownership can affect movement.
- Multi-step states such as Frozen, Puddle, and Mist are understandable in data.
- Bots can keep a match active.
- Bank score can record contested conversions.
- A shrinking arena creates a two-minute escalation arc.
- Territory percentage can resolve a winner.

## What carries forward

| Prototype concept | Unity interpretation |
|---|---|
| Ice, Water, Fire | Data-driven original team identities |
| Tile ownership | Logical territory field mapped onto 3D surfaces |
| Reaction table | Pure C# reaction resolver |
| Friendly speed | Character motor samples territory |
| Spray rectangle | Directional 3D spray contacts and stamps |
| Bank score | Contested-action resource and tie information |
| Ring shrink | Candidate 3D match-pressure system |
| Simple bots | Seedable utility-based bot foundation |
| Renderer separation | Domain/application/presentation layering |

## What does not carry forward

- Pygame rendering.
- Python implementation.
- Frame-count-only timing.
- Proximity-based player hits disconnected from visible spray.
- Order-dependent collision behavior.
- HUD layout.
- Hard-coded team/player construction.
- Unused tile strength and spread bonus.
- Committed bytecode or virtual environment.
- Pixel/grid dimensions as production constraints.

## Porting rule

No Python file is mechanically translated line-by-line.

For each inherited mechanic:

1. Write the intended rule in the Unity design documentation.
2. Resolve prototype defects and ambiguities.
3. Add pure C# tests.
4. Implement the domain rule.
5. Integrate with Unity presentation.
6. Validate through play.

## Historical value

The prototype is successful because it reduced uncertainty cheaply. It should remain stable enough to answer
"what did the original rule do?" while Unity development answers "what should the production rule become?"

