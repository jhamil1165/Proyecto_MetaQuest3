using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel del menú para manejar el plano de corte: activar y desactivar, volver a
/// centrarlo sobre los órganos y girarlo 90 grados (corte horizontal o vertical).
///
/// Hacía falta porque el plano, por sí solo, no se explica: el usuario tiene que poder
/// decidir cuándo corta. El mismo componente responde al botón B/Y del mando.
///
/// Va debajo del menú principal, en el mismo arco, y se enciende con las vistas
/// Modelo 3D y Segmentación, que son donde hay algo que cortar.
/// </summary>
public static class MedicalCutPanel
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string CanvasName = "Canvas_Corte";
    private const float U = 8f;
    private const float Scale = 0.0016f;

    private static readonly Color CardBg = new Color(1f, 1f, 1f, 1f);
    private static readonly Color Subtle = new Color(0.955f, 0.960f, 0.968f, 1f);
    private static readonly Color TextPrimary = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color TextMuted = new Color(0.55f, 0.58f, 0.62f, 1f);

    private static Material _rounded;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step71 - Panel del plano de corte")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        _rounded = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/Mat_UI_Rounded.mat");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        GameObject plane = GameObject.Find("Plano_de_corte");
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);

        if (rootGo == null || plane == null || actions == null || _rounded == null || _font == null)
        {
            Debug.LogError("[Step71] Falta Medical_Menu_UI, Plano_de_corte, el menu, el material o la fuente.");
            return;
        }

        var input = plane.GetComponent<ClippingPlaneInput>();
        if (input == null) input = plane.AddComponent<ClippingPlaneInput>();

        // ---- el canvas, debajo del menú ----
        Transform existing = rootGo.transform.Find(CanvasName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject(CanvasName, typeof(RectTransform));
        go.transform.SetParent(rootGo.transform, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Object.FindObjectOfType<Camera>();
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(660f, 190f);
        rt.anchoredPosition3D = new Vector3(0f, -0.46f, 1.8f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * Scale;

        GameObject card = NewUI("Card_Corte", go.transform);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(620f, 150f);
        Rounded(card, CardBg, 24f);

        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)(2 * U), (int)(2 * U), (int)(1.5f * U), (int)(1.5f * U));
        layout.spacing = U;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = Label(card.transform, "Plano de corte", 20f, TextMuted,
            TextAlignmentOptions.MidlineLeft, "Title", FontStyles.UpperCase);
        title.characterSpacing = 5f;
        Fixed(title.gameObject, 3 * U);

        GameObject row = NewUI("Botones", card.transform);
        Fixed(row, 7 * U);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = (int)U;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;

        Button toggle = PillButton(row.transform, "Activar corte", out Image toggleBg, out TMP_Text toggleText);
        Button center = PillButton(row.transform, "Centrar", out _, out _);
        Button rotate = PillButton(row.transform, "Girar 90°", out _, out _);

        // Los eventos se guardan en la escena: funcionan sin tocar nada en el Inspector.
        UnityEventTools.AddVoidPersistentListener(toggle.onClick, input.Toggle);
        UnityEventTools.AddVoidPersistentListener(center.onClick, input.Center);
        UnityEventTools.AddVoidPersistentListener(rotate.onClick, input.Rotate90);

        var iso = new SerializedObject(input);
        iso.FindProperty("stateLabel").objectReferenceValue = toggleText;
        iso.FindProperty("stateBackground").objectReferenceValue = toggleBg;
        iso.FindProperty("cutting").boolValue = false;
        iso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(input);

        sb.AppendLine("panel creado con: activar/quitar corte, centrar y girar 90 grados");
        sb.AppendLine("boton B/Y del mando: tambien activa y quita el corte");

        // ---- el menú lo enciende donde hay algo que cortar ----
        var aso = new SerializedObject(actions);
        foreach (string field in new[] { "model3DObjects", "segmentationObjects" })
        {
            var group = aso.FindProperty(field);
            bool already = false;
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue == go) already = true;
            }
            if (!already)
            {
                group.arraySize += 1;
                group.GetArrayElementAtIndex(group.arraySize - 1).objectReferenceValue = go;
            }
            sb.AppendLine(field + ": " + group.arraySize + " objetos");
        }
        aso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        MedicalCtViewerLayout.LoadResources();
        MedicalCtViewerLayout.RenderPreview(go, OutDir + "step71_panel.png", sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step71_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP71_DONE");
    }

    private static Button PillButton(Transform parent, string label, out Image background, out TMP_Text text)
    {
        GameObject go = NewUI("Btn_" + label, parent);
        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        background = Rounded(go, Subtle, 20f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.90f, 0.92f, 0.95f, 1f);
        colors.pressedColor = new Color(0.80f, 0.82f, 0.86f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        text = Label(go.transform, label, 20f, TextPrimary, TextAlignmentOptions.Center, "Label");
        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return button;
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Fixed(GameObject go, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = height;
        le.flexibleHeight = 0f;
    }

    private static Image Rounded(GameObject go, Color color, float radius)
    {
        var image = go.AddComponent<Image>();
        image.material = _rounded;
        image.color = color;
        go.AddComponent<UIRoundedRect>();

        var so = new SerializedObject(go.GetComponent<UIRoundedRect>());
        so.FindProperty("radius").floatValue = radius;
        so.ApplyModifiedPropertiesWithoutUndo();
        return image;
    }

    private static TMP_Text Label(Transform parent, string content, float size, Color color,
        TextAlignmentOptions align, string name, FontStyles style = FontStyles.Normal)
    {
        GameObject go = NewUI(name, parent);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.fontStyle = style;
        text.enableWordWrapping = false;
        return text;
    }
}
