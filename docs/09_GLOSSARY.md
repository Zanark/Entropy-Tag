# Glossary

| Term | Meaning in EntropyTag |
|---|---|
| Active arena | Territory currently eligible for movement and scoring |
| Adapter | Unity-facing component translating engine services or input into application commands |
| Aim solution | Shared origin, direction, and contact used by gameplay and visible spray |
| Application layer | Match orchestration around pure domain rules |
| Arena | Authored playable 3D map |
| Assembly definition | Unity `.asmdef` boundary controlling compilation and dependencies |
| Authoritative | Source of truth used to decide game outcome |
| Bank | Team resource earned from contested conversions and reactions |
| Bot profile | Data describing a bot's priorities, timing, and accuracy |
| Build gate | Required evidence before a milestone advances |
| Cell | Logical territory unit used for ownership and scoring |
| Compression | Match phase that forces teams into closer conflict |
| Contact stamp | Territory mutation request produced by a valid spray contact |
| Counter | Elemental matchup producing a specific advantage or status |
| Domain | Pure rules and state independent from Unity |
| Element definition | Data describing an element's identity, tuning, visuals, and references |
| Event | Immutable notification that a meaningful gameplay change occurred |
| Friendly territory | Territory owned by the player's team |
| Game feel | Sensory and control response that makes actions satisfying and readable |
| Gray box | Simple gameplay geometry used before final art |
| Hostile territory | Territory owned by an opposing team |
| Intent | Requested movement, aim, spray, or ability action |
| Logical field | Authoritative territory representation |
| Match phase | Opening, contest, compression, resolution, or another explicit state |
| Match pressure | System preventing indefinite passive play |
| Mist | Reaction state created by selected Ice/Fire interactions |
| MonoBehaviour | Unity component attached to GameObjects |
| Paintable surface | Authored surface registered for territory stamping |
| Puddle | Water-related reaction terrain inherited conceptually from the prototype |
| Presentation | Visual, audio, animation, camera, and UI response |
| Pressure meter | Proposed resource limiting continuous spraying |
| Reaction | Deterministic result of applying an element to an existing state |
| RenderTexture | GPU texture that can receive runtime territory visuals |
| Reticle | Aim indicator |
| Safe boundary | Active playable limit when using a shrinking-arena pressure system |
| Seed | Value making random decisions reproducible |
| ScriptableObject | Unity asset used for authored configuration and references |
| Slice | Small production-quality proof of the intended game |
| Splat map | Texture data blending territory materials |
| Stamp command | Deterministic request to mutate territory at coordinates |
| Status effect | Temporary modifier applied to a player |
| Territory coverage | Percentage of active logical cells owned by a team |
| Territory history | Current state reflecting prior elemental applications |
| Territory mask | Visual texture representing ownership or reaction states |
| Third party | Asset or package not authored for EntropyTag |
| Tuning | Data values controlling feel and balance |
| Unity adapter | MonoBehaviour or service connecting Unity systems to application logic |
| URP | Unity Universal Render Pipeline |
| Vertical slice | Complete narrow demonstration of production gameplay |
| Visual authority | Incorrect model where rendered pixels decide rules; avoided by current proposal |
| VFX | Visual effects communicating spray, reactions, status, and match events |
| World-to-territory mapping | Conversion from 3D contact position to logical territory coordinates |

