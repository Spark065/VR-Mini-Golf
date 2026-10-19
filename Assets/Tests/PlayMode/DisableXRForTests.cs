#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine.TestTools;

// Testler VR başlığı olmadan (batchmode) çalışırken OpenXR/Meta XR Simulator
// başlatılmasın; aksi halde grafik cihazı olmadığı için Editor çöker.
// Ayar yalnızca bellekte değişir ve test sonunda eski haline döner.
public class DisableXRForTests : IPrebuildSetup, IPostBuildCleanup
{
    private const string Key = "MiniGolfTests.XRInitOnStart";

    public void Setup()
    {
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings == null) return;
        SessionState.SetBool(Key, settings.InitManagerOnStart);
        settings.InitManagerOnStart = false;
    }

    public void Cleanup()
    {
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings == null) return;
        settings.InitManagerOnStart = SessionState.GetBool(Key, true);
    }
}
#endif
