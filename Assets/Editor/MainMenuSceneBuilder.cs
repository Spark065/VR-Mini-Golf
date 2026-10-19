using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Oculus.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MainMenuSceneBuilder
{
    private const string MenuPath = MiniGolfSceneCatalog.MainMenuScenePath;
    private const string SourcePath = MiniGolfSceneCatalog.FirstCourseScenePath;
    private const string SecondCoursePath = MiniGolfSceneCatalog.SecondCourseScenePath;
    private const string ReportPath = "Temp/MainMenuValidationReport.txt";

    private static readonly string[] RequiredSourceRoots =
    {
        "[BuildingBlock] Camera Rig",
        "EventSystem",
        "Directional Light",
        "Global Volume"
    };

    [MenuItem("Tools/VR Mini Golf/Main Menu/Menü Sahnesini Kur ve Doğrula")]
    public static void BuildAndValidate()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("Menü sahnesi Play Mode sırasında kurulamaz.");
        }

        RequireCleanLoadedScenes();
        RequireAsset(SourcePath);
        RequireAsset(SecondCoursePath);

        Scene sourceScene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
        Scene menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(menuScene);

        foreach (string rootName in RequiredSourceRoots)
        {
            CloneRootIntoScene(sourceScene, menuScene, rootName);
        }

        GameObject cameraRig = FindRoot(menuScene, "[BuildingBlock] Camera Rig");
        cameraRig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        GameObject menuObject = new GameObject(
            "Main Menu World Space UI",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(menuObject, menuScene);

        MiniGolfMainMenu mainMenu = menuObject.AddComponent<MiniGolfMainMenu>();
        mainMenu.RebuildForEditor();
        menuObject.transform.SetPositionAndRotation(new Vector3(0f, 1.55f, 1.55f), Quaternion.identity);

        EditorSceneManager.MarkSceneDirty(menuScene);
        if (!EditorSceneManager.SaveScene(menuScene, MenuPath))
        {
            throw new IOException($"Menü sahnesi kaydedilemedi: {MenuPath}");
        }

        EditorSceneManager.CloseScene(sourceScene, true);
        SceneManager.SetActiveScene(menuScene);

        EnsureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        List<string> report = Validate(menuScene);
        WriteReport(report);

        bool passed = report.Any(line => line == "OVERALL: PASS");
        if (!passed)
        {
            throw new InvalidOperationException(
                "Ana menü doğrulaması başarısız. Ayrıntılar: " + ReportPath);
        }

        Selection.activeGameObject = menuObject;
        EditorGUIUtility.PingObject(menuObject);
        Debug.Log($"Ana menü sahnesi kuruldu ve doğrulandı. Rapor: {ReportPath}", menuObject);
    }

    [MenuItem("Tools/VR Mini Golf/Main Menu/Açık Menü Sahnesini Doğrula")]
    public static void ValidateOpenMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<string> report = Validate(scene);
        WriteReport(report);

        if (!report.Any(line => line == "OVERALL: PASS"))
        {
            throw new InvalidOperationException(
                "Ana menü doğrulaması başarısız. Ayrıntılar: " + ReportPath);
        }

        Debug.Log($"Ana menü doğrulaması başarılı. Rapor: {ReportPath}");
    }

    private static void EnsureBuildSettings()
    {
        List<EditorBuildSettingsScene> existing = EditorBuildSettings.scenes.ToList();
        List<EditorBuildSettingsScene> ordered = new List<EditorBuildSettingsScene>
        {
            EnabledScene(MenuPath),
            EnabledScene(SourcePath),
            EnabledScene(SecondCoursePath)
        };

        foreach (EditorBuildSettingsScene scene in existing)
        {
            if (ordered.Any(item => PathsMatch(item.path, scene.path))) continue;
            ordered.Add(scene);
        }

        EditorBuildSettings.scenes = ordered.ToArray();
    }

    private static EditorBuildSettingsScene EnabledScene(string path)
    {
        SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        if (asset == null)
        {
            throw new FileNotFoundException("Build Settings sahnesi bulunamadı.", path);
        }

        return new EditorBuildSettingsScene(path, true);
    }

    private static List<string> Validate(Scene scene)
    {
        List<string> report = new List<string>
        {
            "VR MINI GOLF - MAIN MENU VALIDATION",
            $"Timestamp: {DateTime.Now:O}",
            $"Scene: {scene.path}",
            string.Empty
        };

        bool passed = true;
        Check(
            PathsMatch(scene.path, MenuPath),
            "Active scene is Assets/Scenes/MainMenu.unity",
            report,
            ref passed);

        GameObject[] roots = scene.IsValid() && scene.isLoaded
            ? scene.GetRootGameObjects()
            : Array.Empty<GameObject>();
        MiniGolfMainMenu[] menus = roots
            .SelectMany(root => root.GetComponentsInChildren<MiniGolfMainMenu>(true))
            .ToArray();

        Check(menus.Length == 1, "Exactly one MiniGolfMainMenu", report, ref passed);
        MiniGolfMainMenu menu = menus.FirstOrDefault();
        if (menu != null)
        {
            Check(menu.Canvas != null, "Menu Canvas reference assigned", report, ref passed);
            Check(
                menu.Canvas != null && menu.Canvas.renderMode == RenderMode.WorldSpace,
                "Menu Canvas uses World Space",
                report,
                ref passed);
            Check(menu.MainPanel != null, "Main panel assigned", report, ref passed);
            Check(menu.LoadingPanel != null, "Loading panel assigned", report, ref passed);
            Check(menu.FirstCourseButton != null, "First-course button assigned", report, ref passed);
            Check(menu.SecondCourseButton != null, "Second-course button assigned", report, ref passed);
            Check(menu.QuitButton != null, "Quit button assigned", report, ref passed);
            Check(
                menu.GetComponent<PointableCanvas>() != null,
                "Meta PointableCanvas configured",
                report,
                ref passed);
            Check(
                menu.GetComponent<RayInteractable>() != null,
                "Meta RayInteractable configured",
                report,
                ref passed);
        }

        Check(
            roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true)).Any(),
            "XR camera rig contains cameras",
            report,
            ref passed);
        Check(
            roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Count() == 1,
            "Exactly one EventSystem",
            report,
            ref passed);
        Check(
            CountComponentsByTypeName(roots, "Oculus.Interaction.PointableCanvasModule") == 1,
            "Exactly one Meta PointableCanvasModule",
            report,
            ref passed);
        Check(
            CountComponentsByTypeName(roots, "UnityEngine.EventSystems.OVRInputModule") == 1,
            "Existing Meta OVRInputModule preserved",
            report,
            ref passed);
        Check(
            roots.SelectMany(root => root.GetComponentsInChildren<GameManager>(true)).Count() == 0,
            "Menu scene has no gameplay GameManager",
            report,
            ref passed);
        Check(
            roots.SelectMany(root => root.GetComponentsInChildren<GolfHole>(true)).Count() == 0,
            "Menu scene has no GolfHole",
            report,
            ref passed);

        int missingScriptCount = roots.Sum(CountMissingScripts);
        Check(missingScriptCount == 0, "No missing scripts in menu hierarchy", report, ref passed);

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
        string[] expectedFirstScenes = { MenuPath, SourcePath, SecondCoursePath };
        Check(
            buildScenes.Length >= expectedFirstScenes.Length,
            "Build Settings contains menu and two courses",
            report,
            ref passed);

        for (int index = 0; index < expectedFirstScenes.Length; index++)
        {
            bool matches = buildScenes.Length > index
                && buildScenes[index].enabled
                && PathsMatch(buildScenes[index].path, expectedFirstScenes[index]);
            Check(
                matches,
                $"Build index {index}: {expectedFirstScenes[index]} (enabled)",
                report,
                ref passed);
        }

        Check(
            MiniGolfSceneCatalog.CourseCount == 2,
            "Scene catalog sees exactly two playable courses",
            report,
            ref passed);
        Check(
            MiniGolfSceneCatalog.GetBuildIndex(SourcePath) == 1,
            "First course resolves to build index 1",
            report,
            ref passed);
        Check(
            MiniGolfSceneCatalog.GetBuildIndex(SecondCoursePath) == 2,
            "Second course resolves to build index 2",
            report,
            ref passed);

        report.Add(string.Empty);
        report.Add(passed ? "OVERALL: PASS" : "OVERALL: FAIL");
        return report;
    }

    private static void CloneRootIntoScene(Scene sourceScene, Scene destinationScene, string rootName)
    {
        GameObject sourceRoot = FindRoot(sourceScene, rootName);
        if (sourceRoot == null)
        {
            throw new MissingReferenceException(
                $"Kaynak sahnede gerekli kök nesne bulunamadı: {rootName}");
        }

        GameObject clone = Object.Instantiate(sourceRoot);
        clone.name = sourceRoot.name;
        SceneManager.MoveGameObjectToScene(clone, destinationScene);
    }

    private static GameObject FindRoot(Scene scene, string rootName)
    {
        return scene.GetRootGameObjects()
            .FirstOrDefault(root => string.Equals(root.name, rootName, StringComparison.Ordinal));
    }

    private static int CountComponentsByTypeName(IEnumerable<GameObject> roots, string fullTypeName)
    {
        return roots
            .SelectMany(root => root.GetComponentsInChildren<Component>(true))
            .Count(component => component != null && component.GetType().FullName == fullTypeName);
    }

    private static int CountMissingScripts(GameObject root)
    {
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
        foreach (Transform child in root.transform)
        {
            count += CountMissingScripts(child.gameObject);
        }

        return count;
    }

    private static void Check(
        bool condition,
        string label,
        ICollection<string> report,
        ref bool passed)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {label}");
        if (!condition) passed = false;
    }

    private static void WriteReport(IEnumerable<string> report)
    {
        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? "Temp");
        File.WriteAllLines(absolutePath, report);
    }

    private static void RequireAsset(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
        {
            throw new FileNotFoundException("Gerekli sahne bulunamadı.", path);
        }
    }

    private static void RequireCleanLoadedScenes()
    {
        for (int sceneIndex = 0; sceneIndex < EditorSceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = EditorSceneManager.GetSceneAt(sceneIndex);
            if (scene.isDirty)
            {
                throw new InvalidOperationException(
                    $"Kaydedilmemiş sahne değişikliği var: {scene.path}. Önce sahneyi kaydet.");
            }
        }
    }

    private static bool PathsMatch(string left, string right)
    {
        return string.Equals(
            left?.Replace('\\', '/'),
            right?.Replace('\\', '/'),
            StringComparison.OrdinalIgnoreCase);
    }
}
