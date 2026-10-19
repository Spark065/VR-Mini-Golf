#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MiniGolfSceneSetupEditor
{
    private const string PendingRequestPath = "Library/CodexMiniGolfSetup.pending";
    private const string ReportPath = "Temp/CodexMiniGolfSetupReport.txt";
    private const string BallMaterialPath = "Assets/Physics/MiniGolfBall.physicMaterial";
    private const float GolfBallDiameter = 0.04267f;
    private const float PutterLength = 0.90f;
    private const float ReferenceClubHapticIntensity = 0.156f;
    private static readonly Vector3 ReferenceClubBodyColliderCenter =
        new Vector3(67.782364f, 2.9872422f, 34.605957f);
    private static readonly Vector3 ReferenceClubBodyColliderSize =
        new Vector3(0.22459733f, 1.3583515f, 0.17596492f);
    private static readonly Vector3 ReferenceClubHeadColliderCenter =
        new Vector3(0.07826064f, -0.106494054f, -0.09675603f);
    private static readonly Vector3 ReferenceClubHeadColliderSize =
        new Vector3(0.6644016f, 0.2921599f, 0.5501772f);

    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/SampleScene.unity",
        "Assets/Scenes/Scene 2.unity"
    };

    static MiniGolfSceneSetupEditor()
    {
        AssemblyReloadEvents.beforeAssemblyReload += ClearInspectorSelection;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.delayCall += ClearInspectorSelection;
        EditorApplication.delayCall += ProcessPendingRequest;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
        {
            ClearInspectorSelection();
        }
    }

    [MenuItem("Tools/VR Mini Golf/Sahneler/1 ve 2 Sahneyi Güvenli Kur")]
    public static void ConfigureBothScenes()
    {
        ClearInspectorSelection();
        ConfigureAllScenes();
    }

    [MenuItem("Tools/VR Mini Golf/Dogrulama/1 ve 2 Sahneyi Raporla")]
    public static void ValidateBothScenes()
    {
        ConfigureAllScenes(validateOnly: true);
    }

    private static void ProcessPendingRequest()
    {
        if (!File.Exists(PendingRequestPath)) return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += ProcessPendingRequest;
            return;
        }

        // The setup opens scenes and can remove duplicate scene components. If one of
        // those objects is selected, Unity's built-in Inspector keeps a destroyed target
        // during the following domain reload and logs SerializedObject/MissingReference
        // exceptions. Release the selection before touching serialized scene objects.
        ClearInspectorSelection();
        ConfigureAllScenes();
        File.Delete(PendingRequestPath);
        AssetDatabase.Refresh();
    }

    private static void ConfigureAllScenes(bool validateOnly = false)
    {
        if (!validateOnly)
        {
            ClearInspectorSelection();
        }

        string originalScenePath = SceneManager.GetActiveScene().path;
        List<string> report = new List<string>
        {
            $"VR Mini Golf scene setup - {System.DateTime.Now:O}",
            $"Mode: {(validateOnly ? "validation" : "configuration")}",
            $"Build Settings: {string.Join(", ", EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path))}"
        };

        try
        {
            foreach (string scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath))
                {
                    report.Add($"ERROR missing scene: {scenePath}");
                    continue;
                }

                Scene scene = FindLoadedScene(scenePath);
                if (!scene.IsValid())
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }

                SceneManager.SetActiveScene(scene);
                ConfigureScene(scene, validateOnly, report);
                if (!validateOnly)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(originalScenePath) && File.Exists(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllLines(ReportPath, report);
            Debug.Log(string.Join("\n", report));
        }
    }

    private static void ClearInspectorSelection()
    {
        Selection.objects = System.Array.Empty<Object>();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
    }

    private static void ConfigureScene(Scene scene, bool validateOnly, List<string> report)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        BallController[] balls = roots.SelectMany(root => root.GetComponentsInChildren<BallController>(true)).ToArray();
        VRGolfClub[] clubs = roots.SelectMany(root => root.GetComponentsInChildren<VRGolfClub>(true)).ToArray();
        GolfHole[] holes = roots.SelectMany(root => root.GetComponentsInChildren<GolfHole>(true)).ToArray();
        GameManager[] managers = roots.SelectMany(root => root.GetComponentsInChildren<GameManager>(true)).ToArray();
        MetaLevelTransitionUI[] metaUis = roots.SelectMany(root => root.GetComponentsInChildren<MetaLevelTransitionUI>(true)).ToArray();
        int removedNestedClubComponents = 0;

        if (!validateOnly)
        {
            removedNestedClubComponents = RemoveNestedClubComponents(clubs);
            roots = scene.GetRootGameObjects();
            clubs = roots.SelectMany(root => root.GetComponentsInChildren<VRGolfClub>(true)).ToArray();
        }

        if (!validateOnly && managers.Length == 0)
        {
            GameObject managerObject = new GameObject("GameManager");
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            managerObject.AddComponent<GameManager>();
            managers = new[] { managerObject.GetComponent<GameManager>() };
        }

        MetaLevelTransitionUI sceneUi = metaUis.FirstOrDefault();
        if (!validateOnly && sceneUi == null)
        {
            sceneUi = MetaLevelTransitionUI.CreateRuntime();
            sceneUi.name = "Mini Golf World Space UI";
            SceneManager.MoveGameObjectToScene(sceneUi.gameObject, scene);
            metaUis = new[] { sceneUi };
        }

        if (!validateOnly && sceneUi != null)
        {
            ConfigureWorldSpaceUi(sceneUi);
        }

        PhysicsMaterial ballMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BallMaterialPath);
        foreach (BallController ball in balls)
        {
            if (!validateOnly)
            {
                ConfigureBall(ball, ballMaterial, sceneUi);
            }
        }

        foreach (VRGolfClub club in clubs)
        {
            if (!validateOnly)
            {
                ConfigureClub(club, balls.FirstOrDefault());
            }
        }

        foreach (GolfHole hole in holes)
        {
            if (!validateOnly)
            {
                ConfigureHole(hole, sceneUi, clubs.FirstOrDefault());
            }
        }

        Physics.SyncTransforms();
        roots = scene.GetRootGameObjects();

        int metaBuildingBlocks = CountComponentsByTypeName(roots, "Meta.XR.BuildingBlocks.BuildingBlock");
        int metaRayInteractors = CountComponentsByTypeName(roots, "Oculus.Interaction.RayInteractor");
        int pointableModules = CountComponentsByTypeName(roots, "Oculus.Interaction.PointableCanvasModule");
        int outOfBounds = roots
            .SelectMany(root => root.GetComponentsInChildren<Collider>(true))
            .Count(collider => collider.CompareTag("OutOfBounds"));

        report.Add(
            $"SCENE {scene.path}: balls={balls.Length}, clubs={clubs.Length}, holes={holes.Length}, "
            + $"gameManagers={managers.Length}, worldUIs={metaUis.Length}, buildingBlocks={metaBuildingBlocks}, "
            + $"rayInteractors={metaRayInteractors}, pointableCanvasModules={pointableModules}, outOfBoundsColliders={outOfBounds}, "
            + $"removedNestedClubComponents={removedNestedClubComponents}");

        foreach (BallController ball in balls)
        {
            SphereCollider sphere = ball.GetComponentInChildren<SphereCollider>(true);
            Rigidbody body = ball.GetComponent<Rigidbody>();
            float diameter = sphere != null ? GetWorldSphereDiameter(sphere) : -1f;
            report.Add(
                $"  BALL {GetHierarchyPath(ball.transform)} diameter={diameter:F5}m "
                + $"mass={(body != null ? body.mass : -1f):F5}kg kinematic={(body != null && body.isKinematic)} "
                + $"constraints={(body != null ? body.constraints.ToString() : "missing")}");
        }

        foreach (VRGolfClub club in clubs)
        {
            Grabbable metaGrabbable = club.GetComponentInParent<Grabbable>();
            Rigidbody body = metaGrabbable != null
                ? metaGrabbable.GetComponent<Rigidbody>()
                : club.GetComponent<Rigidbody>();
            int distanceGrabInteractables = metaGrabbable != null
                ? metaGrabbable.GetComponentsInChildren<DistanceGrabInteractable>(true).Length
                : 0;
            Bounds clubBounds = CalculateRendererBounds(club.gameObject);
            float clubLength = Mathf.Max(clubBounds.size.x, Mathf.Max(clubBounds.size.y, clubBounds.size.z));
            report.Add(
                $"  CLUB {GetHierarchyPath(club.transform)} length={clubLength:F5}m "
                + $"MetaGrabbable={(metaGrabbable != null)} legacyOVRGrabbable={(club.GetComponent<OVRGrabbable>() != null)} "
                + $"distanceGrabInteractables={distanceGrabInteractables} "
                + $"mass={(body != null ? body.mass : -1f):F2}kg gravity={(body != null && body.useGravity)} "
                + $"kinematic={(body != null && body.isKinematic)} constraints={(body != null ? body.constraints.ToString() : "missing")}");
        }

        foreach (MetaLevelTransitionUI metaUi in metaUis)
        {
            SerializedObject serializedUi = new SerializedObject(metaUi);
            report.Add(
                $"  UI {GetHierarchyPath(metaUi.transform)} renderMode={metaUi.Canvas.renderMode} "
                + $"parent={(metaUi.transform.parent != null ? GetHierarchyPath(metaUi.transform.parent) : "scene-root")} "
                + $"follow={GetBool(serializedUi, "followViewer")} distance={GetFloat(serializedUi, "followDistance"):F2}m "
                + $"horizontalOffset={GetFloat(serializedUi, "horizontalOffset"):F2}m "
                + $"verticalOffset={GetFloat(serializedUi, "verticalOffset"):F2}m "
                + $"maxViewAngle={GetFloat(serializedUi, "maximumViewAngle"):F1}deg");
        }
    }

    private static void ConfigureWorldSpaceUi(MetaLevelTransitionUI sceneUi)
    {
        Canvas uiCanvas = sceneUi.Canvas != null ? sceneUi.Canvas : sceneUi.GetComponent<Canvas>();
        if (uiCanvas != null)
        {
            uiCanvas.renderMode = RenderMode.WorldSpace;
            uiCanvas.sortingOrder = 100;
            EditorUtility.SetDirty(uiCanvas);
        }

        Camera viewerCamera = Camera.main;
        if (viewerCamera == null)
        {
            viewerCamera = Object.FindAnyObjectByType<Camera>();
        }

        SerializedObject serializedUi = new SerializedObject(sceneUi);
        SetObject(serializedUi, "viewerCamera", viewerCamera);
        SetBool(serializedUi, "followViewer", true);
        SetFloat(serializedUi, "followDistance", 1.35f);
        SetFloat(serializedUi, "horizontalOffset", 0f);
        SetFloat(serializedUi, "verticalOffset", -0.08f);
        SetFloat(serializedUi, "positionFollowSpeed", 4f);
        SetFloat(serializedUi, "rotationFollowSpeed", 5f);
        SetFloat(serializedUi, "maximumViewAngle", 35f);
        SetFloat(serializedUi, "maximumPositionLag", 0.35f);
        serializedUi.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sceneUi);
    }

    private static void ConfigureBall(
        BallController ball,
        PhysicsMaterial ballMaterial,
        MetaLevelTransitionUI sceneUi)
    {
        Rigidbody body = ball.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.mass = 0.04593f;
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.None;
            body.linearDamping = 0.04f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 120f;
            body.maxDepenetrationVelocity = 3f;
            EditorUtility.SetDirty(body);
        }

        SphereCollider sphere = ball.GetComponentInChildren<SphereCollider>(true);
        if (sphere != null)
        {
            float currentDiameter = GetWorldSphereDiameter(sphere);
            if (currentDiameter > 0.0001f)
            {
                float scaleFactor = GolfBallDiameter / currentDiameter;
                ball.transform.localScale *= scaleFactor;
            }
        }

        foreach (Collider collider in ball.GetComponentsInChildren<Collider>(true))
        {
            if (ballMaterial != null) collider.sharedMaterial = ballMaterial;
            EditorUtility.SetDirty(collider);
        }

        SerializedObject serializedBall = new SerializedObject(ball);
        SetObject(serializedBall, "ballPhysicsMaterial", ballMaterial);
        SetFloat(serializedBall, "stopThreshold", 0.035f);
        SetFloat(serializedBall, "maximumLaunchSpeed", 3.6f);
        SetFloat(serializedBall, "grassFriction", 0.42f);
        SetFloat(serializedBall, "lowSpeedFrictionMultiplier", 0.85f);
        SetFloat(serializedBall, "highSpeedFrictionMultiplier", 0.62f);
        SetFloat(serializedBall, "frictionTransitionSpeed", 1.5f);
        SetFloat(serializedBall, "extraGravity", 1f);
        SetFloat(serializedBall, "wallReflectionSpeedRetention", 0.98f);
        SetFloat(serializedBall, "minimumWallImpactSpeed", 0.08f);
        SetFloat(serializedBall, "respawnHeightOffset", 0.015f);
        SetFloat(serializedBall, "checkpointUpdateInterval", 0.1f);
        SetFloat(serializedBall, "respawnCooldown", 0.5f);
        SetInt(serializedBall, "physicsTuningVersion", 4);

        SerializedProperty legacyCanvasProperty = serializedBall.FindProperty("legacyLevelCompleteCanvas");
        GameObject legacyCanvasObject = legacyCanvasProperty != null
            ? legacyCanvasProperty.objectReferenceValue as GameObject
            : null;
        Canvas legacyCanvas = legacyCanvasObject != null
            ? legacyCanvasObject.GetComponentInParent<Canvas>(true)
            : null;
        if (legacyCanvas != null && (sceneUi == null || legacyCanvas != sceneUi.Canvas))
        {
            legacyCanvas.gameObject.SetActive(false);
            EditorUtility.SetDirty(legacyCanvas.gameObject);
        }

        serializedBall.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ball);
    }

    private static void ConfigureClub(VRGolfClub club, BallController targetBall)
    {
        Bounds bounds = CalculateRendererBounds(club.gameObject);
        float currentLength = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (currentLength > 0.001f)
        {
            Vector3 visualCenterBeforeScale = bounds.center;
            float scaleFactor = PutterLength / currentLength;
            club.transform.localScale *= scaleFactor;
            Physics.SyncTransforms();

            // Imported club models can have a pivot far outside the mesh. Preserve the
            // rendered center so scaling the child never separates it from its grab root.
            Bounds scaledBounds = CalculateRendererBounds(club.gameObject);
            club.transform.position += visualCenterBeforeScale - scaledBounds.center;
            Physics.SyncTransforms();
        }

        Grabbable metaGrabbable = club.GetComponentInParent<Grabbable>();
        Rigidbody clubBody = club.GetComponent<Rigidbody>();
        Rigidbody grabBody = metaGrabbable != null
            ? metaGrabbable.GetComponent<Rigidbody>()
            : clubBody;

        foreach (Rigidbody nestedBody in club.GetComponentsInChildren<Rigidbody>(true))
        {
            if (nestedBody == clubBody || nestedBody == grabBody) continue;
            Object.DestroyImmediate(nestedBody);
        }

        ConfigureClubBody(grabBody);
        if (clubBody != grabBody)
        {
            ConfigureClubBody(clubBody);
        }

        if (metaGrabbable != null)
        {
            bool isScene2 = string.Equals(
                club.gameObject.scene.path,
                "Assets/Scenes/Scene 2.unity",
                System.StringComparison.OrdinalIgnoreCase);
            metaGrabbable.MaxGrabPoints = 1;
            metaGrabbable.TransferOnSecondSelection = true;
            metaGrabbable.InjectOptionalThrowWhenUnselected(false);
            metaGrabbable.InjectOptionalKinematicWhileSelected(true);

            foreach (GrabInteractable interactable in metaGrabbable.GetComponentsInChildren<GrabInteractable>(true))
            {
                interactable.MaxSelectingInteractors = 1;
                interactable.ResetGrabOnGrabsUpdated = true;
                EditorUtility.SetDirty(interactable);
            }

            foreach (HandGrabInteractable interactable in metaGrabbable.GetComponentsInChildren<HandGrabInteractable>(true))
            {
                interactable.MaxSelectingInteractors = 1;
                interactable.ResetGrabOnGrabsUpdated = true;
                EditorUtility.SetDirty(interactable);
            }

            foreach (DistanceGrabInteractable interactable
                     in metaGrabbable.GetComponentsInChildren<DistanceGrabInteractable>(true))
            {
                interactable.enabled = !isScene2;
                interactable.MaxSelectingInteractors = 1;
                interactable.ResetGrabOnGrabsUpdated = true;
                EditorUtility.SetDirty(interactable);
            }

            BoxCollider grabCollider = metaGrabbable.GetComponent<BoxCollider>();
            if (grabCollider != null)
            {
                grabCollider.isTrigger = true;
                EditorUtility.SetDirty(grabCollider);
            }

            // Comprehensive Interaction Rig, OVRGrabber kullanmaz. Çocuk sopadaki eski
            // OVRGrabbable ikinci bir grab otoritesi oluşturmasın.
            OVRGrabbable legacyGrabbable = club.GetComponent<OVRGrabbable>();
            if (legacyGrabbable != null)
            {
                Object.DestroyImmediate(legacyGrabbable);
            }

            EditorUtility.SetDirty(metaGrabbable);
        }

        SerializedObject serializedClub = new SerializedObject(club);
        ConfigureReferenceClubBodyCollider(club);
        ConfigureReferenceClubHeadCollider(serializedClub);
        SetFloat(serializedClub, "minimumClubHeadSpeed", 0.1f);
        SetFloat(serializedClub, "clubHeadSpeedForFullPutt", 2f);
        SetFloat(serializedClub, "minimumBallLaunchSpeed", 0.25f);
        SetFloat(serializedClub, "maximumBallLaunchSpeed", 4.5f);
        SetFloat(serializedClub, "puttResponseExponent", 1.5f);
        SetFloat(serializedClub, "swingSmoothingTime", 0.06f);
        SetFloat(serializedClub, "maximumBallSpeedForStroke", 0.12f);
        SetFloat(serializedClub, "minimumApproachDot", 0.05f);
        SetFloat(serializedClub, "hitCooldown", 0.2f);
        SetFloat(serializedClub, "maxHapticIntensity", ReferenceClubHapticIntensity);
        SetFloat(serializedClub, "hapticDuration", 0.1f);
        SetBool(serializedClub, "limitToSingleMetaGrab", true);
        SetBool(serializedClub, "transferOnSecondMetaGrab", true);
        SetBool(
            serializedClub,
            "enableControllerDistanceGrab",
            !string.Equals(
                club.gameObject.scene.path,
                "Assets/Scenes/Scene 2.unity",
                System.StringComparison.OrdinalIgnoreCase));
        SetBool(serializedClub, "enforceRealPutterLength", true);
        SetFloat(serializedClub, "targetPutterLength", PutterLength);
        SetBool(serializedClub, "suspendWhenNotHeld", true);
        SetFloat(serializedClub, "clubMass", 0.45f);
        SetFloat(serializedClub, "clubLinearDamping", 0f);
        SetFloat(serializedClub, "clubAngularDamping", 0.05f);
        SetBool(serializedClub, "clampHeldClubAboveGround", false);
        SetFloat(serializedClub, "groundClearance", 0.004f);
        SetFloat(serializedClub, "groundProbeHeight", 0.6f);
        SetObject(serializedClub, "targetBall", targetBall);
        serializedClub.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(club);
    }

    private static void ConfigureReferenceClubBodyCollider(VRGolfClub club)
    {
        Transform visualRoot = club.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(candidate => candidate.name == "Golfclub1");
        if (visualRoot == null) return;

        BoxCollider bodyCollider = visualRoot.GetComponent<BoxCollider>();
        if (bodyCollider == null)
        {
            bodyCollider = visualRoot.gameObject.AddComponent<BoxCollider>();
        }

        bodyCollider.center = ReferenceClubBodyColliderCenter;
        bodyCollider.size = ReferenceClubBodyColliderSize;
        bodyCollider.isTrigger = false;
        EditorUtility.SetDirty(bodyCollider);
    }

    private static void ConfigureReferenceClubHeadCollider(SerializedObject serializedClub)
    {
        SerializedProperty headColliderProperty = serializedClub.FindProperty("clubHeadCollider");
        BoxCollider headCollider = headColliderProperty != null
            ? headColliderProperty.objectReferenceValue as BoxCollider
            : null;
        if (headCollider == null) return;

        headCollider.center = ReferenceClubHeadColliderCenter;
        headCollider.size = ReferenceClubHeadColliderSize;
        headCollider.isTrigger = false;
        EditorUtility.SetDirty(headCollider);
    }

    private static void ConfigureClubBody(Rigidbody body)
    {
        if (body == null) return;

        body.mass = 0.45f;
        body.linearDamping = 0f;
        body.angularDamping = 0.05f;
        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.None;
        body.interpolation = RigidbodyInterpolation.None;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        EditorUtility.SetDirty(body);
    }

    private static int RemoveNestedClubComponents(VRGolfClub[] clubs)
    {
        int removedCount = 0;
        foreach (VRGolfClub club in clubs.OrderByDescending(item => GetHierarchyDepth(item.transform)))
        {
            VRGolfClub parentClub = club.transform.parent != null
                ? club.transform.parent.GetComponentInParent<VRGolfClub>(true)
                : null;
            if (parentClub == null) continue;

            OVRGrabbable duplicateGrabbable = club.GetComponent<OVRGrabbable>();
            if (duplicateGrabbable != null)
            {
                Object.DestroyImmediate(duplicateGrabbable);
                removedCount++;
            }

            Object.DestroyImmediate(club);
            removedCount++;
        }

        return removedCount;
    }

    private static void ConfigureHole(
        GolfHole hole,
        MetaLevelTransitionUI sceneUi,
        VRGolfClub golfClub)
    {
        Collider trigger = hole.GetComponent<Collider>();
        if (trigger == null)
        {
            SphereCollider sphere = hole.gameObject.AddComponent<SphereCollider>();
            sphere.radius = 0.06f;
            trigger = sphere;
        }

        trigger.isTrigger = true;
        SerializedObject serializedHole = new SerializedObject(hole);
        SetObject(serializedHole, "levelCompleteUI", sceneUi);
        SetObject(serializedHole, "golfClub", golfClub);
        SetBool(serializedHole, "pauseGameplayOnComplete", true);
        serializedHole.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(trigger);
        EditorUtility.SetDirty(hole);
    }

    private static float GetWorldSphereDiameter(SphereCollider sphere)
    {
        Vector3 scale = sphere.transform.lossyScale;
        float maximumScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        return sphere.radius * 2f * maximumScale;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static int CountComponentsByTypeName(GameObject[] roots, string typeName)
    {
        return roots
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .Count(component => component != null && component.GetType().FullName == typeName);
    }

    private static Scene FindLoadedScene(string scenePath)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.path == scenePath) return scene;
        }

        return default;
    }

    private static string GetHierarchyPath(Transform target)
    {
        string path = target.name;
        while (target.parent != null)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private static int GetHierarchyDepth(Transform target)
    {
        int depth = 0;
        while (target.parent != null)
        {
            depth++;
            target = target.parent;
        }

        return depth;
    }

    private static void SetFloat(SerializedObject target, string propertyName, float value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null) property.floatValue = value;
    }

    private static void SetBool(SerializedObject target, string propertyName, bool value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
    }

    private static void SetInt(SerializedObject target, string propertyName, int value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null) property.intValue = value;
    }

    private static void SetObject(SerializedObject target, string propertyName, Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }

    private static float GetFloat(SerializedObject target, string propertyName)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        return property != null ? property.floatValue : 0f;
    }

    private static bool GetBool(SerializedObject target, string propertyName)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        return property != null && property.boolValue;
    }
}
#endif
