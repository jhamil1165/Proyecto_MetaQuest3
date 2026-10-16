using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Pone la regla 2D en el visor DICOM: los botones "Medir" y "Borrar" en su cabecera, junto
/// a los de cambiar de estudio, y el componente CTMeasure2D enganchado a sus tres planos y a
/// los rayos de los mandos.
///
/// Solo en el visor DICOM, que es el de la imagen médica original: es donde el profesor pidió
/// medir. Los de segmentación llevan en la cabecera las pastillas de órganos y no caben más
/// botones sin apretujarlo todo.
/// </summary>
public static class MedicalMeasure2DSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string ViewerCanvas = "Canvas_DICOM";

    // Vidrio oscuro, como el resto de la interfaz.
    private static readonly Color PillFull = new Color(1f, 1f, 1f, 0.22f);
    private static readonly Color TextPrimary = new Color32(0xF0, 0xF2, 0xF5, 255);

    [MenuItem("MedicalViewer/Step98 - Regla 2D en el visor DICOM")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        CTMultiPlaneViewer viewer = null;
        foreach (var v in Object.FindObjectsOfType<CTMultiPlaneViewer>(true))
        {
            if (Branch(v.transform).Contains(ViewerCanvas)) { viewer = v; break; }
        }

        if (viewer == null)
        {
            Debug.LogError("[Step98] No encuentro el visor de " + ViewerCanvas);
            return;
        }
        sb.AppendLine("visor: " + Branch(viewer.transform));

        // ---- la cabecera y sus botones ----
        Transform canvas = viewer.transform;
        while (canvas.parent != null && canvas.name != ViewerCanvas) canvas = canvas.parent;

        Transform header = FindDeep(canvas, "Header");
        Transform previous = header != null ? header.Find("Btn_Previous") : null;
        if (header == null || previous == null)
        {
            Debug.LogError("[Step98] No encuentro la cabecera o el boton '<' del visor.");
            return;
        }

        // Si ya estaban de una pasada anterior, se rehacen.
        foreach (string old in new[] { "Btn_Medir2D", "Btn_Borrar2D" })
        {
            Transform t = header.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        // Del mismo alto que los botones de cambiar de estudio, que ya se agrandaron para
        // que se pudieran pulsar bien con el mando.
        var prevLayout = previous.GetComponent<LayoutElement>();
        float height = prevLayout != null && prevLayout.preferredHeight > 0f ? prevLayout.preferredHeight : 48f;

        Button measure = Pill(header, "Btn_Medir2D", "Medir", height, height * 2.3f);
        Button clear = Pill(header, "Btn_Borrar2D", "Borrar", height, height * 2.0f);

        // Delante de "<" y ">": primero se mide, luego se cambia de estudio.
        int index = previous.GetSiblingIndex();
        measure.transform.SetSiblingIndex(index);
        clear.transform.SetSiblingIndex(index + 1);
        sb.AppendLine(string.Format("botones Medir y Borrar en la cabecera, {0:F0} px de alto", height));

        // ---- el componente ----
        var tool = viewer.GetComponent<CTMeasure2D>();
        if (tool == null) tool = viewer.gameObject.AddComponent<CTMeasure2D>();

        var vso = new SerializedObject(viewer);
        var planesProp = vso.FindProperty("planes");

        var so = new SerializedObject(tool);
        var toolPlanes = so.FindProperty("planes");
        toolPlanes.arraySize = planesProp.arraySize;
        for (int i = 0; i < planesProp.arraySize; i++)
        {
            toolPlanes.GetArrayElementAtIndex(i).objectReferenceValue =
                planesProp.GetArrayElementAtIndex(i).objectReferenceValue;
        }
        sb.AppendLine("planos: " + toolPlanes.arraySize);

        var rays = new List<XRRayInteractor>();
        foreach (var ray in Object.FindObjectsOfType<XRRayInteractor>(true))
        {
            if (Branch(ray.transform).ToUpperInvariant().Contains("ORPHAN")) continue;   // rama muerta del rig
            rays.Add(ray);
        }
        var rayList = so.FindProperty("rays");
        rayList.arraySize = rays.Count;
        for (int i = 0; i < rays.Count; i++) rayList.GetArrayElementAtIndex(i).objectReferenceValue = rays[i];
        sb.AppendLine("rayos: " + rays.Count);

        so.FindProperty("toggleButton").objectReferenceValue = measure;
        so.FindProperty("measuring").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tool);

        UnityEventTools.AddVoidPersistentListener(measure.onClick, tool.Toggle);
        UnityEventTools.AddVoidPersistentListener(clear.onClick, tool.Clear);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step98_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP98_DONE");
    }

    /// <summary>Pastilla de vidrio oscuro con texto, igual que las del panel de controles.</summary>
    private static Button Pill(Transform parent, string name, string text, float height, float width)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = layout.minHeight = height;
        layout.preferredWidth = layout.minWidth = width;
        layout.flexibleWidth = 0f;

        var image = go.AddComponent<Image>();
        image.color = PillFull;

        var rounded = go.AddComponent<UIRoundedRect>();
        rounded.Radius = height * 0.3f;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.45f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colors.pressedColor = new Color(0.42f, 0.63f, 1f, 1f);
        colors.selectedColor = new Color(1f, 1f, 1f, 0.45f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, false);
        var rt = labelGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 4f);
        rt.offsetMax = new Vector2(-8f, -4f);

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = TextPrimary;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        // Se encoge si el estado es largo ("Fuera de imagen"), en vez de salirse.
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 12f;
        tmp.fontSizeMax = 24f;
        tmp.raycastTarget = false;

        EditorUtility.SetDirty(go);
        return button;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private static string Branch(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
