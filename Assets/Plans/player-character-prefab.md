# Project Overview
- Game Title: XmasShooter
- High-Level Concept: Fast-paced First-Person Shooter prototype with responsive character controller, Cinemachine-driven first-person camera, and New Input System controls.
- Players: Single-player character controller base (usable in multiplayer prototyping).
- Inspiration / Reference Games: Classic arena/tactical FPS (Quake, Half-Life, CS:GO).
- Tone / Art Direction: Prototyping greybox / blockout aesthetic with ProBuilder geometry.
- Target Platform: StandaloneWindows64 (PC).
- Screen Orientation / Resolution: Landscape 1920x1080.
- Render Pipeline: Universal Render Pipeline (URP - `PC_RPAsset`).

# Game Mechanics
## Core Gameplay Loop
The player spawns into the environment, explores 3D terrain/structures via responsive first-person movement (WASD), navigates verticality using jumping, sprints across open distances with Shift, and looks around smoothly with mouse-driven camera controls. Dynamic camera FOV and event hooks provide kinesthetic speed feedback and visual impact.

## Controls and Input Methods
Configured via Unity's New Input System (`InputSystem_Actions.inputactions`):
- Movement: `W`, `A`, `S`, `D` (Vector2 `Move` action) relative to player facing direction.
- Sprint: `Left Shift` (Button `Sprint` action) accelerating movement when moving forward.
- Look: Mouse Delta (Vector2 `Look` action) with customizable mouse sensitivity and vertical pitch clamping (-85° to +85°).
- Jump: `Space` (Button `Jump` action) triggering vertical velocity calculation when grounded.
- Cursor Locking: Mouse cursor locked to the center of screen on gameplay start and toggleable via `Escape`.

# UI
Minimal in-editor prototyping HUD:
- Center screen crosshair/reticle indicator (standard FPS UI overlay or Canvas reticle).
- On-screen controls reminder / debug readout for position, velocity, and grounded state (optional debug toggle).

```
+-------------------------------------------------------+
|                                                       |
|                                                       |
|                                                       |
|                         [+]                           |
|                      (Reticle)                        |
|                                                       |
|                                                       |
| [WASD: Move | Space: Jump | Mouse: Look]              |
+-------------------------------------------------------+
```

# Key Asset & Context
### 1. Existing Dependencies Verified
- `com.unity.cinemachine`: Already installed (`3.1.5`).
- `com.unity.probuilder`: Already installed (`6.0.9`).
- `com.unity.inputsystem`: Already installed (`1.19.0`).
- Existing Input Action Asset: `Assets/InputSystems/InputSystem_Actions.inputactions`.

### 2. Assets to Create / Modify
- Script: `Assets/Scripts/Gameplay/Player/FPSMovementController.cs` (Modify)
  - Adds sprint input handling (`Sprint` action from `InputSystem_Actions`), sprint speed setting, sprint acceleration/deceleration interpolation.
  - Exposes decoupled events: `event Action<bool> OnSprintStateChanged` and `event Action<float> OnSprintProgress` (0.0 to 1.0) for zero-coupling with audio, camera, and future VFX.
- Script: `Assets/Scripts/Gameplay/Player/FPSCameraEffects.cs` (Create)
  - Dedicated camera effects controller subscribing to `OnSprintProgress` (or referencing `FPSMovementController`).
  - Serialized FOV properties (`baseFov`, `sprintFov`, `fovTransitionSpeed`, `fovCurve`).
  - Smoothly updates `CinemachineCamera.Lens.FieldOfView`.
  - Scalable VFX architecture: exposes `UnityEvent<float>` / interfaces so future VFX (e.g., speed lines, chromatic aberration, motion blur, particle trails) can be plugged in via inspector or code without altering movement logic.
- Prefab: `Assets/Prefabs/Player/FPSPlayer.prefab` (Modify)
  - Attach `FPSCameraEffects` component, serialize FOV values, and link references.
- Scene Integration:
  - Update `FPSPlayer` instance in `Assets/Scenes/Prototype1.unity`.

# Implementation Steps

### Step 1: Create FPSMovementController Script
- Description: [COMPLETED] Basic movement, jumping, mouse look, gravity, and input setup.
- Assigned role: developer
- Dependencies: None
- Parallelizable: Yes

### Step 2: Create Player Character Prefab
- Description: [COMPLETED] Constructed `FPSPlayer.prefab` with 1.8m ProBuilder cube and CinemachineCamera.
- Assigned role: developer
- Dependencies: Step 1
- Parallelizable: No

### Step 3: Setup Scene Camera and Test Instance in Prototype1 Scene
- Description: [COMPLETED] Added CinemachineBrain to Main Camera and instantiated player in scene.
- Assigned role: developer
- Dependencies: Step 2
- Parallelizable: No

### Step 4: Verification & Framing Check
- Description: [COMPLETED] Validated camera priority, CinemachineBrain binding, and CharacterController dimensions.
- Assigned role: developer
- Dependencies: Step 3
- Parallelizable: No

### Step 5: Extend FPSMovementController with Sprint & Event Architecture
- Description: Enhance `FPSMovementController.cs` to read the `Sprint` action from `PlayerInput` / `InputSystem_Actions`. Implement forward-movement condition (player must be moving forward to sprint), smooth speed interpolation between walk speed (default 6 m/s) and sprint speed (default 10 m/s), calculate sprint progress factor (0.0 to 1.0), and broadcast `OnSprintStateChanged(bool)` and `OnSprintProgress(float)`.
- Assigned role: developer
- Dependencies: None
- Parallelizable: Yes

### Step 6: Create Scalable FPSCameraEffects Script
- Description: Create `FPSCameraEffects.cs` in `Assets/Scripts/Gameplay/Player/`. Expose serializable properties: `m_NormalFOV` (default 70°), `m_SprintFOV` (default 82°), `m_FovTransitionSpeed` (default 10f), and an optional `AnimationCurve`. Interpolate `CinemachineCamera.Lens.FieldOfView` based on sprint progress. Include serialized `UnityEvent<float> OnSprintVFXIntensityChanged` to allow dropping in future VFX (speed lines UI, URP Volume motion blur, audio pitch, particles) without changing any code.
- Assigned role: developer
- Dependencies: Step 5
- Parallelizable: No

### Step 7: Update FPSPlayer Prefab & Scene Instance
- Description: Add `FPSCameraEffects` to `FPSPlayer.prefab` (and scene instance), configure serialized values, link `CinemachineCamera` and `FPSMovementController` references, and save the updated prefab and scene.
- Assigned role: developer
- Dependencies: Step 6
- Parallelizable: No

### Step 8: Verification & Sprint Testing
- Description: Test compilation, inspect serialized fields, and verify sprint state transitions and Cinemachine FOV change dynamically via edit/playmode tests.
- Assigned role: developer
- Dependencies: Step 7
- Parallelizable: No

# Verification & Testing
1. Input Verification:
   - Ensure `Sprint` action (`Left Shift`) is detected and correctly mapped from `InputSystem_Actions`.
2. Movement Speed Verification:
   - Verify walk speed is 6.0 m/s, sprint speed reaches 10.0 m/s.
   - Verify sprint only engages when moving forward (not while idle or moving backward).
3. Camera FOV Verification:
   - Verify base FOV is 70° at rest or during normal walk.
   - Verify FOV smoothly scales up to sprint FOV (82°) while sprinting, and smoothly returns to 70° when Shift is released or movement stops.
4. VFX Scalability Check:
   - Check that `OnSprintStateChanged`, `OnSprintProgress`, and `OnSprintVFXIntensityChanged` fire properly, allowing any future particle, post-processing, or audio components to hook in without modifying character movement.
