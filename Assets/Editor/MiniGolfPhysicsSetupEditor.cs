#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MiniGolfPhysicsSetupEditor
{
    private const string BallMaterialPath = "Assets/Physics/MiniGolfBall.physicMaterial";
    private const string GroundMaterialPath = "Assets/Physics/MiniGolfGround.physicMaterial";
    private const string ObstacleMaterialPath = "Assets/Physics/MiniGolfObstacle.physicMaterial";

    [MenuItem("Tools/VR Mini Golf/Fizik/Sahnedeki Toplari Dengele")]
    private static void TuneSceneBalls()
    {
        PhysicsMaterial ballMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BallMaterialPath);
        BallController[] balls = Object.FindObjectsByType<BallController>(FindObjectsInactive.Include);

        foreach (BallController ball in balls)
        {
            Rigidbody body = ball.GetComponent<Rigidbody>();
            if (body != null)
            {
                Undo.RecordObject(body, "Mini golf top fiziğini ayarla");
                body.mass = 0.04593f;
                body.linearDamping = 0.04f;
                body.angularDamping = 0.05f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.maxAngularVelocity = 120f;
                body.maxDepenetrationVelocity = 3f;
            }

            Collider[] colliders = ball.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                Undo.RecordObject(collider, "Mini golf top materyalini ayarla");
                collider.sharedMaterial = ballMaterial;
            }

            SerializedObject serializedBall = new SerializedObject(ball);
            SetFloat(serializedBall, "stopThreshold", 0.035f);
            SetFloat(serializedBall, "ballMass", 0.04593f);
            SetFloat(serializedBall, "linearDamping", 0.04f);
            SetFloat(serializedBall, "angularDamping", 0.05f);
            SetFloat(serializedBall, "maximumRollingAngularSpeed", 120f);
            SetFloat(serializedBall, "maximumDepenetrationVelocity", 3f);
            SetObject(serializedBall, "ballPhysicsMaterial", ballMaterial);
            SetFloat(serializedBall, "maximumLaunchSpeed", 3.6f);
            SetFloat(serializedBall, "grassFriction", 0.42f);
            SetFloat(serializedBall, "lowSpeedFrictionMultiplier", 0.85f);
            SetFloat(serializedBall, "highSpeedFrictionMultiplier", 0.62f);
            SetFloat(serializedBall, "frictionTransitionSpeed", 1.5f);
            SetFloat(serializedBall, "rollingSpinResponse", 7f);
            SetFloat(serializedBall, "extraGravity", 1f);
            SetBool(serializedBall, "detectWallsFromContactNormal", true);
            SetFloat(serializedBall, "maximumWallNormalY", 0.35f);
            SetFloat(serializedBall, "wallReflectionSpeedRetention", 0.98f);
            SetFloat(serializedBall, "wallSpeedLoss", 0.06f);
            SetFloat(serializedBall, "highSpeedAdditionalWallLoss", 0.08f);
            SetFloat(serializedBall, "speedForMaximumWallLoss", 3.6f);
            SetFloat(serializedBall, "wallSurfaceFriction", 0.02f);
            SetFloat(serializedBall, "minimumWallImpactSpeed", 0.08f);
            SetFloat(serializedBall, "minimumWallBounceSpeed", 0.1f);
            SetFloat(serializedBall, "wallSeparationSpeed", 0.08f);
            SetFloat(serializedBall, "wallSeparationDistance", 0.003f);
            SetInt(serializedBall, "physicsTuningVersion", 3);
            serializedBall.ApplyModifiedProperties();
        }

        MarkActiveSceneDirty();
        Debug.Log($"VR Mini Golf: {balls.Length} topun Rigidbody, yuvarlanma ve aynasal sekme değerleri ayarlandı.");
    }

    [MenuItem("Tools/VR Mini Golf/Fizik/Secili Colliderlari Zemin Yap")]
    private static void ApplyGroundMaterial()
    {
        ApplyMaterialToSelected(GroundMaterialPath, "mini golf zemin");
    }

    [MenuItem("Tools/VR Mini Golf/Fizik/Secili Colliderlari Engel Yap")]
    private static void ApplyObstacleMaterial()
    {
        ApplyMaterialToSelected(ObstacleMaterialPath, "mini golf engel");
    }

    [MenuItem("Tools/VR Mini Golf/Dogrulama/Top Fizigini Kontrol Et")]
    private static void ValidatePhysicsSetup()
    {
        BallController[] balls = Object.FindObjectsByType<BallController>(FindObjectsInactive.Include);
        int configuredBalls = 0;

        foreach (BallController ball in balls)
        {
            Rigidbody body = ball.GetComponent<Rigidbody>();
            Collider collider = ball.GetComponentInChildren<Collider>(true);
            if (body != null
                && body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic
                && collider != null
                && collider.sharedMaterial != null)
            {
                configuredBalls++;
            }
        }

        Debug.Log(
            "VR Mini Golf fizik doğrulaması:\n"
            + $"- Golf topu: {balls.Length} (Rigidbody ve materyal hazır: {configuredBalls})\n"
            + "- Duvar sekmesi BallController tarafından aynasal yansıma olarak uygulanır.");
    }

    private static void ApplyMaterialToSelected(string assetPath, string undoName)
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(assetPath);
        if (material == null)
        {
            Debug.LogError($"VR Mini Golf: Fizik materyali bulunamadı: {assetPath}");
            return;
        }

        GameObject[] selectedObjects = Selection.gameObjects;
        int colliderCount = 0;
        foreach (GameObject selectedObject in selectedObjects)
        {
            Collider[] colliders = selectedObject.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                Undo.RecordObject(collider, undoName);
                collider.sharedMaterial = material;
                EditorUtility.SetDirty(collider);
                colliderCount++;
            }
        }

        MarkActiveSceneDirty();
        Debug.Log($"VR Mini Golf: {colliderCount} Collider'a '{material.name}' uygulandı.");
    }

    private static void MarkActiveSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
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

    private static void SetObject(
        SerializedObject target,
        string propertyName,
        Object value)
    {
        SerializedProperty property = target.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }
}
#endif
