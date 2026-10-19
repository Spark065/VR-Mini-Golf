using System;
using System.Collections.Generic;
using System.IO;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class Scene2CourseBuilder
{
    private const string ScenePath = "Assets/Scenes/Scene 2.unity";
    private const string GeneratedRootName = "Scene2_Course";
    private const string ClubGrabAnchorName = "Club_GrabAnchor";
    private const string PendingRequestPath = "Library/CodexScene2Builder.pending";
    private const string PreviewPath = "Temp/Scene2CoursePreview.png";

    private const string StartPrefabPath = "Assets/Prefabs/start.prefab";
    private const string StraightPrefabPath = "Assets/Prefabs/straight.prefab";
    private const string CornerPrefabPath = "Assets/Prefabs/square-corner-a.prefab";
    private const string HolePrefabPath = "Assets/Prefabs/hole-round.prefab";
    private const string FlagPrefabPath = "Assets/Prefabs/flag-blue.prefab";
    private const string DiamondPrefabPath = "Assets/Prefabs/obstacle-diamond.prefab";
    private const string BlockPrefabPath = "Assets/Prefabs/obstacle-block.prefab";
    private const string BallPhysicsMaterialPath = "Assets/Physics/MiniGolfBall.physicMaterial";
    private const string GroundPhysicsMaterialPath = "Assets/Physics/MiniGolfGround.physicMaterial";
    private const string ObstaclePhysicsMaterialPath = "Assets/Physics/MiniGolfObstacle.physicMaterial";

    static Scene2CourseBuilder()
    {
        EditorApplication.delayCall += ProcessPendingRequest;
    }

    [MenuItem("Tools/VR Mini Golf/Rebuild Scene 2 Course")]
    public static void RebuildScene2Course()
    {
        BuildCourse(allowOpeningScene: true);
    }

    [MenuItem("Tools/VR Mini Golf/Sahneler/2. Sahne Golf Sopasini Duzelt")]
    public static void RepairScene2GolfClub()
    {
        Scene scene = FindLoadedScene(ScenePath);
        if (!scene.IsValid())
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        SceneManager.SetActiveScene(scene);
        VRGolfClub club = FindComponentInScene<VRGolfClub>(scene);
        if (club == null)
        {
            Debug.LogError("Scene 2 golf club repair could not find VRGolfClub.");
            return;
        }

        Bounds beforeBounds = CalculateBounds(club.gameObject);
        if (!RepairClubHierarchyAndInteraction(club))
        {
            return;
        }

        if (!PositionClubBesidePlayer(scene, club))
        {
            return;
        }

        if (!ConfigureClubGrabGeometry(club))
        {
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
        {
            Debug.LogError("Scene 2 golf club was repaired in memory but Unity could not save the scene.");
            return;
        }

        Bounds afterBounds = CalculateBounds(club.gameObject);
        Debug.Log(
            $"Scene 2 golf club repair complete. " +
            $"visualCenterBefore={beforeBounds.center}, visualCenterAfter={afterBounds.center}, " +
            $"visualSize={afterBounds.size}.");
    }

    private static void ProcessPendingRequest()
    {
        if (!File.Exists(PendingRequestPath))
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ProcessPendingRequest;
            return;
        }

        if (BuildCourse(allowOpeningScene: false))
        {
            File.Delete(PendingRequestPath);
            AssetDatabase.Refresh();
        }
    }

    private static bool BuildCourse(bool allowOpeningScene)
    {
        if (!File.Exists(ScenePath))
        {
            Debug.LogError($"Scene 2 builder could not find {ScenePath}.");
            return false;
        }

        Scene scene = FindLoadedScene(ScenePath);
        if (!scene.IsValid())
        {
            if (!allowOpeningScene && HasDirtyLoadedScene())
            {
                Debug.LogWarning(
                    "Scene 2 automatic build paused because another loaded scene has unsaved changes. " +
                    "Open Scene 2 and use Tools > VR Mini Golf > Rebuild Scene 2 Course.");
                return false;
            }

            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        SceneManager.SetActiveScene(scene);

        GameObject existingGeneratedRoot = FindRoot(scene, GeneratedRootName);
        if (existingGeneratedRoot != null)
        {
            UnityEngine.Object.DestroyImmediate(existingGeneratedRoot);
        }

        PreserveLegacyCourse(scene);
        ConfigureOutOfBoundsPlane(scene);

        GameObject courseRoot = new GameObject(GeneratedRootName);
        SceneManager.MoveGameObjectToScene(courseRoot, scene);
        courseRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Transform piecesRoot = CreateGroup("Course_Pieces", courseRoot.transform);
        Transform obstaclesRoot = CreateGroup("Obstacles", courseRoot.transform);
        Transform gameplayRoot = CreateGroup("Gameplay", courseRoot.transform);
        Transform safetyRoot = CreateGroup("Safety_Colliders", courseRoot.transform);

        GameObject measurementPiece = InstantiatePrefab( StraightPrefabPath, piecesRoot, "Measurement_Straight");
        measurementPiece.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Physics.SyncTransforms();
        Bounds measurementBounds = CalculateBounds(measurementPiece);
        float tileStep = Mathf.Max(measurementBounds.size.x, measurementBounds.size.z);
        float laneWidth = Mathf.Min(measurementBounds.size.x, measurementBounds.size.z);
        bool straightRunsOnZ = measurementBounds.size.z >= measurementBounds.size.x;
        UnityEngine.Object.DestroyImmediate(measurementPiece);

        tileStep = Mathf.Max(0.5f, tileStep * 0.995f);
        laneWidth = Mathf.Max(0.8f, laneWidth);

        Quaternion firstRotation = straightRunsOnZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
        Quaternion secondRotation = firstRotation * Quaternion.Euler(0f, 90f, 0f);
        Vector3 firstDirection = firstRotation * Vector3.forward;
        Vector3 secondDirection = secondRotation * Vector3.forward;
        Vector3 sideDirection = firstRotation * Vector3.right;
        Vector3 origin = Vector3.zero;
        Vector3 cornerPosition = origin + firstDirection * (tileStep * 4f);

        GameObject start = PlaceCoursePiece(StartPrefabPath, piecesRoot, "01_Start", origin, firstRotation);
        PlaceCoursePiece(StraightPrefabPath, piecesRoot, "02_Straight", origin + firstDirection * tileStep, firstRotation);
        PlaceCoursePiece(
            DiamondPrefabPath,
            obstaclesRoot,
            "03_Obstacle_Diamond",
            origin + firstDirection * tileStep * 2f,
            firstRotation);
        PlaceCoursePiece(StraightPrefabPath, piecesRoot, "04_Straight", origin + firstDirection * tileStep * 3f, firstRotation);
        PlaceCoursePiece(CornerPrefabPath, piecesRoot, "05_L_Corner", cornerPosition, firstRotation);
        PlaceCoursePiece(StraightPrefabPath, piecesRoot, "06_Straight", cornerPosition + secondDirection * tileStep, secondRotation);
        PlaceCoursePiece(
            BlockPrefabPath,
            obstaclesRoot,
            "07_Obstacle_Block",
            cornerPosition + secondDirection * tileStep * 2f,
            secondRotation);
        GameObject holePiece = PlaceCoursePiece(
            HolePrefabPath,
            piecesRoot,
            "08_Hole",
            cornerPosition + secondDirection * tileStep * 3f,
            secondRotation);

        Vector3 holePosition = holePiece.transform.position;
        GameObject flag = InstantiatePrefab(FlagPrefabPath, gameplayRoot, "Hole_Flag");
        flag.transform.SetPositionAndRotation(holePosition, secondRotation);
        SetStaticRecursively(flag);

        GameObject holeTrigger = new GameObject("Hole_Trigger");
        holeTrigger.transform.SetParent(gameplayRoot, false);
        holeTrigger.transform.position = holePosition + Vector3.up * 0.08f;
        SphereCollider trigger = holeTrigger.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Clamp(laneWidth * 0.11f, 0.11f, 0.2f);
        holeTrigger.AddComponent<GolfHole>();

        CreateSafetyColliders(
            safetyRoot,
            origin,
            cornerPosition,
            holePosition,
            firstDirection,
            secondDirection,
            firstRotation,
            secondRotation,
            tileStep,
            laneWidth);

        PositionGameplayObjects(scene, start, origin, firstDirection, sideDirection, tileStep, laneWidth);
        ConfigureLighting(scene);
        EnsureSceneInBuildSettings();

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
        if (!saved)
        {
            Debug.LogError("Scene 2 course was built in memory but Unity could not save the scene.");
            return false;
        }

        if (!ValidateScene2(scene, courseRoot))
        {
            return false;
        }

        CreatePreview(scene, courseRoot, firstDirection, secondDirection);
        Debug.Log(
            $"Scene 2 course build complete. tileStep={tileStep:F2}m, laneWidth={laneWidth:F2}m, " +
            $"preview={PreviewPath}");
        return true;
    }

    private static bool ValidateScene2(Scene scene, GameObject courseRoot)
    {
        List<string> issues = new List<string>();
        Transform piecesRoot = courseRoot.transform.Find("Course_Pieces");
        Transform obstaclesRoot = courseRoot.transform.Find("Obstacles");
        Transform holeTrigger = courseRoot.transform.Find("Gameplay/Hole_Trigger");

        if (piecesRoot == null || piecesRoot.childCount != 6)
        {
            issues.Add("expected six standard course prefab instances");
        }

        if (obstaclesRoot == null || obstaclesRoot.childCount != 2)
        {
            issues.Add("expected two obstacle prefab instances");
        }

        ValidatePrefabChildren(piecesRoot, issues);
        ValidatePrefabChildren(obstaclesRoot, issues);

        SphereCollider holeCollider = holeTrigger != null ? holeTrigger.GetComponent<SphereCollider>() : null;
        if (holeTrigger == null || holeCollider == null || !holeCollider.isTrigger || holeTrigger.GetComponent<GolfHole>() == null)
        {
            issues.Add("hole trigger or GolfHole component is missing");
        }

        BallController ball = FindComponentInScene<BallController>(scene);
        Rigidbody ballBody = ball != null ? ball.transform.root.GetComponent<Rigidbody>() : null;
        if (ball == null || ballBody == null || ballBody.isKinematic ||
            ballBody.collisionDetectionMode != CollisionDetectionMode.ContinuousDynamic)
        {
            issues.Add("golf ball is missing or its Rigidbody is not dynamic/Continuous Dynamic");
        }

        VRGolfClub club = FindComponentInScene<VRGolfClub>(scene);
        if (club == null || ball == null || Vector3.Distance(club.transform.position, ball.transform.position) > 3f)
        {
            issues.Add("VR golf club striking object is missing or too far from the starting ball");
        }

        if (FindComponentInScene<GameManager>(scene) == null)
        {
            issues.Add("GameManager is missing");
        }

        GameObject outerPlane = FindRoot(scene, "Plane");
        Collider outerPlaneCollider = outerPlane != null ? outerPlane.GetComponent<Collider>() : null;
        if (outerPlane == null || !outerPlane.CompareTag("OutOfBounds")
            || outerPlaneCollider == null || !outerPlaneCollider.enabled)
        {
            issues.Add("outer Plane must remain an enabled OutOfBounds catch surface");
        }

        if (FindComponentInScene<Camera>(scene) == null ||
            FindComponentInScene<Light>(scene, light => light.type == LightType.Directional) == null)
        {
            issues.Add("camera rig camera or directional light is missing");
        }

        int missingScriptCount = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                missingScriptCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject);
            }
        }

        if (missingScriptCount > 0)
        {
            issues.Add($"scene contains {missingScriptCount} missing script reference(s)");
        }

        bool sceneEnabled = Array.Exists(
            EditorBuildSettings.scenes,
            item => item.enabled && string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase));
        if (!sceneEnabled)
        {
            issues.Add("Scene 2 is not enabled in Build Settings");
        }

        if (issues.Count > 0)
        {
            Debug.LogError("Scene 2 validation failed: " + string.Join("; ", issues));
            return false;
        }

        Debug.Log(
            "Scene 2 validation passed: 8 connected course prefab modules, hole trigger, dynamic ball, " +
            "camera/light, Build Settings entry, and zero missing scripts.");
        return true;
    }

    private static void ValidatePrefabChildren(Transform parent, List<string> issues)
    {
        if (parent == null)
        {
            return;
        }

        foreach (Transform child in parent)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(child.gameObject) ||
                PrefabUtility.GetPrefabInstanceStatus(child.gameObject) != PrefabInstanceStatus.Connected)
            {
                issues.Add($"{child.name} is not a connected prefab instance");
            }
        }
    }

    private static GameObject PlaceCoursePiece(
        string prefabPath,
        Transform parent,
        string objectName,
        Vector3 position,
        Quaternion rotation)
    {
        GameObject instance = InstantiatePrefab(prefabPath, parent, objectName);
        instance.transform.SetPositionAndRotation(position, rotation);
        PhysicsMaterial surfaceMaterial = prefabPath == DiamondPrefabPath || prefabPath == BlockPrefabPath
            ? AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ObstaclePhysicsMaterialPath)
            : AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(GroundPhysicsMaterialPath);
        EnsureStaticMeshColliders(instance, surfaceMaterial);
        SetStaticRecursively(instance);
        return instance;
    }

    private static GameObject InstantiatePrefab(string assetPath, Transform parent, string objectName)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            throw new InvalidOperationException($"Required prefab not found: {assetPath}");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.name = objectName;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        return instance;
    }

    private static void EnsureStaticMeshColliders(GameObject instance, PhysicsMaterial surfaceMaterial)
    {
        MeshFilter[] meshFilters = instance.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Collider existingCollider = meshFilter.GetComponent<Collider>();
            if (existingCollider == null)
            {
                MeshCollider meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                existingCollider = meshCollider;
            }

            existingCollider.sharedMaterial = surfaceMaterial;
        }
    }

    private static void CreateSafetyColliders(
        Transform parent,
        Vector3 origin,
        Vector3 cornerPosition,
        Vector3 holePosition,
        Vector3 firstDirection,
        Vector3 secondDirection,
        Quaternion firstRotation,
        Quaternion secondRotation,
        float tileStep,
        float laneWidth)
    {
        float railThickness = 0.08f;
        float railHeight = 0.32f;
        float firstRailLength = tileStep * 4.15f;
        float secondRailLength = tileStep * 3.35f;
        float sideOffset = laneWidth * 0.5f + railThickness * 0.4f;

        Vector3 firstCenter = origin + firstDirection * tileStep * 1.55f + Vector3.up * railHeight * 0.5f;
        CreateBoxCollider(parent, "FirstLeg_Left_Safety", firstCenter - firstRotation * Vector3.right * sideOffset,
            firstRotation, new Vector3(railThickness, railHeight, firstRailLength));
        CreateBoxCollider(parent, "FirstLeg_Right_Safety", firstCenter + firstRotation * Vector3.right * sideOffset,
            firstRotation, new Vector3(railThickness, railHeight, firstRailLength));

        Vector3 secondCenter = cornerPosition + secondDirection * tileStep * 1.65f + Vector3.up * railHeight * 0.5f;
        CreateBoxCollider(parent, "SecondLeg_Left_Safety", secondCenter - secondRotation * Vector3.right * sideOffset,
            secondRotation, new Vector3(railThickness, railHeight, secondRailLength));
        CreateBoxCollider(parent, "SecondLeg_Right_Safety", secondCenter + secondRotation * Vector3.right * sideOffset,
            secondRotation, new Vector3(railThickness, railHeight, secondRailLength));

        Vector3 endCenter = holePosition + secondDirection * tileStep * 0.5f + Vector3.up * railHeight * 0.5f;
        CreateBoxCollider(parent, "Hole_End_Safety", endCenter, secondRotation,
            new Vector3(laneWidth + railThickness * 2f, railHeight, railThickness));

        CreateOutOfBoundsFloor(
            parent,
            "FirstLeg_OutOfBounds",
            origin + firstDirection * tileStep * 2f + Vector3.down * 0.65f,
            firstRotation,
            new Vector3(laneWidth + 2f, 0.1f, tileStep * 5f + 2f));
        CreateOutOfBoundsFloor(
            parent,
            "SecondLeg_OutOfBounds",
            cornerPosition + secondDirection * tileStep * 1.5f + Vector3.down * 0.65f,
            secondRotation,
            new Vector3(laneWidth + 2f, 0.1f, tileStep * 4f + 2f));
    }

    private static void CreateBoxCollider(
        Transform parent,
        string objectName,
        Vector3 position,
        Quaternion rotation,
        Vector3 size)
    {
        GameObject colliderObject = new GameObject(objectName);
        colliderObject.transform.SetParent(parent, false);
        colliderObject.transform.SetPositionAndRotation(position, rotation);
        BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
        boxCollider.size = size;
        boxCollider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ObstaclePhysicsMaterialPath);
    }

    private static void CreateOutOfBoundsFloor(
        Transform parent,
        string objectName,
        Vector3 position,
        Quaternion rotation,
        Vector3 size)
    {
        GameObject floor = new GameObject(objectName);
        floor.transform.SetParent(parent, false);
        floor.transform.SetPositionAndRotation(position, rotation);
        floor.tag = "OutOfBounds";
        BoxCollider collider = floor.AddComponent<BoxCollider>();
        collider.size = size;
        collider.isTrigger = true;
        collider.sharedMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(GroundPhysicsMaterialPath);
    }

    private static void PositionGameplayObjects(
        Scene scene,
        GameObject start,
        Vector3 origin,
        Vector3 firstDirection,
        Vector3 sideDirection,
        float tileStep,
        float laneWidth)
    {
        BallController ball = FindComponentInScene<BallController>(scene);
        if (ball != null)
        {
            Transform ballRoot = ball.transform.root;
            Bounds startBounds = CalculateBounds(start);
            Bounds ballBounds = CalculateBounds(ballRoot.gameObject);
            float ballRadius = Mathf.Max(0.035f, ballBounds.extents.y);
            ballRoot.position = new Vector3(origin.x, startBounds.max.y + ballRadius + 0.015f, origin.z);
            ballRoot.rotation = Quaternion.identity;
            ballRoot.gameObject.tag = "GolfBall";

            Rigidbody body = ballRoot.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                body.mass = 0.04593f;
                body.linearDamping = 0.04f;
                body.angularDamping = 0.05f;
                body.maxAngularVelocity = 120f;
                body.maxDepenetrationVelocity = 3f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            PhysicsMaterial ballMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BallPhysicsMaterialPath);
            foreach (Collider collider in ballRoot.GetComponentsInChildren<Collider>(true))
            {
                collider.sharedMaterial = ballMaterial;
            }
        }

        Transform rigRoot = FindRootContaining(scene, "Camera Rig");
        Vector3 playerPosition = origin - firstDirection * tileStep * 0.4f - sideDirection * laneWidth * 0.85f;
        if (rigRoot != null)
        {
            playerPosition.y = rigRoot.position.y;
            rigRoot.SetPositionAndRotation(playerPosition, Quaternion.LookRotation(firstDirection, Vector3.up));
        }

        VRGolfClub club = FindComponentInScene<VRGolfClub>(scene);
        if (club != null)
        {
            Vector3 clubPosition = playerPosition + sideDirection * 0.45f + Vector3.up * 0.8f;
            // The imported club prefab has an offset model pivot. Moving VRGolfClub.transform
            // directly separates the visible club/colliders from the Meta Grabbable Rigidbody.
            // Translate the complete Meta motion root from the rendered club center instead.
            Grabbable grabbable = club.GetComponentInParent<Grabbable>();
            Transform motionRoot = grabbable != null && grabbable.Transform != null
                ? grabbable.Transform
                : grabbable != null ? grabbable.transform : club.transform;
            Bounds clubBounds = CalculateBounds(club.gameObject);
            motionRoot.position += clubPosition - clubBounds.center;

            Rigidbody body = grabbable != null
                ? grabbable.GetComponent<Rigidbody>()
                : club.GetComponent<Rigidbody>();
            if (body != null && !body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

        }
    }

    private static bool RepairClubHierarchyAndInteraction(VRGolfClub club)
    {
        Grabbable grabbable = club.GetComponentInParent<Grabbable>();
        if (grabbable == null)
        {
            Debug.LogError("Scene 2 golf club repair requires the existing Meta Grabbable parent.");
            return false;
        }

        Rigidbody grabBody = grabbable.GetComponent<Rigidbody>();
        if (grabBody == null)
        {
            Debug.LogError("Scene 2 golf club repair requires the existing Meta Grabbable Rigidbody.");
            return false;
        }

        Transform motionRoot = grabbable.Transform != null ? grabbable.Transform : grabbable.transform;
        if (club.transform.parent != motionRoot)
        {
            Undo.SetTransformParent(club.transform, motionRoot, "Repair Scene 2 golf club hierarchy");
        }

        Undo.RecordObject(grabbable, "Repair Scene 2 Meta grab settings");
        grabbable.InjectOptionalTargetTransform(motionRoot);
        grabbable.InjectOptionalRigidbody(grabBody);
        grabbable.InjectOptionalKinematicWhileSelected(true);
        grabbable.InjectOptionalThrowWhenUnselected(false);
        grabbable.MaxGrabPoints = 1;
        grabbable.TransferOnSecondSelection = true;
        EditorUtility.SetDirty(grabbable);

        GrabInteractable nearGrab = grabbable.GetComponentInChildren<GrabInteractable>(true);
        if (nearGrab == null)
        {
            Debug.LogError("Scene 2 golf club repair could not find the existing Meta GrabInteractable.");
            return false;
        }

        Undo.RecordObject(nearGrab, "Repair Scene 2 controller grab settings");
        nearGrab.InjectAllGrabInteractable(grabBody);
        nearGrab.MaxSelectingInteractors = 1;
        nearGrab.ResetGrabOnGrabsUpdated = true;
        EditorUtility.SetDirty(nearGrab);

        HandGrabInteractable handGrab = grabbable.GetComponentInChildren<HandGrabInteractable>(true);
        if (handGrab == null)
        {
            Debug.LogError("Scene 2 golf club repair could not find the existing Meta HandGrabInteractable.");
            return false;
        }

        Undo.RecordObject(handGrab, "Repair Scene 2 hand grab settings");
        handGrab.InjectRigidbody(grabBody);
        handGrab.InjectOptionalPointableElement(grabbable);
        handGrab.MaxSelectingInteractors = 1;
        handGrab.ResetGrabOnGrabsUpdated = true;
        EditorUtility.SetDirty(handGrab);

        DistanceGrabInteractable distanceGrab =
            grabbable.GetComponentInChildren<DistanceGrabInteractable>(true);
        if (distanceGrab == null)
        {
            distanceGrab = Undo.AddComponent<DistanceGrabInteractable>(nearGrab.gameObject);
        }

        Undo.RecordObject(distanceGrab, "Repair Scene 2 distance grab settings");
        // Scene 2 must use the same close-range grab behavior as the authored
        // club setup. Distance Grab makes the imported pivot snap from far away.
        distanceGrab.enabled = false;
        distanceGrab.InjectAllGrabInteractable(grabBody);
        distanceGrab.InjectOptionalPointableElement(grabbable);
        distanceGrab.MaxSelectingInteractors = 1;
        distanceGrab.ResetGrabOnGrabsUpdated = true;
        EditorUtility.SetDirty(distanceGrab);

        SerializedObject serializedClub = new SerializedObject(club);
        serializedClub.Update();
        SerializedProperty distanceGrabProperty =
            serializedClub.FindProperty("enableControllerDistanceGrab");
        SerializedProperty runtimeLengthProperty =
            serializedClub.FindProperty("enforceRealPutterLength");
        if (distanceGrabProperty != null)
        {
            distanceGrabProperty.boolValue = false;
        }

        if (runtimeLengthProperty != null)
        {
            // Scene 2 already persists its authored scale. Disabling the duplicate Awake
            // rescale keeps its offset imported pivot from moving the model in Play Mode.
            runtimeLengthProperty.boolValue = false;
        }

        serializedClub.ApplyModifiedProperties();
        EditorUtility.SetDirty(club);

        ConfigureClubBody(grabBody);
        Rigidbody clubBody = club.GetComponent<Rigidbody>();
        if (clubBody != null && clubBody != grabBody)
        {
            ConfigureClubBody(clubBody);
        }

        return true;
    }

    private static bool PositionClubBesidePlayer(Scene scene, VRGolfClub club)
    {
        Transform rigRoot = FindRootContaining(scene, "Camera Rig");
        if (rigRoot == null)
        {
            Debug.LogError("Scene 2 golf club repair could not find the Camera Rig.");
            return false;
        }

        Grabbable grabbable = club.GetComponentInParent<Grabbable>();
        Transform motionRoot = grabbable != null && grabbable.Transform != null
            ? grabbable.Transform
            : grabbable != null ? grabbable.transform : club.transform;
        Bounds visualBounds = CalculateBounds(club.gameObject);
        Vector3 desiredVisualCenter = rigRoot.position + rigRoot.right * 0.45f + Vector3.up * 0.8f;

        Undo.RecordObject(motionRoot, "Align Scene 2 golf club with grab root");
        motionRoot.position += desiredVisualCenter - visualBounds.center;
        Physics.SyncTransforms();
        EditorUtility.SetDirty(motionRoot);
        return true;
    }

    private static bool ConfigureClubGrabGeometry(VRGolfClub club)
    {
        Grabbable grabbable = club.GetComponentInParent<Grabbable>();
        if (grabbable == null)
        {
            Debug.LogError("Scene 2 golf club grab geometry requires the Meta Grabbable parent.");
            return false;
        }

        Transform motionRoot = grabbable.Transform != null ? grabbable.Transform : grabbable.transform;
        BoxCollider grabCollider = motionRoot.GetComponent<BoxCollider>();
        if (grabCollider == null)
        {
            grabCollider = Undo.AddComponent<BoxCollider>(motionRoot.gameObject);
        }

        Bounds visualBounds = CalculateBounds(club.gameObject);
        Bounds localVisualBounds = ConvertWorldBoundsToLocal(motionRoot, visualBounds);
        Undo.RecordObject(grabCollider, "Align Scene 2 grab collider with golf club");
        grabCollider.center = localVisualBounds.center;
        grabCollider.size = localVisualBounds.size;
        grabCollider.isTrigger = true;
        EditorUtility.SetDirty(grabCollider);

        GrabInteractable nearGrab = grabbable.GetComponentInChildren<GrabInteractable>(true);
        if (nearGrab != null)
        {
            Undo.RecordObject(nearGrab, "Assign Scene 2 controller grab anchor");
            // Null keeps the same free close-range grab pose as Scene 1.
            nearGrab.InjectOptionalGrabSource(null);
            EditorUtility.SetDirty(nearGrab);
        }

        DistanceGrabInteractable distanceGrab =
            grabbable.GetComponentInChildren<DistanceGrabInteractable>(true);
        if (distanceGrab != null)
        {
            Undo.RecordObject(distanceGrab, "Assign Scene 2 distance grab anchor");
            distanceGrab.InjectOptionalGrabSource(null);
            distanceGrab.enabled = false;
            EditorUtility.SetDirty(distanceGrab);
        }

        Transform unusedGrabAnchor = motionRoot.Find(ClubGrabAnchorName);
        if (unusedGrabAnchor != null)
        {
            Undo.RecordObject(unusedGrabAnchor.gameObject, "Disable unused Scene 2 golf club grab anchor");
            unusedGrabAnchor.gameObject.SetActive(false);
            EditorUtility.SetDirty(unusedGrabAnchor.gameObject);
        }

        Physics.SyncTransforms();
        return true;
    }

    private static Bounds ConvertWorldBoundsToLocal(Transform localRoot, Bounds worldBounds)
    {
        Vector3 min = worldBounds.min;
        Vector3 max = worldBounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        Bounds localBounds = new Bounds(localRoot.InverseTransformPoint(corners[0]), Vector3.zero);
        for (int index = 1; index < corners.Length; index++)
        {
            localBounds.Encapsulate(localRoot.InverseTransformPoint(corners[index]));
        }

        return localBounds;
    }

    private static void ConfigureClubBody(Rigidbody body)
    {
        Undo.RecordObject(body, "Repair Scene 2 golf club Rigidbody");
        body.mass = 0.45f;
        body.linearDamping = 0f;
        body.angularDamping = 0.05f;
        body.useGravity = false;
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.None;
        EditorUtility.SetDirty(body);
    }

    private static void ConfigureLighting(Scene scene)
    {
        Light directionalLight = FindComponentInScene<Light>(scene, light => light.type == LightType.Directional);
        if (directionalLight == null)
        {
            return;
        }

        directionalLight.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        directionalLight.intensity = 1.1f;
        directionalLight.shadows = LightShadows.Soft;
    }

    private static void PreserveLegacyCourse(Scene scene)
    {
        GameObject legacy = FindRoot(scene, "golf course");
        if (legacy == null)
        {
            return;
        }

        legacy.name = "LegacyCourse_Disabled";
        legacy.SetActive(false);
    }

    private static void ConfigureOutOfBoundsPlane(Scene scene)
    {
        GameObject plane = FindRoot(scene, "Plane");
        if (plane == null)
        {
            return;
        }

        plane.tag = "OutOfBounds";
        foreach (Collider currentCollider in plane.GetComponentsInChildren<Collider>(true))
        {
            currentCollider.enabled = true;
        }
    }

    private static void EnsureSceneInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int existingIndex = scenes.FindIndex(item => string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            EditorBuildSettingsScene existing = scenes[existingIndex];
            scenes[existingIndex] = new EditorBuildSettingsScene(existing.path, true);
        }
        else
        {
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void CreatePreview(Scene scene, GameObject courseRoot, Vector3 firstDirection, Vector3 secondDirection)
    {
        Bounds bounds = CalculateBounds(courseRoot);
        if (bounds.size.sqrMagnitude < 0.01f)
        {
            return;
        }

        GameObject cameraObject = new GameObject("Scene2_Preview_Camera");
        SceneManager.MoveGameObjectToScene(cameraObject, scene);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.78f, 0.84f, 0.74f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.25f;
        camera.transform.position = bounds.center + Vector3.up * Mathf.Max(12f, bounds.size.magnitude)
            - firstDirection * 2f - secondDirection * 2f;
        camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, Vector3.up);

        RenderTexture renderTexture = new RenderTexture(1024, 768, 24);
        Texture2D screenshot = new Texture2D(1024, 768, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;

        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            screenshot.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0);
            screenshot.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(PreviewPath) ?? "Temp");
            File.WriteAllBytes(PreviewPath, screenshot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(screenshot);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }

    private static void SetStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.SetStaticEditorFlags(child.gameObject, flags);
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private static Transform CreateGroup(string objectName, Transform parent)
    {
        GameObject group = new GameObject(objectName);
        group.transform.SetParent(parent, false);
        return group.transform;
    }

    private static Scene FindLoadedScene(string path)
    {
        for (int index = 0; index < EditorSceneManager.sceneCount; index++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(index);
            if (string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase))
            {
                return scene;
            }
        }

        return default;
    }

    private static bool HasDirtyLoadedScene()
    {
        for (int index = 0; index < EditorSceneManager.sceneCount; index++)
        {
            if (EditorSceneManager.GetSceneAt(index).isDirty)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, objectName, StringComparison.Ordinal))
            {
                return root;
            }
        }

        return null;
    }

    private static Transform FindRootContaining(Scene scene, string namePart)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.IndexOf(namePart, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return root.transform;
            }
        }

        return null;
    }

    private static T FindComponentInScene<T>(Scene scene, Predicate<T> predicate = null) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] components = root.GetComponentsInChildren<T>(true);
            foreach (T component in components)
            {
                if (predicate == null || predicate(component))
                {
                    return component;
                }
            }
        }

        return null;
    }
}

public static class TwoSceneLevelDesignBuilder
{
    private const string Scene1Path = "Assets/Scenes/SampleScene.unity";
    private const string Scene2Path = "Assets/Scenes/Scene 2.unity";
    private const string GeneratedRootName = "LevelDesign_Course";
    private const string ReportPath = "Temp/TwoSceneLevelDesignReport.txt";

    private const string StartPrefabPath = "Assets/Prefabs/start.prefab";
    private const string StraightPrefabPath = "Assets/Prefabs/straight.prefab";
    private const string RampLowPrefabPath = "Assets/Prefabs/ramp-low.prefab";
    private const string RampMediumPrefabPath = "Assets/Prefabs/ramp-medium.prefab";
    private const string CornerPrefabPath = "Assets/Prefabs/square-corner-a.prefab";
    private const string TrianglePrefabPath = "Assets/Prefabs/obstacle-triangle.prefab";
    private const string DiamondPrefabPath = "Assets/Prefabs/obstacle-diamond.prefab";
    private const string NarrowBlockPrefabPath = "Assets/Prefabs/narrow-block.prefab";
    private const string BlockPrefabPath = "Assets/Prefabs/obstacle-block.prefab";
    private const string HolePrefabPath = "Assets/Prefabs/hole-round.prefab";
    private const string FlagPrefabPath = "Assets/Prefabs/flag-blue.prefab";
    private const string GroundMaterialPath = "Assets/Physics/MiniGolfGround.physicMaterial";
    private const string ObstacleMaterialPath = "Assets/Physics/MiniGolfObstacle.physicMaterial";

    private static readonly string[] LegacyRootNames =
    {
        "Golf Parcour",
        "Scene2_Course",
        "LegacyCourse_Disabled"
    };

    private static readonly ScenePlan[] Plans =
    {
        new ScenePlan(
            "Scene 1",
            Scene1Path,
            new[]
            {
                new PieceSpec("01_Start", StartPrefabPath, 0, 0, 0, false),
                new PieceSpec("02_Straight", StraightPrefabPath, 0, 1, 0, false),
                new PieceSpec("03_Ramp_Low", RampLowPrefabPath, 0, 2, 0, false),
                new PieceSpec("04_Straight", StraightPrefabPath, 0, 3, 0, false),
                new PieceSpec("05_Corner", CornerPrefabPath, 0, 4, 0, false),
                new PieceSpec("06_Straight", StraightPrefabPath, 1, 4, 1, false),
                new PieceSpec("07_Obstacle_Triangle", TrianglePrefabPath, 2, 4, 1, true),
                new PieceSpec("08_Straight", StraightPrefabPath, 3, 4, 1, false),
                new PieceSpec("09_Hole_Round", HolePrefabPath, 4, 4, 1, false)
            },
            new[]
            {
                new LegSpec(0, 0, 0, 4),
                new LegSpec(0, 4, 4, 4)
            }),
        new ScenePlan(
            "Scene 2",
            Scene2Path,
            new[]
            {
                new PieceSpec("01_Start", StartPrefabPath, 0, 0, 0, false),
                new PieceSpec("02_Straight", StraightPrefabPath, 0, 1, 0, false),
                new PieceSpec("03_Obstacle_Diamond", DiamondPrefabPath, 0, 2, 0, true),
                new PieceSpec("04_Ramp_Medium", RampMediumPrefabPath, 0, 3, 0, false),
                new PieceSpec("05_Corner_First", CornerPrefabPath, 0, 4, 0, false),
                new PieceSpec("06_Straight", StraightPrefabPath, 1, 4, 1, false),
                new PieceSpec("07_Narrow_Block", NarrowBlockPrefabPath, 2, 4, 1, true),
                new PieceSpec("08_Straight", StraightPrefabPath, 3, 4, 1, false),
                new PieceSpec("09_Corner_Second", CornerPrefabPath, 4, 4, 1, false),
                new PieceSpec("10_Straight", StraightPrefabPath, 4, 3, 2, false),
                new PieceSpec("11_Obstacle_Block", BlockPrefabPath, 4, 2, 2, true),
                new PieceSpec("12_Straight", StraightPrefabPath, 4, 1, 2, false),
                new PieceSpec("13_Hole_Round", HolePrefabPath, 4, 0, 2, false)
            },
            new[]
            {
                new LegSpec(0, 0, 0, 4),
                new LegSpec(0, 4, 4, 4),
                new LegSpec(4, 4, 4, 0)
            })
    };

    [MenuItem("Tools/VR Mini Golf/Level Design/Iki Sahneyi Kur")]
    public static void BuildBothScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            Debug.LogError("Two-scene level design build is unavailable while Unity is playing or compiling.");
            return;
        }

        List<string> preflightIssues = ValidateRequiredAssets();
        if (preflightIssues.Count > 0)
        {
            Debug.LogError("Two-scene level design build stopped: " + string.Join("; ", preflightIssues));
            return;
        }

        Scene originalActiveScene = SceneManager.GetActiveScene();
        List<Scene> scenesOpenedByTool = new List<Scene>();
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build two-scene mini golf level design");
        bool buildSucceeded = false;

        try
        {
            foreach (ScenePlan plan in Plans)
            {
                Scene scene = GetOrOpenScene(plan.ScenePath, scenesOpenedByTool);
                SceneManager.SetActiveScene(scene);
                BuildScene(scene, plan);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, plan.ScenePath))
                {
                    throw new InvalidOperationException("Unity could not save " + plan.ScenePath + ".");
                }
            }

            buildSucceeded = true;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            RestoreSceneSetup(originalActiveScene, scenesOpenedByTool);
            Undo.CollapseUndoOperations(undoGroup);

            // Collapsing the Undo group can mark the active scene dirty again after
            // the per-scene saves above. Only re-save when the original scene is one
            // of the two scenes this tool intentionally owns.
            if (buildSucceeded &&
                originalActiveScene.IsValid() &&
                originalActiveScene.isLoaded &&
                originalActiveScene.isDirty &&
                IsPlannedScene(originalActiveScene.path))
            {
                if (!EditorSceneManager.SaveScene(originalActiveScene, originalActiveScene.path))
                {
                    buildSucceeded = false;
                    Debug.LogError("Two-scene level design build could not save the restored active scene.");
                }
            }

            if (buildSucceeded && originalActiveScene.IsValid() && IsPlannedScene(originalActiveScene.path))
            {
                QueueFinalPlannedSceneSave(originalActiveScene.path);
            }
        }

        if (!buildSucceeded)
        {
            return;
        }

        Debug.Log("Two-scene level design build completed and both scenes were saved.");
        ValidateBothScenes();
    }

    [MenuItem("Tools/VR Mini Golf/Level Design/Iki Sahneyi Dogrula")]
    public static void ValidateBothScenes()
    {
        Scene originalActiveScene = SceneManager.GetActiveScene();
        List<Scene> scenesOpenedByTool = new List<Scene>();
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        bool allPassed = true;

        report.AppendLine("Two Scene Level Design Validation");
        report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine();

        try
        {
            foreach (ScenePlan plan in Plans)
            {
                if (!File.Exists(plan.ScenePath))
                {
                    allPassed = false;
                    report.AppendLine(plan.DisplayName + " [FAIL]");
                    report.AppendLine("  - Scene asset is missing: " + plan.ScenePath);
                    report.AppendLine();
                    continue;
                }

                Scene scene = GetOrOpenScene(plan.ScenePath, scenesOpenedByTool);
                bool scenePassed = ValidateSceneReadOnly(scene, plan, report);
                allPassed &= scenePassed;
                report.AppendLine();
            }
        }
        catch (Exception exception)
        {
            allPassed = false;
            report.AppendLine("VALIDATION EXCEPTION [FAIL]");
            report.AppendLine(exception.ToString());
        }
        finally
        {
            RestoreSceneSetup(originalActiveScene, scenesOpenedByTool);
        }

        report.AppendLine(allPassed ? "OVERALL: PASS" : "OVERALL: FAIL");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Temp");
        File.WriteAllText(ReportPath, report.ToString());

        if (allPassed)
        {
            Debug.Log(report.ToString());
        }
        else
        {
            Debug.LogError(report.ToString());
        }
    }

    private static void BuildScene(Scene scene, ScenePlan plan)
    {
        BallController ball = RequireExactlyOne<BallController>(scene, plan.DisplayName);
        VRGolfClub club = RequireExactlyOne<VRGolfClub>(scene, plan.DisplayName);
        GolfHole hole = RequireExactlyOne<GolfHole>(scene, plan.DisplayName);
        RequireExactlyOne<GameManager>(scene, plan.DisplayName);
        RequireExactlyOne<MetaLevelTransitionUI>(scene, plan.DisplayName);

        Transform rigRoot = FindCameraRig(scene);
        if (rigRoot == null)
        {
            throw new InvalidOperationException(plan.DisplayName + " does not contain an existing Camera Rig.");
        }

        foreach (GameObject priorRoot in FindRoots(scene, GeneratedRootName))
        {
            DetachGolfHoles(priorRoot);
            Undo.DestroyObjectImmediate(priorRoot);
        }

        DisableLegacyCourseRoots(scene);

        GameObject generatedRoot = CreateGameObject(GeneratedRootName, null, scene);
        Transform piecesRoot = CreateGameObject("Course_Pieces", generatedRoot.transform, scene).transform;
        Transform obstaclesRoot = CreateGameObject("Obstacles", generatedRoot.transform, scene).transform;
        Transform gameplayRoot = CreateGameObject("Gameplay", generatedRoot.transform, scene).transform;
        Transform safetyRoot = CreateGameObject("Safety_Colliders", generatedRoot.transform, scene).transform;

        GameObject straightPrefab = LoadRequiredAsset<GameObject>(StraightPrefabPath);
        Bounds straightBounds = CalculatePrefabLocalBounds(straightPrefab);
        float tileStep = Mathf.Max(0.5f, Mathf.Max(straightBounds.size.x, straightBounds.size.z) * 0.995f);
        float laneWidth = Mathf.Max(0.8f, Mathf.Min(straightBounds.size.x, straightBounds.size.z));
        // Kenney straight.prefab's authored open/travel axis is local Z; square-ish renderer bounds are not orientation data.
        Quaternion forwardRotation = Quaternion.identity;

        PhysicsMaterial groundMaterial = LoadRequiredAsset<PhysicsMaterial>(GroundMaterialPath);
        PhysicsMaterial obstacleMaterial = LoadRequiredAsset<PhysicsMaterial>(ObstacleMaterialPath);
        GameObject startPiece = null;
        GameObject holePiece = null;

        foreach (PieceSpec piece in plan.Pieces)
        {
            Transform parent = piece.IsObstacle ? obstaclesRoot : piecesRoot;
            Quaternion rotation = forwardRotation * Quaternion.Euler(0f, piece.QuarterTurns * 90f, 0f);
            Vector3 position = GridPosition(piece.X, piece.Z, tileStep);
            GameObject instance = PlaceConnectedPrefab(
                piece.PrefabPath,
                parent,
                piece.Name,
                position,
                rotation,
                piece.IsObstacle ? obstacleMaterial : groundMaterial);

            if (piece.PrefabPath == StartPrefabPath)
            {
                startPiece = instance;
            }

            if (piece.PrefabPath == HolePrefabPath)
            {
                holePiece = instance;
            }
        }

        if (startPiece == null || holePiece == null)
        {
            throw new InvalidOperationException(plan.DisplayName + " plan must include start and hole prefabs.");
        }

        PlaceConnectedPrefab(
            FlagPrefabPath,
            gameplayRoot,
            "Gameplay_Flag",
            holePiece.transform.position,
            holePiece.transform.rotation,
            groundMaterial,
            false);

        ConfigureExistingHole(hole, gameplayRoot, holePiece.transform.position, holePiece.transform.rotation, laneWidth);
        CreateSafetyGeometry(plan, safetyRoot, forwardRotation, tileStep, laneWidth, groundMaterial, obstacleMaterial);
        ConfigureOutOfBoundsPlane(scene, groundMaterial);
        PositionGameplayObjects(scene, ball, club, rigRoot, startPiece, forwardRotation, tileStep, laneWidth);
    }

    private static void ConfigureExistingHole(
        GolfHole hole,
        Transform gameplayRoot,
        Vector3 targetPosition,
        Quaternion targetRotation,
        float laneWidth)
    {
        Undo.SetTransformParent(hole.transform, gameplayRoot, "Move existing GolfHole into generated gameplay group");
        Undo.RecordObject(hole.gameObject, "Activate existing GolfHole");
        Undo.RecordObject(hole.transform, "Position existing GolfHole");
        hole.gameObject.SetActive(true);
        hole.transform.SetPositionAndRotation(targetPosition, targetRotation);

        Collider triggerCollider = null;
        Collider[] colliders = hole.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            triggerCollider = colliders[0];
        }
        else
        {
            SphereCollider sphere = Undo.AddComponent<SphereCollider>(hole.gameObject);
            sphere.radius = Mathf.Clamp(laneWidth * 0.11f, 0.11f, 0.2f);
            triggerCollider = sphere;
        }

        Undo.RecordObject(triggerCollider, "Configure GolfHole trigger");
        triggerCollider.enabled = true;
        triggerCollider.isTrigger = true;
        EditorUtility.SetDirty(triggerCollider);
        EditorUtility.SetDirty(hole.gameObject);
    }

    private static void PositionGameplayObjects(
        Scene scene,
        BallController ball,
        VRGolfClub club,
        Transform rigRoot,
        GameObject startPiece,
        Quaternion forwardRotation,
        float tileStep,
        float laneWidth)
    {
        Rigidbody ballBody = ball.GetComponentInParent<Rigidbody>();
        if (ballBody == null)
        {
            ballBody = Undo.AddComponent<Rigidbody>(ball.gameObject);
        }

        Bounds startBounds = CalculateWorldBounds(startPiece);
        Bounds ballBounds = CalculateWorldBounds(ballBody.gameObject);
        float ballRadius = Mathf.Max(0.035f, ballBounds.extents.y);
        Vector3 targetBallCenter = new Vector3(0f, startBounds.max.y + ballRadius + 0.015f, 0f);

        Undo.RecordObject(ballBody.transform, "Position existing golf ball");
        ballBody.transform.position += targetBallCenter - ballBounds.center;
        Undo.RecordObject(ballBody, "Configure existing golf ball Rigidbody");
        ballBody.isKinematic = false;
        ballBody.useGravity = true;
        ballBody.detectCollisions = true;
        ballBody.interpolation = RigidbodyInterpolation.Interpolate;
        ballBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        ballBody.linearVelocity = Vector3.zero;
        ballBody.angularVelocity = Vector3.zero;
        EditorUtility.SetDirty(ballBody);

        Vector3 forward = forwardRotation * Vector3.forward;
        Vector3 side = forwardRotation * Vector3.right;
        float behindClearance = Mathf.Max(1.1f, tileStep * 0.3f);
        float sideClearance = laneWidth * 0.5f + 1.1f;
        Vector3 rigPosition = -forward * behindClearance - side * sideClearance;
        rigPosition.y = rigRoot.position.y;

        Undo.RecordObject(rigRoot, "Position existing Camera Rig with swing clearance");
        rigRoot.SetPositionAndRotation(rigPosition, Quaternion.LookRotation(forward, Vector3.up));
        EditorUtility.SetDirty(rigRoot);

        Grabbable grabbable = club.GetComponentInParent<Grabbable>();
        Transform motionRoot = grabbable != null && grabbable.Transform != null
            ? grabbable.Transform
            : grabbable != null ? grabbable.transform : club.transform;
        Bounds clubBounds = CalculateWorldBounds(club.gameObject);
        Vector3 desiredClubCenter = rigPosition + side * 0.55f + forward * 0.25f + Vector3.up * 0.8f;

        Undo.RecordObject(motionRoot, "Translate complete Meta Grabbable motion root");
        motionRoot.position += desiredClubCenter - clubBounds.center;
        EditorUtility.SetDirty(motionRoot);
        Physics.SyncTransforms();
    }

    private static void CreateSafetyGeometry(
        ScenePlan plan,
        Transform safetyRoot,
        Quaternion forwardRotation,
        float tileStep,
        float laneWidth,
        PhysicsMaterial groundMaterial,
        PhysicsMaterial obstacleMaterial)
    {
        for (int index = 0; index < plan.Legs.Length; index++)
        {
            LegSpec leg = plan.Legs[index];
            Vector3 start = GridPosition(leg.StartX, leg.StartZ, tileStep);
            Vector3 end = GridPosition(leg.EndX, leg.EndZ, tileStep);
            Vector3 direction = (end - start).normalized;
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 right = rotation * Vector3.right;
            float railThickness = 0.08f;
            float railHeight = 0.34f;
            float joinOverlap = 0.20f;
            float outOfBoundsLength = Vector3.Distance(start, end) + tileStep + joinOverlap;
            float halfTile = tileStep * 0.5f;
            float railOverlap = 0.02f;
            Vector3 railStart = start + direction * (index > 0 ? halfTile - railOverlap : -halfTile);
            Vector3 railEnd = end + direction * (index < plan.Legs.Length - 1 ? -halfTile + railOverlap : halfTile);
            float railLength = Vector3.Distance(railStart, railEnd);
            float sideOffset = laneWidth * 0.5f + railThickness * 0.5f;
            Vector3 center = (start + end) * 0.5f;
            Vector3 railCenter = (railStart + railEnd) * 0.5f;
            string prefix = "Leg_" + (index + 1).ToString("00");

            CreateSafetyBox(
                prefix + "_Rail_Left",
                safetyRoot,
                railCenter - right * sideOffset + Vector3.up * railHeight * 0.5f,
                rotation,
                new Vector3(railThickness, railHeight, railLength),
                false,
                null,
                obstacleMaterial);
            CreateSafetyBox(
                prefix + "_Rail_Right",
                safetyRoot,
                railCenter + right * sideOffset + Vector3.up * railHeight * 0.5f,
                rotation,
                new Vector3(railThickness, railHeight, railLength),
                false,
                null,
                obstacleMaterial);
            CreateSafetyBox(
                prefix + "_OutOfBounds_Floor",
                safetyRoot,
                center + Vector3.down * 0.55f,
                rotation,
                new Vector3(laneWidth + railThickness * 2f, 0.08f, outOfBoundsLength),
                true,
                "OutOfBounds",
                groundMaterial);
        }
    }

    private static void CreateSafetyBox(
        string name,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        Vector3 size,
        bool isTrigger,
        string tagName,
        PhysicsMaterial material)
    {
        GameObject safetyObject = CreateGameObject(name, parent, parent.gameObject.scene);
        Undo.RecordObject(safetyObject.transform, "Position safety collider");
        safetyObject.transform.SetPositionAndRotation(position, rotation);
        if (!string.IsNullOrEmpty(tagName))
        {
            Undo.RecordObject(safetyObject, "Tag OutOfBounds safety floor");
            safetyObject.tag = tagName;
        }

        BoxCollider collider = Undo.AddComponent<BoxCollider>(safetyObject);
        collider.size = size;
        collider.isTrigger = isTrigger;
        collider.sharedMaterial = material;
        GameObjectUtility.SetStaticEditorFlags(
            safetyObject,
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic);
    }

    private static GameObject PlaceConnectedPrefab(
        string prefabPath,
        Transform parent,
        string name,
        Vector3 position,
        Quaternion rotation,
        PhysicsMaterial material,
        bool configureSurfaceColliders = true)
    {
        GameObject prefab = LoadRequiredAsset<GameObject>(prefabPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
        if (instance == null)
        {
            throw new InvalidOperationException("Could not instantiate required prefab: " + prefabPath);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Create connected course prefab");
        Undo.RecordObject(instance, "Name connected course prefab");
        Undo.RecordObject(instance.transform, "Position connected course prefab");
        instance.name = name;
        instance.transform.SetPositionAndRotation(position, rotation);
        if (configureSurfaceColliders)
        {
            EnsureStaticMeshColliders(instance, material);
        }

        SetStaticRecursively(instance);
        return instance;
    }

    private static void EnsureStaticMeshColliders(GameObject instance, PhysicsMaterial material)
    {
        foreach (MeshFilter meshFilter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            Collider collider = meshFilter.GetComponent<Collider>();
            if (collider == null)
            {
                MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(meshFilter.gameObject);
                meshCollider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
        {
            Undo.RecordObject(collider, "Assign mini golf physics material");
            collider.sharedMaterial = material;
            EditorUtility.SetDirty(collider);
        }
    }

    private static void ConfigureOutOfBoundsPlane(Scene scene, PhysicsMaterial groundMaterial)
    {
        GameObject plane = FindRoot(scene, "Plane");
        if (plane == null)
        {
            throw new InvalidOperationException(scene.name + " does not contain the required root Plane.");
        }

        Undo.RecordObject(plane, "Tag outer Plane as OutOfBounds");
        plane.tag = "OutOfBounds";
        EnsureStaticMeshColliders(plane, groundMaterial);

        Collider[] colliders = plane.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            BoxCollider fallback = Undo.AddComponent<BoxCollider>(plane);
            Bounds bounds = CalculateWorldBounds(plane);
            fallback.center = plane.transform.InverseTransformPoint(bounds.center);
            fallback.size = new Vector3(
                Mathf.Max(1f, bounds.size.x),
                Mathf.Max(0.1f, bounds.size.y),
                Mathf.Max(1f, bounds.size.z));
            fallback.sharedMaterial = groundMaterial;
            colliders = new Collider[] { fallback };
        }

        foreach (Collider collider in colliders)
        {
            Undo.RecordObject(collider, "Enable OutOfBounds Plane collider");
            collider.enabled = true;
            collider.sharedMaterial = groundMaterial;
            EditorUtility.SetDirty(collider);
        }

        EditorUtility.SetDirty(plane);
    }

    private static void DisableLegacyCourseRoots(Scene scene)
    {
        foreach (string legacyName in LegacyRootNames)
        {
            foreach (GameObject legacyRoot in FindRoots(scene, legacyName))
            {
                DetachGolfHoles(legacyRoot);
                Undo.RecordObject(legacyRoot, "Disable legacy mini golf course root");
                legacyRoot.SetActive(false);
                EditorUtility.SetDirty(legacyRoot);
            }
        }
    }

    private static void DetachGolfHoles(GameObject parentRoot)
    {
        GolfHole[] holes = parentRoot.GetComponentsInChildren<GolfHole>(true);
        foreach (GolfHole hole in holes)
        {
            if (hole.gameObject == parentRoot)
            {
                throw new InvalidOperationException(
                    "Cannot preserve GolfHole because it is attached directly to course root " + parentRoot.name + ".");
            }

            Undo.SetTransformParent(hole.transform, null, "Preserve existing GolfHole");
            Undo.RecordObject(hole.gameObject, "Activate preserved GolfHole");
            hole.gameObject.SetActive(true);
            EditorUtility.SetDirty(hole.gameObject);
        }
    }

    private static bool ValidateSceneReadOnly(Scene scene, ScenePlan plan, System.Text.StringBuilder report)
    {
        bool passed = true;
        report.AppendLine(plan.DisplayName + " (" + plan.ScenePath + ")");

        List<GameObject> generatedRoots = FindRoots(scene, GeneratedRootName);
        GameObject generatedRoot = generatedRoots.Count == 1 ? generatedRoots[0] : null;
        bool rootValid = generatedRoot != null;
        Transform pieces = rootValid ? generatedRoot.transform.Find("Course_Pieces") : null;
        Transform obstacles = rootValid ? generatedRoot.transform.Find("Obstacles") : null;
        Transform gameplay = rootValid ? generatedRoot.transform.Find("Gameplay") : null;
        Transform safety = rootValid ? generatedRoot.transform.Find("Safety_Colliders") : null;
        bool groupsValid = rootValid && pieces != null && obstacles != null && gameplay != null && safety != null;
        AddCheck(
            report,
            ref passed,
            rootValid && groupsValid,
            "generated root/groups",
            "roots=" + generatedRoots.Count + ", Course_Pieces=" + (pieces != null) +
            ", Obstacles=" + (obstacles != null) + ", Gameplay=" + (gameplay != null) +
            ", Safety_Colliders=" + (safety != null));

        Dictionary<string, int> expectedPathCounts = BuildExpectedPrefabCounts(plan);
        Dictionary<string, int> actualPathCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int connectedPieces = CountGeneratedConnectedPrefabs(pieces, null, actualPathCounts);
        int connectedObstacles = CountGeneratedConnectedPrefabs(obstacles, null, actualPathCounts);
        int connectedGameplay = CountGeneratedConnectedPrefabs(gameplay, "Gameplay_Flag", actualPathCounts);
        int expectedPieces = CountPieces(plan, false);
        int expectedObstacles = CountPieces(plan, true);
        int expectedTotal = plan.Pieces.Length + 1;
        bool prefabCountsValid = connectedPieces == expectedPieces &&
                                 connectedObstacles == expectedObstacles &&
                                 connectedGameplay == 1 &&
                                 connectedPieces + connectedObstacles + connectedGameplay == expectedTotal;

        foreach (KeyValuePair<string, int> expected in expectedPathCounts)
        {
            int actual;
            actualPathCounts.TryGetValue(expected.Key, out actual);
            prefabCountsValid &= actual == expected.Value;
        }

        AddCheck(
            report,
            ref passed,
            prefabCountsValid,
            "connected prefab exact minimum counts",
            "Course_Pieces=" + connectedPieces + "/" + expectedPieces +
            ", Obstacles=" + connectedObstacles + "/" + expectedObstacles +
            ", Gameplay flag=" + connectedGameplay + "/1, total=" +
            (connectedPieces + connectedObstacles + connectedGameplay) + "/" + expectedTotal);

        string prefabRotationDetail;
        bool prefabRotationsValid = ValidateGeneratedPrefabRotations(
            plan,
            pieces,
            obstacles,
            gameplay,
            out prefabRotationDetail);
        AddCheck(
            report,
            ref passed,
            prefabRotationsValid,
            "connected prefab plan rotations",
            prefabRotationDetail);

        AddSingletonCheck<BallController>(scene, plan, report, ref passed);
        AddSingletonCheck<VRGolfClub>(scene, plan, report, ref passed);
        AddSingletonCheck<GolfHole>(scene, plan, report, ref passed);
        AddSingletonCheck<GameManager>(scene, plan, report, ref passed);
        AddSingletonCheck<MetaLevelTransitionUI>(scene, plan, report, ref passed);

        List<GolfHole> holes = FindComponentsInScene<GolfHole>(scene);
        bool holeTriggerValid = holes.Count == 1 &&
                                Array.Exists(holes[0].GetComponentsInChildren<Collider>(true), collider =>
                                    collider.enabled && collider.isTrigger);
        AddCheck(report, ref passed, holeTriggerValid, "GolfHole trigger collider", holeTriggerValid ? "present" : "missing");

        List<BallController> balls = FindComponentsInScene<BallController>(scene);
        Rigidbody ballBody = balls.Count == 1 ? balls[0].GetComponentInParent<Rigidbody>() : null;
        bool ballBodyValid = ballBody != null && !ballBody.isKinematic &&
                             ballBody.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic;
        AddCheck(
            report,
            ref passed,
            ballBodyValid,
            "ball Rigidbody",
            ballBody == null
                ? "missing"
                : "dynamic=" + (!ballBody.isKinematic) + ", mode=" + ballBody.collisionDetectionMode);

        int outOfBoundsColliderCount = CountEnabledOutOfBoundsColliders(scene);
        AddCheck(
            report,
            ref passed,
            outOfBoundsColliderCount >= 3,
            "OutOfBounds colliders",
            outOfBoundsColliderCount + "/3 minimum");

        GameObject plane = FindRoot(scene, "Plane");
        bool planeValid = plane != null && plane.activeInHierarchy && plane.CompareTag("OutOfBounds") &&
                          Array.Exists(plane.GetComponentsInChildren<Collider>(true), collider =>
                              collider.enabled && collider.gameObject.activeInHierarchy);
        AddCheck(report, ref passed, planeValid, "root Plane catch surface", planeValid ? "tagged and enabled" : "invalid");

        int missingScripts = CountMissingScripts(scene);
        AddCheck(report, ref passed, missingScripts == 0, "missing scripts", missingScripts.ToString());

        bool enabledInBuildSettings = Array.Exists(
            EditorBuildSettings.scenes,
            buildScene => buildScene.enabled &&
                          string.Equals(buildScene.path, plan.ScenePath, StringComparison.OrdinalIgnoreCase));
        AddCheck(
            report,
            ref passed,
            enabledInBuildSettings,
            "Build Settings",
            enabledInBuildSettings ? "enabled" : "disabled or absent");

        report.AppendLine("  RESULT: " + (passed ? "PASS" : "FAIL"));
        return passed;
    }

    private static int CountGeneratedConnectedPrefabs(
        Transform parent,
        string requiredName,
        Dictionary<string, int> pathCounts)
    {
        if (parent == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Transform child in parent)
        {
            if (requiredName != null && !string.Equals(child.name, requiredName, StringComparison.Ordinal))
            {
                continue;
            }

            if (PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject) != child.gameObject ||
                PrefabUtility.GetPrefabInstanceStatus(child.gameObject) != PrefabInstanceStatus.Connected)
            {
                continue;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
            int existing;
            pathCounts.TryGetValue(path, out existing);
            pathCounts[path] = existing + 1;
            count++;
        }

        return count;
    }

    private static bool ValidateGeneratedPrefabRotations(
        ScenePlan plan,
        Transform pieces,
        Transform obstacles,
        Transform gameplay,
        out string detail)
    {
        const float RotationTolerance = 0.1f;
        int validCount = 0;
        List<string> mismatches = new List<string>();
        PieceSpec? holePiece = null;

        foreach (PieceSpec piece in plan.Pieces)
        {
            Transform parent = piece.IsObstacle ? obstacles : pieces;
            Transform instance = parent != null ? parent.Find(piece.Name) : null;
            Quaternion expectedRotation = Quaternion.Euler(0f, piece.QuarterTurns * 90f, 0f);
            if (IsConnectedPrefabAtRotation(instance, expectedRotation, RotationTolerance))
            {
                validCount++;
            }
            else
            {
                mismatches.Add(piece.Name);
            }

            if (piece.PrefabPath == HolePrefabPath)
            {
                holePiece = piece;
            }
        }

        Transform flag = gameplay != null ? gameplay.Find("Gameplay_Flag") : null;
        Quaternion flagRotation = holePiece.HasValue
            ? Quaternion.Euler(0f, holePiece.Value.QuarterTurns * 90f, 0f)
            : Quaternion.identity;
        if (holePiece.HasValue && IsConnectedPrefabAtRotation(flag, flagRotation, RotationTolerance))
        {
            validCount++;
        }
        else
        {
            mismatches.Add("Gameplay_Flag");
        }

        int expectedCount = plan.Pieces.Length + 1;
        detail = validCount + "/" + expectedCount + " match plan";
        if (mismatches.Count > 0)
        {
            detail += "; mismatches=" + string.Join(", ", mismatches.ToArray());
        }

        return validCount == expectedCount;
    }

    private static bool IsConnectedPrefabAtRotation(
        Transform instance,
        Quaternion expectedRotation,
        float tolerance)
    {
        return instance != null &&
               PrefabUtility.GetOutermostPrefabInstanceRoot(instance.gameObject) == instance.gameObject &&
               PrefabUtility.GetPrefabInstanceStatus(instance.gameObject) == PrefabInstanceStatus.Connected &&
               Quaternion.Angle(instance.rotation, expectedRotation) <= tolerance;
    }

    private static Dictionary<string, int> BuildExpectedPrefabCounts(ScenePlan plan)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (PieceSpec piece in plan.Pieces)
        {
            int existing;
            counts.TryGetValue(piece.PrefabPath, out existing);
            counts[piece.PrefabPath] = existing + 1;
        }

        counts[FlagPrefabPath] = 1;
        return counts;
    }

    private static int CountPieces(ScenePlan plan, bool obstacle)
    {
        int count = 0;
        foreach (PieceSpec piece in plan.Pieces)
        {
            if (piece.IsObstacle == obstacle)
            {
                count++;
            }
        }

        return count;
    }

    private static void AddSingletonCheck<T>(
        Scene scene,
        ScenePlan plan,
        System.Text.StringBuilder report,
        ref bool passed) where T : Component
    {
        int count = FindComponentsInScene<T>(scene).Count;
        AddCheck(report, ref passed, count == 1, typeof(T).Name, count + "/1");
    }

    private static void AddCheck(
        System.Text.StringBuilder report,
        ref bool overall,
        bool condition,
        string label,
        string detail)
    {
        report.AppendLine("  - " + label + ": " + (condition ? "PASS" : "FAIL") + " (" + detail + ")");
        overall &= condition;
    }

    private static int CountEnabledOutOfBoundsColliders(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.enabled && collider.gameObject.activeInHierarchy &&
                    HasOutOfBoundsTagInParents(collider.transform))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool HasOutOfBoundsTagInParents(Transform current)
    {
        while (current != null)
        {
            if (current.CompareTag("OutOfBounds"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static int CountMissingScripts(Scene scene)
    {
        int count = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                count += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            }
        }

        return count;
    }

    private static T RequireExactlyOne<T>(Scene scene, string displayName) where T : Component
    {
        List<T> components = FindComponentsInScene<T>(scene);
        if (components.Count != 1)
        {
            throw new InvalidOperationException(
                displayName + " requires exactly one " + typeof(T).Name + ", found " + components.Count + ".");
        }

        return components[0];
    }

    private static List<T> FindComponentsInScene<T>(Scene scene) where T : Component
    {
        List<T> results = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            results.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return results;
    }

    private static Transform FindCameraRig(Scene scene)
    {
        Transform inactiveMatch = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.IndexOf("Camera Rig", StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            if (root.activeInHierarchy)
            {
                return root.transform;
            }

            if (inactiveMatch == null)
            {
                inactiveMatch = root.transform;
            }
        }

        return inactiveMatch;
    }

    private static List<string> ValidateRequiredAssets()
    {
        List<string> issues = new List<string>();
        foreach (ScenePlan plan in Plans)
        {
            if (!File.Exists(plan.ScenePath))
            {
                issues.Add("missing scene " + plan.ScenePath);
            }

            foreach (PieceSpec piece in plan.Pieces)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(piece.PrefabPath) == null)
                {
                    issues.Add("missing prefab " + piece.PrefabPath);
                }
            }
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(FlagPrefabPath) == null)
        {
            issues.Add("missing prefab " + FlagPrefabPath);
        }

        if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(GroundMaterialPath) == null)
        {
            issues.Add("missing physics material " + GroundMaterialPath);
        }

        if (AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ObstacleMaterialPath) == null)
        {
            issues.Add("missing physics material " + ObstacleMaterialPath);
        }

        return issues;
    }

    private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new InvalidOperationException("Required asset was not found: " + path);
        }

        return asset;
    }

    private static GameObject CreateGameObject(string name, Transform parent, Scene scene)
    {
        GameObject gameObject = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }
        else
        {
            SceneManager.MoveGameObjectToScene(gameObject, scene);
        }

        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;
        gameObject.transform.localScale = Vector3.one;
        return gameObject;
    }

    private static void SetStaticRecursively(GameObject root)
    {
        StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
        {
            Undo.RecordObject(transform.gameObject, "Mark generated course geometry static");
            GameObjectUtility.SetStaticEditorFlags(transform.gameObject, flags);
        }
    }

    private static Bounds CalculatePrefabLocalBounds(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            throw new InvalidOperationException("Straight prefab has no renderer bounds: " + StraightPrefabPath);
        }

        Bounds result = default;
        bool initialized = false;
        foreach (Renderer renderer in renderers)
        {
            Bounds rendererBounds = renderer.localBounds;
            Vector3 min = rendererBounds.min;
            Vector3 max = rendererBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 worldPoint = renderer.transform.TransformPoint(corner);
                Vector3 rootLocalPoint = prefab.transform.InverseTransformPoint(worldPoint);
                if (!initialized)
                {
                    result = new Bounds(rootLocalPoint, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    result.Encapsulate(rootLocalPoint);
                }
            }
        }

        return result;
    }

    private static Bounds CalculateWorldBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int index = 1; index < colliders.Length; index++)
            {
                bounds.Encapsulate(colliders[index].bounds);
            }

            return bounds;
        }

        return new Bounds(root.transform.position, Vector3.one * 0.1f);
    }

    private static Vector3 GridPosition(int x, int z, float tileStep)
    {
        return new Vector3(x * tileStep, 0f, z * tileStep);
    }

    private static Scene GetOrOpenScene(string path, List<Scene> scenesOpenedByTool)
    {
        for (int index = 0; index < EditorSceneManager.sceneCount; index++)
        {
            Scene loaded = EditorSceneManager.GetSceneAt(index);
            if (string.Equals(loaded.path, path, StringComparison.OrdinalIgnoreCase))
            {
                return loaded;
            }
        }

        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        scenesOpenedByTool.Add(scene);
        return scene;
    }

    private static bool IsPlannedScene(string path)
    {
        foreach (ScenePlan plan in Plans)
        {
            if (string.Equals(plan.ScenePath, path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void QueueFinalPlannedSceneSave(string scenePath)
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            Scene restoredScene = SceneManager.GetSceneByPath(scenePath);
            if (!restoredScene.IsValid() || !restoredScene.isLoaded || !restoredScene.isDirty)
            {
                return;
            }

            if (!EditorSceneManager.SaveScene(restoredScene, scenePath))
            {
                Debug.LogError("Two-scene level design build could not complete its delayed active-scene save.");
            }
        };
    }

    private static void RestoreSceneSetup(Scene originalActiveScene, List<Scene> scenesOpenedByTool)
    {
        for (int index = scenesOpenedByTool.Count - 1; index >= 0; index--)
        {
            Scene openedScene = scenesOpenedByTool[index];
            if (openedScene.IsValid() && openedScene.isLoaded)
            {
                EditorSceneManager.CloseScene(openedScene, true);
            }
        }

        if (originalActiveScene.IsValid() && originalActiveScene.isLoaded)
        {
            SceneManager.SetActiveScene(originalActiveScene);
        }
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }
        }

        return null;
    }

    private static List<GameObject> FindRoots(Scene scene, string name)
    {
        List<GameObject> results = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                results.Add(root);
            }
        }

        return results;
    }

    private sealed class ScenePlan
    {
        public readonly string DisplayName;
        public readonly string ScenePath;
        public readonly PieceSpec[] Pieces;
        public readonly LegSpec[] Legs;

        public ScenePlan(string displayName, string scenePath, PieceSpec[] pieces, LegSpec[] legs)
        {
            DisplayName = displayName;
            ScenePath = scenePath;
            Pieces = pieces;
            Legs = legs;
        }
    }

    private struct PieceSpec
    {
        public readonly string Name;
        public readonly string PrefabPath;
        public readonly int X;
        public readonly int Z;
        public readonly int QuarterTurns;
        public readonly bool IsObstacle;

        public PieceSpec(string name, string prefabPath, int x, int z, int quarterTurns, bool isObstacle)
        {
            Name = name;
            PrefabPath = prefabPath;
            X = x;
            Z = z;
            QuarterTurns = quarterTurns;
            IsObstacle = isObstacle;
        }
    }

    private struct LegSpec
    {
        public readonly int StartX;
        public readonly int StartZ;
        public readonly int EndX;
        public readonly int EndZ;

        public LegSpec(int startX, int startZ, int endX, int endZ)
        {
            StartX = startX;
            StartZ = startZ;
            EndX = endX;
            EndZ = endZ;
        }
    }
}
