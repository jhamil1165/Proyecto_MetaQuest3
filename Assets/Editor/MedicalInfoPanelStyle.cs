using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Oscurece los carteles que se quedaron fuera del repintado: los que dicen el nombre del
/// órgano al señalarlo ("VESÍCULA", "HÍGADO"...).
///
/// No entraron en la pasada anterior porque no son interfaz normal: el fondo es un cuadrado
/// con material propio y el texto es TextMeshPro 3D, no de lienzo. Se quedaron en blanco con
/// letra oscura, y en el visor un cartel blanco sobre una escena oscura deslumbra: es
/// exactamente lo que Meta desaconseja al pedir que no se use blanco puro.
/// </summary>
public static class MedicalInfoPanelStyle
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string MatDir = "Assets/Materials/UI";

    private static readonly Color Surface = new Color32(0x1E, 0x22, 0x28, 235);
    private static readonly Color TextPrimary = new Color32(0xF0, 0xF2, 0xF5, 255);
    private static readonly Color TextSecondary = new Color32(0xA8, 0xB1, 0xBC, 255);

    [MenuItem("MedicalViewer/Step86 - Oscurecer los carteles de los organos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Material panel = Panel(sb);
        int backgrounds = 0, texts = 0;

        foreach (var text in Object.FindObjectsOfType<TextMeshPro>(true))
        {
            // El título va en claro y el cuerpo algo más apagado, para que se distingan.
            bool title = text.name.ToLowerInvariant().Contains("title");
            text.color = title ? TextPrimary : TextSecondary;
            EditorUtility.SetDirty(text);
            texts++;
            sb.AppendLine("texto 3D aclarado: " + Path(text.gameObject) + (title ? "  (titulo)" : ""));
        }

        // El fondo del cartel: cualquier cuadrado llamado Panel_BG.
        foreach (var renderer in Object.FindObjectsOfType<MeshRenderer>(true))
        {
            if (!renderer.name.Contains("Panel_BG")) continue;

            renderer.sharedMaterial = panel;
            EditorUtility.SetDirty(renderer);
            backgrounds++;
            sb.AppendLine("fondo del cartel a oscuro: " + Path(renderer.gameObject));
        }

        sb.AppendLine("carteles: " + backgrounds + " fondos, " + texts + " textos");

        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step86_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP86_DONE");
    }

    /// <summary>Material propio del cartel, para no tocar el compartido de nadie.</summary>
    private static Material Panel(StringBuilder sb)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        string path = MatDir + "/Mat_Cartel_Organo.mat";

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            sb.AppendLine("material del cartel creado en " + path);
        }

        material.SetColor("_BaseColor", Surface);
        material.SetColor("_Color", Surface);

        // Con transparencia, para que se intuya lo que hay detrás como en el resto de paneles.
        material.SetFloat("_Surface", 1f);          // 1 = transparente en URP
        material.SetFloat("_Blend", 0f);            // mezcla normal
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(material);
        return material;
    }

    private static string Path(GameObject go)
    {
        string path = go.name;
        for (Transform t = go.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
        return path;
    }
}
