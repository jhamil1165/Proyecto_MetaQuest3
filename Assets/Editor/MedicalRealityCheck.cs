using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dice por que no se ve la habitacion de verdad detras de los paneles, y en que estado esta
/// el panel que enciende y apaga los organos. Solo mira, no cambia nada.
/// </summary>
public static class MedicalRealityCheck
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step79 - Por que no se ve la realidad")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        sb.AppendLine("=== PASSTHROUGH ===");

        var manager = Object.FindObjectOfType<OVRManager>(true);
        sb.AppendLine(manager == null
            ? "OVRManager: NO HAY"
            : "OVRManager: passthrough=" + manager.isInsightPassthroughEnabled +
              " | objeto activo=" + manager.gameObject.activeInHierarchy);

        var layer = Object.FindObjectOfType<OVRPassthroughLayer>(true);
        sb.AppendLine(layer == null
            ? "OVRPassthroughLayer: NO HAY  <-- sin esto no hay realidad de fondo"
            : "OVRPassthroughLayer: tipo=" + layer.overlayType +
              " | componente activo=" + layer.enabled +
              " | objeto activo=" + layer.gameObject.activeInHierarchy);

        foreach (var cam in Object.FindObjectsOfType<Camera>(true))
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            sb.AppendLine(string.Format(
                "camara '{0}': borrado={1} fondo=({2:F2},{3:F2},{4:F2}, alfa {5:F2}) " +
                "| post={6} | activa={7}",
                cam.name, cam.clearFlags,
                cam.backgroundColor.r, cam.backgroundColor.g, cam.backgroundColor.b, cam.backgroundColor.a,
                data != null ? data.renderPostProcessing.ToString() : "?",
                cam.gameObject.activeInHierarchy));
        }

        sb.AppendLine("skybox de la escena: " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "ninguno"));

        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        sb.AppendLine(pipeline == null
            ? "pipeline: no es URP"
            : "pipeline URP: " + pipeline.name + " | HDR=" + pipeline.supportsHDR);

        // Cualquier cosa muy grande alrededor tapa la realidad.
        foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            Vector3 size = renderer.bounds.size;
            if (size.x > 4f && size.y > 4f && size.z > 4f && renderer.gameObject.activeInHierarchy)
            {
                sb.AppendLine("[OJO] objeto enorme que podria tapar la realidad: " +
                              renderer.name + " (" + size.ToString("F1") + " m)");
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== PANEL DE ORGANOS ===");

        var panel = Object.FindObjectOfType<OrganSystemsPanel>(true);
        if (panel == null)
        {
            sb.AppendLine("OrganSystemsPanel: NO HAY en la escena");
        }
        else
        {
            var so = new SerializedObject(panel);
            var rows = so.FindProperty("rows");
            sb.AppendLine("OrganSystemsPanel: en '" + Path(panel.gameObject) +
                          "' | activo=" + panel.gameObject.activeInHierarchy +
                          " | filas=" + rows.arraySize);

            for (int i = 0; i < rows.arraySize; i++)
            {
                var row = rows.GetArrayElementAtIndex(i);
                var target = row.FindPropertyRelative("target").objectReferenceValue;
                var toggle = row.FindPropertyRelative("toggle").objectReferenceValue;
                sb.AppendLine("   fila '" + row.FindPropertyRelative("label").stringValue +
                              "': organo=" + (target != null ? target.name : "SIN ASIGNAR") +
                              " | interruptor=" + (toggle != null ? "si" : "SIN ASIGNAR"));
            }
        }

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var aso = new SerializedObject(actions);
            foreach (string field in new[] { "dicomObjects", "segmentationObjects", "model3DObjects" })
            {
                var group = aso.FindProperty(field);
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < group.arraySize; i++)
                {
                    var go = group.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    names.Append(go != null ? go.name : "(vacio)").Append("  ");
                }
                sb.AppendLine("vista " + field + ": " + names);
            }
        }

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step79_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP79_DONE");
    }

    private static string Path(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }
}
