using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Saca la paleta que hay puesta ahora mismo en la interfaz: que colores se repiten, en
/// cuantos sitios y con que nombres. Sirve para repintar sin ir a ciegas y sin tocar lo que
/// no se debe, como las imagenes del TAC o las miniaturas de los organos.
/// </summary>
public static class MedicalStyleDump
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step82 - Que colores usa la interfaz")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject root = GameObject.Find("Medical_Menu_UI");
        if (root == null)
        {
            Debug.LogError("[Step82] No encuentro Medical_Menu_UI.");
            return;
        }

        var images = new Dictionary<string, List<string>>();
        var texts = new Dictionary<string, List<string>>();
        var sizes = new Dictionary<float, int>();
        int rawImages = 0;

        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            string path = Path(graphic.gameObject, root.transform);

            if (graphic is RawImage)
            {
                rawImages++;
                continue;   // imagenes del TAC y miniaturas: no se tocan
            }

            if (graphic is TMP_Text text)
            {
                Add(texts, Key(text.color), path + "   [" + text.fontSize.ToString("F0") + "px]");
                sizes.TryGetValue(text.fontSize, out int n);
                sizes[text.fontSize] = n + 1;
                continue;
            }

            if (graphic is Image image)
            {
                string sprite = image.sprite != null ? "  sprite:" + image.sprite.name : "";
                Add(images, Key(image.color), path + sprite);
            }
        }

        sb.AppendLine("=== FONDOS Y BOTONES (Image) ===");
        Report(sb, images);

        sb.AppendLine();
        sb.AppendLine("=== TEXTOS (TMP) ===");
        Report(sb, texts);

        sb.AppendLine();
        sb.AppendLine("=== TAMAÑOS DE LETRA ===");
        foreach (var pair in sizes.OrderByDescending(p => p.Key))
        {
            sb.AppendLine(string.Format("{0,6:F0} px  en {1} sitios", pair.Key, pair.Value));
        }

        sb.AppendLine();
        sb.AppendLine("RawImage (TAC y miniaturas, no se tocan): " + rawImages);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step82_estilo.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP82_DONE");
    }

    private static void Report(StringBuilder sb, Dictionary<string, List<string>> map)
    {
        foreach (var pair in map.OrderByDescending(p => p.Value.Count))
        {
            sb.AppendLine(string.Format("{0}  x{1}", pair.Key, pair.Value.Count));
            foreach (string example in pair.Value.Take(3)) sb.AppendLine("      " + example);
            if (pair.Value.Count > 3) sb.AppendLine("      ...");
        }
    }

    private static void Add(Dictionary<string, List<string>> map, string key, string path)
    {
        if (!map.TryGetValue(key, out var list)) map[key] = list = new List<string>();
        list.Add(path);
    }

    /// <summary>Color en hexadecimal mas su transparencia, que es como se compara facil.</summary>
    private static string Key(Color c)
    {
        return string.Format("#{0:X2}{1:X2}{2:X2} alfa {3:F2}",
            Mathf.RoundToInt(c.r * 255f), Mathf.RoundToInt(c.g * 255f), Mathf.RoundToInt(c.b * 255f), c.a);
    }

    private static string Path(GameObject go, Transform stop)
    {
        string path = go.name;
        for (Transform t = go.transform.parent; t != null && t != stop; t = t.parent) path = t.name + "/" + path;
        return path;
    }
}
