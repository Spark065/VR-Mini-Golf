using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Oyun scriptleri Assembly-CSharp içinde olduğu için asmdef'ten doğrudan referans
// verilemez; tipler isimle bulunur.
[PrebuildSetup("DisableXRForTests")]
[PostBuildCleanup("DisableXRForTests")]
public class BallPhysicsTests
{
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        // Headless çalıştırmada OpenXR başlatma hataları testi düşürmesin.
        LogAssert.ignoreFailingMessages = true;
        Time.timeScale = 1f;
        root = new GameObject("TestRoot");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.Destroy(root);
        foreach (var manager in UnityEngine.Object.FindObjectsByType(
                     Type.GetType("GameManager, Assembly-CSharp"), FindObjectsSortMode.None))
        {
            UnityEngine.Object.Destroy(((Component)manager).gameObject);
        }
    }

    [UnityTest]
    public IEnumerator PuttedBall_ClimbsTwelveDegreeRamp()
    {
        // Tek parça 12° rampa; üst yüzeyi orijinden geçer. Top doğrudan rampa üzerinden
        // vurulur, böylece iki collider arasındaki kenar çarpışması ölçüme karışmaz.
        Quaternion tilt = Quaternion.Euler(-12f, 0f, 0f);
        CreateBox("Ramp", tilt * new Vector3(0f, -0.05f, 1f), new Vector3(2f, 0.1f, 3f), tilt);

        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.transform.SetParent(root.transform);
        ball.transform.position = tilt * new Vector3(0f, 0.0225f, 0f);
        ball.AddComponent<Rigidbody>();
        Component controller = ball.AddComponent(Type.GetType("BallController, Assembly-CSharp"));

        // Topun yüzeye oturması için kısa bekleme (uzun beklemede rampadan geri kayar).
        for (int i = 0; i < 2; i++) yield return new WaitForFixedUpdate();

        bool hit = (bool)controller.GetType().GetMethod("ApplyPutt")
            .Invoke(controller, new object[] { Vector3.forward, 2.5f });
        Assert.IsTrue(hit, "ApplyPutt vuruşu kabul etmedi.");

        float startHeight = ball.transform.position.y;
        float maxHeight = startHeight;
        for (float elapsed = 0f; elapsed < 3f; elapsed += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            maxHeight = Mathf.Max(maxHeight, ball.transform.position.y);
        }

        // Düzeltilmiş kodda ~0.32 m, eski (her karede dikey hızı sıfırlayan) kodda ~0.15 m ölçüldü.
        // Eşik ikisinin arasında: en az 0.22 m yükselmeli.
        float climbed = maxHeight - startHeight;
        Assert.Greater(climbed, 0.22f, $"Top rampaya çıkamadı (yükselme = {climbed:F3} m).");
    }

    [Test]
    public void FinalScore_LosesFivePointsPerStrokeAndNeverGoesNegative()
    {
        var method = Type.GetType("GameManager, Assembly-CSharp").GetMethod("CalculateFinalScore");
        Assert.AreEqual(85, method.Invoke(null, new object[] { 3, 100, 5 }));
        Assert.AreEqual(0, method.Invoke(null, new object[] { 40, 100, 5 }));
    }

    private void CreateBox(string name, Vector3 position, Vector3 size, Quaternion rotation)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(root.transform);
        box.transform.SetPositionAndRotation(position, rotation);
        box.transform.localScale = size;
    }
}
