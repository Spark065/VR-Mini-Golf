# Scene 2 VR Mini Golf Setup

## Meta Building Blocks level transition

`Assets/Scripts/GolfHole.cs` owns the level-complete flow. When the tagged golf ball enters the hole trigger, the existing sink/effect behavior runs, gameplay pauses with `Time.timeScale = 0`, and a Meta ray-compatible World Space Canvas appears in front of the active camera.

- The runtime Canvas contains exactly one default uGUI Button labelled `Bir Sonraki Bölüme Geç`.
- `MetaLevelTransitionUI` adds `PointableCanvas`, `RayInteractable`, a clipped plane surface, and `PointableCanvasModule` for the existing Meta comprehensive rig.
- The controller/hand ray continues updating while gameplay is paused; no Unity XR Interaction Toolkit component or generic XR input binding is used.
- The button loads the next enabled Build Settings scene asynchronously and ignores repeated clicks while loading.
- The default real-time delay is 0.2 seconds. The last scene remains paused safely because `Wrap To First Scene` defaults to false.

No manual Canvas wiring is required in either scene. On each hole GameObject, keep `GolfHole` and a trigger Collider. The optional Inspector fields are `Level Complete UI` (leave empty for runtime creation), `Pause Gameplay On Complete`, `Scene Load Delay`, and `Wrap To First Scene`. `XRSceneTransition` now only remains as a manual compatibility bridge and delegates to `GolfHole`; it does not create an input action.

## Build Settings

The enabled scene order is:

1. `Assets/Scenes/SampleScene.unity`
2. `Assets/Scenes/Scene 2.unity`

The transition therefore loads Scene 2 from SampleScene and safely does nothing on Scene 2 unless wrapping is enabled.

## Scene 2 hierarchy

`Scene2_Course` contains:

- `Course_Pieces`: start, straight sections, L corner, and round hole prefab instances.
- `Obstacles`: one diamond and one block course-module prefab instance.
- `Gameplay`: flag and the `Hole_Trigger` with `GolfHole`.
- `Safety_Colliders`: invisible side protection and tagged out-of-bounds recovery floors.

The earlier `golf course` root is preserved as inactive `LegacyCourse_Disabled`, and the old fallback `Plane` is inactive. Existing ball, club, GameManager, XR camera rig, and directional light objects are reused and repositioned.

Scene 2 uses the same Meta `OVRComprehensiveInteractionRig` settings as Scene 1 for Dynamic Resolution and Passthrough. Its player start transform remains course-specific so the player faces the first lane.

The course can be rebuilt idempotently from `Tools > VR Mini Golf > Rebuild Scene 2 Course`.

## Device smoke test

1. Start Play Mode in SampleScene.
2. Put the golf ball into the hole and confirm that ball/gameplay physics stop while the Meta controller ray remains responsive.
3. Point at `Bir Sonraki Bölüme Geç`, press it, and confirm one transition to Scene 2.
4. Click repeatedly during loading; only one scene load should occur.
5. Complete the hole in Scene 2 and confirm pressing the button causes no scene-load error on the final Build Settings scene.
6. Strike the ball through both obstacles, around the L corner, and into the round hole.
7. Verify the safety rails keep normal shots on the course and that a fallen ball returns via the existing out-of-bounds logic.
