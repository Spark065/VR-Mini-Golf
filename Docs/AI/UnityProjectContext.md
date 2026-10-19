# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `C:/Users/sayaz/Desktop/Unity games/OyunDenemeleri1/XR deneme`
- Likely project: single-player VR mini-golf prototype using Meta/Oculus interaction APIs. **Likely**, based on the gameplay scripts and scene contents.
- Last analyzed: 2026-08-27
- Last analyzed commit: `8ca13d2` (repository status was read-only checked; Git LFS emitted an access warning while checking status).

## Confirmed Environment

- Unity version: 6000.4.4f1, revision `360f97ecca93`.
- Render pipeline: Universal Render Pipeline 17.4.0. The package and authored `Mobile_RPAsset`/`PC_RPAsset` settings confirm URP; the global GraphicsSettings default pipeline reference is null.
- Input: Unity Input System 1.19.0 is active (`activeInputHandler: 1`). `VRGolfClub` supports the scene's existing `OVRGrabbable` and `XRGrabInteractable` paths plus their haptic APIs.
- XR: OpenXR 1.16.1, XR Interaction Toolkit 3.4.1, XR Management 4.7.0, Meta XR SDK 205.0.0.
- Target: Meta Quest Android build profile exists at `Assets/Settings/Build Profiles/Meta Quest.asset`; exact supported Quest models are unknown.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Rendering | URP 17.4.0 with mobile and PC assets | Confirmed | `Packages/manifest.json`, `Assets/Settings/*RPAsset.asset` |
| XR | Meta XR SDK, OpenXR, XR Interaction Toolkit, XR Management | Confirmed | `Packages/manifest.json`, `SampleScene.unity` |
| Interaction | Meta Building Blocks are serialized into the main scene. The active comprehensive rig includes Meta controller near-grab and distance-grab interactors/ray feedback. | Confirmed | `Assets/Scenes/SampleScene.unity`, Meta `BaseInteractors.prefab` |
| UI | uGUI and TextMesh Pro | Confirmed | `Packages/manifest.json`, `GolfBallController.cs`, `SampleScene.unity` |
| AI/tools | Unity AI Assistant 2.9.0-pre.2 and Inference 2.6.1 are installed; generated project files also contain Unity AI MCP assemblies | Confirmed | `Packages/manifest.json`, generated `.csproj` files |
| Navigation/multiplayer | AI Navigation and Multiplayer Center packages are present; no first-party gameplay networking was found | Confirmed | `Packages/manifest.json`, `Assets/Scripts/*.cs` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/Scripts` | Golf club/ball gameplay, hole, scoring, and scene-transition scripts | Confirmed | `Assets/Scripts/*.cs` |
| `Assets/Scenes` | Two enabled gameplay scenes | Confirmed | `Assets/Scenes/SampleScene.unity`, `Assets/Scenes/Scene 2.unity`, build settings |
| `Assets/Prefabs` | Prefabs and golf/UI assets | Confirmed | Directory listing |
| `Assets/Physics` | Mini-golf ball, ground, and obstacle physics materials | Confirmed | `Assets/Physics/*.physicMaterial` |
| `Assets/Settings` | URP assets, volume profiles, and Meta Quest build profile | Confirmed | Directory listing |
| `Assets/Downloads` | Imported sports-kit demo content | Confirmed | `Assets/Downloads/FreeSportsKIt_SA` |
| `Assets/_Recovery` | Recovery scenes, excluded from Build Settings | Confirmed | Directory listing and build settings |
| `Assets/Oculus`, `Assets/XR`, `Assets/XRI`, `Assets/Plugins` | XR/vendor and plugin content | Confirmed as present; internal ownership varies | Directory listing |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `Assembly-CSharp` | First-party runtime gameplay | UnityEngine, SceneManagement, TMPro, Meta OVR/Interaction types | No first-party `.asmdef` or `.asmref` files found; package assemblies are separate. The mini-golf runtime scripts do not import XR Interaction Toolkit. |
| Package assemblies | Meta XR, Unity XR, AI, and editor/runtime tooling | Package-defined | Generated `.csproj` files are not treated as source architecture. |

## Scenes And Startup Flow

- Enabled build scenes are `SampleScene` (index 0) and `Scene 2` (index 1).
- `SampleScene` serializes two Meta `OVRComprehensiveInteractionRig` instances; one is active and one is disabled. The active rig resolves through `OVRComprehensiveInteractors` to Meta `GrabInteractor` and `ControllerDistanceGrabInteractor` prefabs.
- Scene composition includes Meta Building Blocks, a golf club, `GolfHole`, the legacy `GolfBallController` component (derived from `BallController`), `GameManager`, and TextMesh Pro UI.
- `GameManager` is a `DontDestroyOnLoad` singleton. The enabled build list contains `SampleScene` at index 0 and `Scene 2` at index 1; `GolfHole` owns the Meta UI-driven transition between them. Scene 2 replaces the next-level action with a final result and clean-session restart to build index 0.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Scene-composed MonoBehaviours | Gameplay is split among `VRGolfClub`, `BallController`, `GolfHole`, and `GameManager` with Inspector references | Confirmed | `Assets/Scripts/*.cs`, `SampleScene.unity` |
| Persistent game session | `GameManager.Instance` owns a scene-independent `MiniGolfSession`; completed holes are recorded idempotently by build index, totaled across both scenes, and reset before restart scene activation | Confirmed | `MiniGolfSession.cs`, `GameManager.cs`, `GolfHole.cs` |
| Physics gameplay | Rigidbody velocity, strength-scaled particles/haptics, speed-dependent mirror wall reflection, persistent-contact separation, rolling friction, and hole triggers drive the golf loop | Confirmed | `VRGolfClub.cs`, `BallController.cs`, `GolfHole.cs` |
| Club interaction | `VRGolfClub` uses the existing Meta `OVRGrabbable`/`OVRInput` path, keeps the released putter kinematic and gravity-free, adds a held-state ground clearance guard, and drives an optional World Space sprite-frame power bar without changing putt or haptic calculations | Confirmed | `VRGolfClub.cs`, `Assets/Prefabs/chargebar2` |
| VR UI | `GolfHole` provisions a World Space uGUI Canvas with Meta `PointableCanvas`, `RayInteractable`, and `PointableCanvasModule`; Scene 1 shows the next-level action, while Scene 2 shows both hole counts, total strokes, `max(0, 100 - total * 5)`, and a ray-clickable restart action | Confirmed | `GolfHole.cs`, `MetaLevelTransitionUI.cs` |
| Course checkpoint | `BallController` records a valid ground position in world coordinates, ignores UI and `OutOfBounds` surfaces, and debounces a velocity-clearing return to the last valid point | Confirmed | `BallController.cs` |
| Serialization compatibility | `GolfBallController` inherits `BallController`; migrated UI field names use `FormerlySerializedAs` | Confirmed | `GolfBallController.cs`, `BallController.cs` |
| Async/networking/ECS | No first-party usage found | Confirmed for inspected scripts; broader vendor content not audited | `Assets/Scripts/*.cs` |

## Coding Conventions

- Namespace style: global namespace in all inspected first-party scripts.
- Serialized fields: mixed style; `VRGolfClub` favors private `[SerializeField]` fields, while other scripts expose public Inspector fields.
- Lifecycle/style: Unity lifecycle methods with compact methods, comments/tooltips, and Turkish UI/comments; no consistent XML documentation or nullable annotations observed.
- Async: `BallController` uses a coroutine for sinking; no task-based async pattern observed.

## Testing And Validation

- EditMode tests: none found in first-party assets.
- PlayMode tests: none found in first-party assets.
- Unity Test Framework 1.6.0 and performance testing support are installed.
- Unity's own script pipeline compiled `Assembly-CSharp` and `Assembly-CSharp-Editor` successfully on 2026-08-25 after the persistent-contact wall-bounce fix. An isolated Play Mode physics probe launched a real `BallController` sphere between two parallel colliders: four consecutive rebounds passed, minimum measured exit-normal speed was `1.676 m/s`, and no sticking interval over `0.25 s` occurred. The temporary probe objects and script were removed after the run. No Quest headset/controller stroke test was performed.
- On 2026-08-26 the changed runtime scripts and the new scene-setup editor utility passed isolated Roslyn compilation against the installed Unity 6.4, Meta SDK, uGUI, and TextMesh Pro assemblies. A live Unity compile/Play Mode/Quest controller run was not available while the Editor was backgrounded behind another full-screen application; `Library/CodexMiniGolfSetup.pending` remains as the one-shot scene persistence request.
- On 2026-08-27 the persistent-session/final-result changes passed an isolated .NET `netstandard2.1` compile against the installed Unity 6.4, Meta Interaction, uGUI, and TextMesh Pro assemblies with zero errors. Four focused `MiniGolfSession` checks passed (two-hole total, idempotent replacement, missing-hole snapshot, clean reset). Live Unity MCP Console access was revoked, so no Play Mode or headset validation was performed for this change.
- On 2026-08-27 the VR sprite-frame power-bar change passed isolated runtime and `UNITY_EDITOR` Roslyn compilation for every first-party runtime script with zero errors. All 14 referenced `Assets/Prefabs/chargebar2` sprites were present. The full generated-solution fallback remains blocked by two package-level URP `PassesData.cs` compiler errors, and live Unity MCP access is still revoked; no Play Mode or Quest visual check was performed.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| Unity package/MCP presence | available in project files | `com.unity.ai.assistant` and generated Unity AI MCP assemblies |
| Live Unity connection/editor/console/scene/build-settings/test/profiler tools | unavailable to this task | No callable Unity MCP capabilities exposed in the current tool set |

## Important Constraints

- Do not modify Unity assets, packages, project settings, or scenes without a scoped task.
- Preserve existing user changes; no source assets were modified during onboarding.
- Avoid treating generated directories (`Library`, `Temp`, `Logs`, `obj`) and generated `.csproj` files as architecture evidence.
- The active Quest profile and XR scene setup make device testing important; exact device and refresh rate remain unknown.
- `SampleScene` was open with unsaved user changes during the 2026-08-25 implementation. Script/assets were changed, but the scene was not silently saved.
- Both enabled scenes already serialize one `GameManager`, one `GolfHole`, one `GolfBallController`, and one `VRGolfClub`. Runtime guards make both scenes functional even before the pending Editor setup persists the 42.67 mm ball and 0.90 m putter overrides.

## Unknowns And Confidence

- **Unknown:** exact Quest device matrix, graphics quality selection, and runtime XR loader state.
- **Unknown:** whether the installed Unity AI/MCP package is configured to accept an external connection; package presence alone does not prove a live MCP endpoint.
- **Unknown:** real Quest controller interaction, exact hand pose alignment, club release trajectory, and measured roll/rebound distances; these require a connected headset or simulator and representative course geometry.

## Source Files Inspected

- `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/GraphicsSettings.asset`
- `Packages/manifest.json`, `Packages/packages-lock.json`
- `Assets/Scripts/VRGolfClub.cs`, `BallController.cs`, `GolfBallController.cs`, `GolfHole.cs`, `GameManager.cs`, `MiniGolfSession.cs`, `MetaLevelTransitionUI.cs`
- `Assets/Editor/MiniGolfPhysicsSetupEditor.cs`, `Scene2CourseBuilder.cs`
- `Assets/Scenes/SampleScene.unity`
- `Assets/Prefabs/uwu.prefab`, `golf_ball.prefab`
- Meta Interaction SDK 205 `Grabbable`, `GrabInteractable`, `DistanceGrabInteractable`, `BaseInteractors`, and comprehensive rig package sources
- `Assets/Settings/Mobile_RPAsset.asset`, `PC_RPAsset.asset`, `Build Profiles/Meta Quest.asset`
- Existing `Docs/AI/UnityProjectContext.md`

<!-- unity-onboarding:generated:end -->
