using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reconstruye el panel de tomografía como visor de TRES PLANOS simultáneos:
/// axial, coronal y sagital en paralelo, cada uno con su deslizador.
///
/// Sustituye al visor de plano único con botones. Es el diseño que propuso
/// leamsi1je y es el correcto: en radiología se revisan los tres planos a la vez,
/// no uno cada vez.
/// </summary>
public static class MedicalMultiPlaneBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const float U = 8f;

    private static readonly Color CardBg      = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TextPrimary = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color TextMuted   = new Color(0.55f, 0.58f, 0.62f, 1f);
    private static readonly Color SubtleBg    = new Color(0.955f, 0.960f, 0.968f, 1f);
    private static readonly Color Accent      = new Color(0.09f, 0.42f, 0.88f, 1f);
    private static readonly Color DividerCol  = new Color(0.91f, 0.92f, 0.93f, 1f);
    private static readonly Color FilmBg      = new Color(0.07f, 0.08f, 0.10f, 1f);

    // Cortes por plano en el volumen de la TC.
    private static readonly (string plane, int count)[] Planes =
    {
        ("axial", 267),
        ("coronal", 512),
        ("sagital", 512),
    };

    private static Material _matRounded;
    private static Sprite _knob;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step47 - Three Plane CT Viewer")]
    public static void Build()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        _matRounded = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/Mat_UI_Rounded.mat");
        _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (_matRounded == null || _font == null)
        {
            Debug.LogError("STEP47_FAILED faltan material o fuente");
            return;
        }

        GameObject canvasDicom = GameObject.Find("Canvas_DICOM");
        if (canvasDicom == null)
        {
            // Puede estar desactivado por el estado inicial SoloMenu.
            GameObject root = GameObject.Find("Medical_Menu_UI");
            Transform found = root != null ? root.transform.Find("Canvas_DICOM") : null;
            if (found != null) canvasDicom = found.gameObject;
        }

        if (canvasDicom == null)
        {
            Debug.LogError("STEP47_FAILED Canvas_DICOM no encontrado");
            return;
        }

        bool wasActive = canvasDicom.activeSelf;
        canvasDicom.SetActive(true);

        // El panel de tres columnas necesita mas ancho que el de plano unico.
        var canvasRt = canvasDicom.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(1100f, 760f);

        foreach (Transform child in canvasDicom.transform)
        {
            Object.DestroyImmediate(child.gameObject);
        }

        GameObject card = Card("Card_DICOM", canvasDicom.transform, new Vector2(1060f, 700f));

        var viewer = card.AddComponent<CTMultiPlaneViewer>();

        Header(card.transform, "Tomografía", out TMP_Text organLabel, sb);

        GameObject row = NewUI("Planes", card.transform);
        row.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = (int)(2 * U);
        h.childAlignment = TextAnchor.UpperCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;

        var planeViews = new List<Object>();
        foreach (var (plane, count) in Planes)
        {
            planeViews.Add(BuildPlane(row.transform, plane, count, sb));
        }

        Line(card.transform);
        Footer(card.transform, out Button prev, out Button next);

        var so = new SerializedObject(viewer);
        var planesProp = so.FindProperty("planes");
        planesProp.arraySize = planeViews.Count;
        for (int i = 0; i < planeViews.Count; i++)
        {
            planesProp.GetArrayElementAtIndex(i).objectReferenceValue = planeViews[i];
        }
        so.FindProperty("organLabel").objectReferenceValue = organLabel;
        so.FindProperty("previousButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();

        canvasDicom.SetActive(wasActive);

        EditorUtility.SetDirty(canvasDicom);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step47_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP47_DONE");
    }

    private static CTPlaneView BuildPlane(Transform parent, string plane, int count, StringBuilder sb)
    {
        GameObject column = NewUI($"Plane_{plane}", parent);
        var v = column.AddComponent<VerticalLayoutGroup>();
        v.spacing = (int)U;
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandHeight = false;

        TMP_Text title = Label(column.transform, Capitalize(plane), 24f, TextPrimary,
            TextAlignmentOptions.Center, "Title", FontStyles.Bold);
        title.gameObject.AddComponent<LayoutElement>().minHeight = 4 * U;

        // Lienzo oscuro: una radiografia se lee mucho mejor sobre fondo negro.
        GameObject film = NewUI("Film", column.transform);
        var fl = film.AddComponent<LayoutElement>();
        fl.minHeight = 42 * U;
        fl.flexibleHeight = 1f;
        Rounded(film, FilmBg, 12f);

        GameObject imageGo = NewUI("Slice", film.transform);
        var raw = imageGo.AddComponent<RawImage>();
        raw.color = Color.white;
        var rawRt = raw.rectTransform;
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        rawRt.offsetMin = new Vector2(U, U);
        rawRt.offsetMax = new Vector2(-U, -U);

        TMP_Text counter = Label(column.transform, "0 / 0", 18f, TextMuted,
            TextAlignmentOptions.Center, "Counter");
        counter.gameObject.AddComponent<LayoutElement>().minHeight = 3 * U;

        GameObject sliderGo = NewUI("Slider", column.transform);
        sliderGo.AddComponent<LayoutElement>().minHeight = 5 * U;
        Slider slider = BuildSlider(sliderGo);

        var view = column.AddComponent<CTPlaneView>();
        var so = new SerializedObject(view);
        so.FindProperty("plane").stringValue = plane;
        so.FindProperty("sliceCount").intValue = count;
        so.FindProperty("display").objectReferenceValue = raw;
        so.FindProperty("slider").objectReferenceValue = slider;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("counter").objectReferenceValue = counter;
        so.ApplyModifiedPropertiesWithoutUndo();

        sb.AppendLine($"columna {plane}: {count} cortes");
        return view;
    }

    // ---------------- helpers ----------------

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image Rounded(GameObject go, Color color, float radius)
    {
        var img = go.AddComponent<Image>();
        img.material = _matRounded;
        img.color = color;
        go.AddComponent<UIRoundedRect>();

        var so = new SerializedObject(go.GetComponent<UIRoundedRect>());
        so.FindProperty("radius").floatValue = radius;
        so.ApplyModifiedPropertiesWithoutUndo();
        return img;
    }

    private static GameObject Card(string name, Transform parent, Vector2 size)
    {
        GameObject go = NewUI(name, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Rounded(go, CardBg, 28f);

        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset((int)(3 * U), (int)(3 * U), (int)(3 * U), (int)(2.5f * U));
        v.spacing = (int)U;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        return go;
    }

    private static TMP_Text Label(Transform parent, string content, float size, Color color,
        TextAlignmentOptions align, string name, FontStyles style = FontStyles.Normal)
    {
        GameObject go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.enableWordWrapping = false;
        return t;
    }

    private static void Header(Transform parent, string title, out TMP_Text organLabel, StringBuilder sb)
    {
        GameObject row = NewUI("Header", parent);
        row.AddComponent<LayoutElement>().minHeight = 6 * U;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;

        Label(row.transform, title, 34f, TextPrimary, TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        organLabel = Label(row.transform, "Hígado", 26f, Accent, TextAlignmentOptions.MidlineRight, "Organ");
        organLabel.gameObject.AddComponent<LayoutElement>().minWidth = 22 * U;

        sb.AppendLine("cabecera: " + title);
    }

    private static void Line(Transform parent)
    {
        GameObject go = NewUI("Divider", parent);
        go.AddComponent<LayoutElement>().minHeight = 1.5f;
        go.AddComponent<Image>().color = DividerCol;
    }

    private static void Footer(Transform parent, out Button previous, out Button next)
    {
        GameObject row = NewUI("Footer", parent);
        row.AddComponent<LayoutElement>().minHeight = 6 * U;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = (int)U;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;

        previous = TextButton(row.transform, "Estudio anterior");
        next = TextButton(row.transform, "Estudio siguiente");
    }

    private static Button TextButton(Transform parent, string label)
    {
        GameObject go = NewUI("Btn_" + label, parent);
        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Image bg = Rounded(go, SubtleBg, 16f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        var c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.88f, 0.90f, 0.93f);
        c.fadeDuration = 0.08f;
        btn.colors = c;

        TMP_Text t = Label(go.transform, label, 20f, TextPrimary, TextAlignmentOptions.Center, "Label");
        var rt = t.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return btn;
    }

    private static Slider BuildSlider(GameObject go)
    {
        var slider = go.AddComponent<Slider>();

        GameObject bg = NewUI("Background", go.transform);
        var brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0.5f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.sizeDelta = new Vector2(-3f * U, 0.75f * U);
        brt.anchoredPosition = Vector2.zero;
        Rounded(bg, DividerCol, 0.4f * U);

        GameObject fillArea = NewUI("Fill Area", go.transform);
        var frt = fillArea.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0.5f);
        frt.anchorMax = new Vector2(1f, 0.5f);
        frt.sizeDelta = new Vector2(-3f * U, 0.75f * U);
        frt.anchoredPosition = Vector2.zero;

        GameObject fill = NewUI("Fill", fillArea.transform);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.sizeDelta = Vector2.zero;
        Rounded(fill, Accent, 0.4f * U);

        GameObject handleArea = NewUI("Handle Slide Area", go.transform);
        var hrt = handleArea.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.sizeDelta = new Vector2(-3f * U, 0f);
        hrt.anchoredPosition = Vector2.zero;

        GameObject handle = NewUI("Handle", handleArea.transform);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(3f * U, 3f * U);
        var handleImg = handle.AddComponent<Image>();
        handleImg.sprite = _knob;
        handleImg.color = Color.white;

        slider.fillRect = fillRt;
        slider.handleRect = handleRt;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
