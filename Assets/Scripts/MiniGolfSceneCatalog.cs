using System;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps menu scenes out of mini-golf hole numbering while still allowing the
/// build to start from a dedicated main menu.
/// </summary>
public static class MiniGolfSceneCatalog
{
    public const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    public const string FirstCourseScenePath = "Assets/Scenes/SampleScene.unity";
    public const string SecondCourseScenePath = "Assets/Scenes/Scene 2.unity";

    public static int CourseCount
    {
        get
        {
            int count = 0;
            for (int buildIndex = 0; buildIndex < SceneManager.sceneCountInBuildSettings; buildIndex++)
            {
                if (IsCourseBuildIndex(buildIndex)) count++;
            }

            return count;
        }
    }

    public static int GetBuildIndex(string scenePath)
    {
        if (string.IsNullOrWhiteSpace(scenePath)) return -1;

        for (int buildIndex = 0; buildIndex < SceneManager.sceneCountInBuildSettings; buildIndex++)
        {
            if (PathsMatch(SceneUtility.GetScenePathByBuildIndex(buildIndex), scenePath))
            {
                return buildIndex;
            }
        }

        return -1;
    }

    public static int GetFirstCourseBuildIndex()
    {
        for (int buildIndex = 0; buildIndex < SceneManager.sceneCountInBuildSettings; buildIndex++)
        {
            if (IsCourseBuildIndex(buildIndex)) return buildIndex;
        }

        return -1;
    }

    public static int GetCourseIndex(Scene scene)
    {
        if (scene.buildIndex < 0 || !IsCourseBuildIndex(scene.buildIndex)) return -1;

        int courseIndex = 0;
        for (int buildIndex = 0; buildIndex < scene.buildIndex; buildIndex++)
        {
            if (IsCourseBuildIndex(buildIndex)) courseIndex++;
        }

        return courseIndex;
    }

    public static bool TryGetNextCourseBuildIndex(Scene currentScene, out int nextBuildIndex)
    {
        int startIndex = MathfMaxZero(currentScene.buildIndex + 1);
        for (int buildIndex = startIndex;
             buildIndex < SceneManager.sceneCountInBuildSettings;
             buildIndex++)
        {
            if (!IsCourseBuildIndex(buildIndex)) continue;

            nextBuildIndex = buildIndex;
            return true;
        }

        nextBuildIndex = -1;
        return false;
    }

    public static bool IsFinalCourse(Scene scene)
    {
        return GetCourseIndex(scene) >= 0
            && !TryGetNextCourseBuildIndex(scene, out _);
    }

    public static bool IsCourseBuildIndex(int buildIndex)
    {
        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            return false;
        }

        string scenePath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return !string.IsNullOrWhiteSpace(scenePath)
            && !PathsMatch(scenePath, MainMenuScenePath);
    }

    private static bool PathsMatch(string left, string right)
    {
        return string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }

    private static int MathfMaxZero(int value)
    {
        return value < 0 ? 0 : value;
    }
}
