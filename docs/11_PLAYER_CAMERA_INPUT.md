# Player, Camera, and Input Sandbox

## Purpose

The selected movement sandbox evaluates the first playable third-person control stack before production
territory rules, weapons, animation, or final art are added.

Open:

`EntropyTag/Assets/EntropyTag/Scenes/Tests/Sandbox_PlayerMovement.unity`

Then enter Play Mode.

## Camera comparison scenes

Variant 05, Free Aim Continuous Follow, is the selected foundation. The original eight scenes remain under
`CameraVariants/` until the selected model completes its final interactive acceptance check.

1. `Sandbox_Camera_01_CenteredImmediate` - fixed center reticle and immediate camera response.
2. `Sandbox_Camera_02_CenteredSmooth` - fixed center reticle with short camera damping.
3. `Sandbox_Camera_03_CenteredShoulder` - centered camera that shifts over the shoulder while Aim is held.
4. `Sandbox_Camera_04_FreeAimEdgeTurn` - free reticle; camera rotates only outside a central safe zone.
5. `Sandbox_Camera_05_FreeAimContinuousFollow` - free reticle while camera continuously follows input.
6. `Sandbox_Camera_06_ElasticTether` - reticle moves inside a radius and pulls the camera like a spring.
7. `Sandbox_Camera_07_SoftZoneRecenter` - free reticle with edge turn that recenters during rotation.
8. `Sandbox_Camera_08_CenteredCinematicSpring` - fixed center reticle with intentionally heavier camera lag.

Every scene uses the same expanded movement gym, movement mechanics, paint-splat shooter, and thin cuboid
torso with a sphere head. Only camera/crosshair behavior changes.

The movement gym contains a larger floor, climb wall, raised platform, ramp, narrow beam, graduated steps,
and a low-clearance slide tunnel.

## Controls

| Intent | Keyboard and mouse | Gamepad |
|---|---|---|
| Move | `WASD` | Left stick |
| Turn camera / aim | Mouse movement | Right stick |
| Jump / wall jump | `Space` | A / Cross |
| Slide | `Left Ctrl` | B / Circle |
| Aim | Right mouse button | Left trigger |
| Fire paint projectiles | Left mouse button | Right trigger |
| Pause intent | `Escape` | Start |

Hold movement toward the climb wall to climb it. Jumping while climbing pushes the player away from the wall.
Fire launches pooled test projectiles through the shared aim solution. On impact they become simple cyan
splats that remain on the contacted floor, wall, or platform until the finite splat pool wraps.

## Runtime flow

```mermaid
flowchart LR
    accTitle: Player sandbox runtime flow
    accDescr: Mouse or right stick moves a free reticle while continuously rotating the camera, and the shared physics aim solution drives feedback and persistent impact splats.

    devices[Keyboard, mouse, or gamepad]
    input[PlayerInputSource]
    settings[Persisted sensitivity and inversion]
    motor[ThirdPersonMotor]
    camera[ThirdPersonCameraRig]
    experiment[CameraAimExperimentController]
    aim[ThirdPersonAimSolver]
    reticle[AimReticlePresenter]
    shooter[TestProjectileShooter]
    splats[Persistent pooled splats]
    spray[Future spray contact]

    devices --> input
    settings --> input
    input --> motor
    input --> experiment
    experiment --> camera
    experiment --> aim
    camera --> aim
    aim --> reticle
    aim --> shooter
    shooter --> splats
    aim --> spray
```

## Components

- `PlayerInputSource` binds the shared Input Action Asset once and reads movement, look, aim, fire, jump, and
  slide without per-frame action lookup.
- `ThirdPersonMotor` uses `CharacterController` for camera-relative acceleration, deceleration, gravity,
  grounded movement, jumping, wall climbing and wall jumping, sliding with reduced collision height,
  rotation, external impulses, and speed modifiers.
- `CameraAimExperimentController` contains eight isolated presets and serializes the selected mode into each
  scene. It supports centered, shoulder, free-reticle, edge-zone, tether, recentering, and spring behavior.
- `ThirdPersonCameraRig` applies orbit look, pitch limits, and sphere-cast collision without shifting into a
  shoulder view.
- `ThirdPersonAimSolver` raycasts through screen center and exposes one `AimSolution`.
- `AimReticlePresenter` displays the reticle and contact marker from that same solution.
- `TestProjectileShooter` prewarms reusable projectile and splat pools, fires toward the shared aim solution,
  and leaves a simple persistent splat at each collision without allocating a new projectile per shot.
- `PlayerSettingsStore` persists versioned mouse sensitivity, gamepad sensitivity, and vertical inversion in
  local `PlayerPrefs`.
- `PlayerSandboxDiagnostics` records frame allocation and main-thread timing counters.

## Validation

Automated validation covers:

- EditMode: 4 passed, 0 failed.
- PlayMode: 10 passed, 0 failed.
- Keyboard/mouse and gamepad schemes are present.
- Camera collision prevents the camera remaining behind the sandbox wall.
- Motor impulse and speed-modifier hooks operate.
- Settings survive a local save/load cycle.
- Test projectiles activate from the pool and travel along the shared aim solution.
- Jump, wall climb, wall jump, and reduced-height slide behavior operate in the generated movement gym.
- Projectile impacts create persistent pooled splats.
- Firing does not cause the camera collision rig to collapse toward the player.
- A 300-iteration warm player loop allocates zero managed bytes and averages below 1 ms per isolated update.
- Windows development build succeeds and explicitly excludes all eight comparison scenes.

Variant 05 is selected. The final acceptance gate is one combined roughly-60-FPS playtest of its camera,
movement gym, jump, wall climb, slide, and paint-splat behavior.

Generic Input System `<Gamepad>` bindings cover both Xbox-style and PlayStation-style controllers; any
platform-specific glyphs or naming belong to the later UI/accessibility TODO.

## Current limitations

- Placeholder thin cuboid torso, sphere head, floor, wall, target, reticle, and lighting only.
- No animation, jumping, sprinting, real combat, spray, pause flow, rebinding UI, or settings UI.
- Test splats have no ownership, territory conversion, elemental behavior, damage, or gameplay scoring.
- Input tuning values are persisted by the service but not yet exposed through a menu.
- The sandbox is a technical proof, not a production arena.
