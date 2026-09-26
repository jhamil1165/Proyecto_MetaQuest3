using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Pone la habitación de verdad de fondo en vez del gris.
///
/// Para que el Quest deje ver lo que hay alrededor tienen que cumplirse cuatro cosas a la
/// vez, y estaban fallando todas:
///
/// 1. OVRManager con el passthrough encendido.
/// 2. La capa OVRPassthroughLayer viva y dibujando por detrás de la escena (Underlay).
///    Estaba puesta como Underlay, pero en un objeto apagado, así que no hacía nada.
/// 3. Las cámaras borrando a transparente. Las de los ojos borraban con el cielo y la del
///    centro con un gris claro opaco: ese gris es justo el "fondo blanco" que se veía.
/// 4. El manifiesto de Android declarando que la aplicación usa passthrough.
///
/// En el editor esto no se nota: sin visor no hay cámara real que enseñar y el fondo sale
/// negro o transparente. Se ve en el Quest, o por Link con el passthrough permitido.
/// </summary>
public static class MedicalPassthrough
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
    private const string Feature = "com.oculus.feature.PASSTHROUGH";

    [MenuItem("MedicalViewer/Step81 - Ver la realidad de fondo (passthrough)")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        // ---- 1. el interruptor general ----
        var manager = Object.FindObjectOfType<OVRManager>(true);
        if (manager != null)
        {
            manager.isInsightPassthroughEnabled = true;
            EditorUtility.SetDirty(manager);
            sb.AppendLine("OVRManager: passthrough encendido");
        }
        else
        {
            sb.AppendLine("[FALLO] no hay OVRManager en la escena");
        }

        // ---- 2. la capa que trae la imagen de las camaras del visor ----
        var layer = Object.FindObjectOfType<OVRPassthroughLayer>(true);
        if (layer != null)
        {
            layer.overlayType = OVROverlay.OverlayType.Underlay;   // por detras de todo
            layer.enabled = true;

            if (!layer.gameObject.activeSelf)
            {
                layer.gameObject.SetActive(true);
                sb.AppendLine("la capa estaba en un objeto apagado; encendida: " + layer.gameObject.name);
            }

            // Un padre apagado la deja muerta igual.
            for (Transform t = layer.transform.parent; t != null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(true);
                sb.AppendLine("encendido el padre apagado: " + t.name);
            }

            EditorUtility.SetDirty(layer);
            sb.AppendLine("OVRPassthroughLayer: Underlay y activa");
        }
        else
        {
            sb.AppendLine("[FALLO] no hay OVRPassthroughLayer: sin ella no hay realidad de fondo");
        }

        // ---- 3. las camaras, borrando a transparente ----
        foreach (var cam in Object.FindObjectsOfType<Camera>(true))
        {
            if (cam.clearFlags == CameraClearFlags.SolidColor && cam.backgroundColor.a == 0f) continue;

            sb.AppendLine(string.Format("camara '{0}': {1} con alfa {2:F2} -> transparente",
                cam.name, cam.clearFlags, cam.backgroundColor.a));

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            EditorUtility.SetDirty(cam);
        }

        // Un cielo opaco taparia la realidad aunque las camaras borren bien.
        if (RenderSettings.skybox != null)
        {
            RenderSettings.skybox = null;
            sb.AppendLine("quitado el cielo de la escena");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        // ---- 4. el manifiesto ----
        PatchManifest(sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step81_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP81_DONE");
    }

    /// <summary>Declara el passthrough en el manifiesto, si no estaba ya.</summary>
    private static void PatchManifest(StringBuilder sb)
    {
        if (!System.IO.File.Exists(ManifestPath))
        {
            sb.AppendLine("[AVISO] no encuentro " + ManifestPath);
            return;
        }

        string xml = System.IO.File.ReadAllText(ManifestPath);
        if (xml.Contains(Feature))
        {
            sb.AppendLine("manifiesto: ya declaraba el passthrough");
            return;
        }

        // Se cuelga junto a las demas uses-feature, dentro de <manifest>.
        const string anchor = "  <uses-feature android:name=\"oculus.software.handtracking\"";
        string line = "  <uses-feature android:name=\"" + Feature + "\" android:required=\"true\" />\n";

        if (xml.Contains(anchor)) xml = xml.Replace(anchor, line + anchor);
        else xml = xml.Replace("</manifest>", line + "</manifest>");

        System.IO.File.WriteAllText(ManifestPath, xml);
        AssetDatabase.ImportAsset(ManifestPath);
        sb.AppendLine("manifiesto: declarado " + Feature);
    }
}
