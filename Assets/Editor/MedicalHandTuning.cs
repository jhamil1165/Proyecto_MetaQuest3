using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Ajuste de la interacción con las manos tras la segunda prueba en el visor:
///
///  - La pinza pedía fuerza 0,7: había que juntar índice y pulgar casi del todo y repetir el
///    gesto varias veces. Se baja a 0,45 para agarrar y 0,25 para soltar.
///  - El círculo de apuntar, un poco más grande (3 cm -> 4,5 cm visto a un metro), siempre
///    visible y con movimiento suave: lo coloca RayReticle en vez de la retícula de XRI, que
///    solo aparecía al tocar algo.
///  - De cerca se agarra con la mano y el rayo se aparta (HandNearFar).
///
/// La subida del rayo, el suavizado y la pausa al pinzar van en OVRHandXRController con sus
/// valores por defecto.
/// </summary>
public static class MedicalHandTuning
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ReticlePrefab = "Assets/Prefabs/Reticula_Rayo.prefab";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step103 - Ajustar pinza y circulo de las manos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        foreach (var hand in Object.FindObjectsOfType<OVRHandXRController>(true))
        {
            var so = new SerializedObject(hand);
            so.FindProperty("pinchThreshold").floatValue = 0.45f;
            so.FindProperty("releaseThreshold").floatValue = 0.25f;
            // Rayo más suave: la mano tiembla y el círculo se veía nervioso.
            so.FindProperty("smoothing").floatValue = 9f;
            // 15 grados hacían que el círculo cayera por encima del botón al que se apuntaba.
            so.FindProperty("pitchOffset").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine(string.Format("{0}: pinza 0.45 / suelta 0.25, rayo levantado {1} grados",
                hand.BoundHandName, so.FindProperty("pitchOffset").floatValue));
        }

        var prefab = PrefabUtility.LoadPrefabContents(ReticlePrefab);
        var scaler = new SerializedObject(prefab.GetComponent<ReticleScaler>());
        scaler.FindProperty("sizeAtOneMetre").floatValue = 0.045f;
        scaler.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(prefab, ReticlePrefab);
        PrefabUtility.UnloadPrefabContents(prefab);
        sb.AppendLine("circulo: 4.5 cm a un metro");

        var reticle = AssetDatabase.LoadAssetAtPath<GameObject>(ReticlePrefab);
        foreach (var visual in Object.FindObjectsOfType<XRInteractorLineVisual>(true))
        {
            if (Branch(visual.transform).ToUpperInvariant().Contains("ORPHAN")) continue;

            visual.reticle = null;
            EditorUtility.SetDirty(visual);

            var follower = visual.GetComponent<RayReticle>();
            if (follower == null) follower = visual.gameObject.AddComponent<RayReticle>();
            var so = new SerializedObject(follower);
            so.FindProperty("reticlePrefab").objectReferenceValue = reticle;
            so.FindProperty("followSpeed").floatValue = 8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine("circulo siempre visible y suave: " + Branch(visual.transform));
        }

        // De cerca agarra la mano; de lejos, el rayo.
        foreach (var hand in Object.FindObjectsOfType<OVRHandXRController>(true))
        {
            var ray = hand.GetComponent<XRRayInteractor>();
            var direct = hand.GetComponentInChildren<XRDirectInteractor>(true);
            if (ray == null || direct == null) continue;

            var nearFar = hand.GetComponent<HandNearFar>();
            if (nearFar == null) nearFar = hand.gameObject.AddComponent<HandNearFar>();
            var so = new SerializedObject(nearFar);
            so.FindProperty("ray").objectReferenceValue = ray;
            so.FindProperty("direct").objectReferenceValue = direct;
            so.ApplyModifiedPropertiesWithoutUndo();
            sb.AppendLine(hand.BoundHandName + ": agarre directo de cerca, rayo de lejos");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step103_output.txt", sb.ToString());
        Debug.Log("STEP103_DONE");
    }

    private static string Branch(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
