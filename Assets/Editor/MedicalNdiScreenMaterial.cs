using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hace que la pantalla NDI pueda apagarse de verdad cuando no hay señal.
///
/// Venía con un material de shader Unlit/Texture, que sólo sabe pintar una textura y no
/// admite tinte. NdiScreenStatus lleva desde siempre mandándole un color oscuro para avisar
/// de que no llega nada, y el shader lo ignoraba: por eso la pantalla se veía blanca pasara
/// lo que pasara, y el aviso de "sin señal" nunca se vio.
///
/// Se le pone un material propio con Universal Render Pipeline/Unlit, que sí admite tinte, y
/// se le dice al receptor de NDI que escriba el vídeo en _BaseMap, que es como se llama la
/// textura en ese shader. Con vídeo el tinte es blanco y no altera la imagen; sin vídeo
/// queda oscura, que es lo que se buscaba.
/// </summary>
public static class MedicalNdiScreenMaterial
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string MatDir = "Assets/Materials/NDI";

    [MenuItem("MedicalViewer/Step84 - Que la pantalla NDI pueda apagarse")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
        {
            Debug.LogError("[Step84] No encuentro el shader URP/Unlit.");
            return;
        }

        var idle = (Color)new Color32(0x12, 0x16, 0x1C, 255);
        int done = 0;

        foreach (var status in Object.FindObjectsOfType<NdiScreenStatus>(true))
        {
            var so = new SerializedObject(status);
            var renderer = so.FindProperty("screenRenderer").objectReferenceValue as Renderer;
            if (renderer == null)
            {
                sb.AppendLine("[AVISO] " + status.name + " no tiene pantalla asignada");
                continue;
            }

            string name = status.gameObject.name;

            // Un material por pantalla: compartirlo haria que apagar una apagase la otra.
            System.IO.Directory.CreateDirectory(MatDir);
            string path = MatDir + "/Mat_" + Sanitise(name) + ".mat";

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(unlit);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = unlit;
            material.SetColor("_BaseColor", idle);
            material.SetColor("_Color", idle);
            EditorUtility.SetDirty(material);

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);

            // El receptor tiene que escribir el video donde este shader lo lee.
            foreach (var behaviour in status.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null || behaviour.GetType().Name != "NdiReceiver") continue;

                var rso = new SerializedObject(behaviour);
                var property = rso.FindProperty("_targetMaterialProperty");
                if (property == null) continue;

                string before = property.stringValue;
                property.stringValue = "_BaseMap";
                rso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(behaviour);

                sb.AppendLine("   receptor: el video pasa de '" +
                              (string.IsNullOrEmpty(before) ? "(vacio)" : before) + "' a '_BaseMap'");
            }

            sb.AppendLine(name + ": material propio " + path + " (URP/Unlit, admite tinte)");
            done++;
        }

        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("pantallas arregladas: " + done);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step84_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP84_DONE");
    }

    private static string Sanitise(string name)
    {
        var sb = new StringBuilder();
        foreach (char c in name) sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        return sb.ToString();
    }
}
