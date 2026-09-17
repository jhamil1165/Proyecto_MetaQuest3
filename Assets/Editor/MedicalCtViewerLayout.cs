using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visor de TC en CUADRÍCULA OSCURA, al estilo de los equipos de navegación quirúrgica
/// (Brainlab): cuatro recuadros iguales con axial, coronal, sagital y un cuarto de
/// información del estudio, que reserva el hueco donde la vista Segmentación pondrá
/// el modelo 3D del órgano.
///
/// Paleta oscura a propósito: la tomografía es negra, y sobre un fondo claro el ojo
/// se adapta al blanco y pierde contraste en la imagen.
///
/// Las imágenes tienen alto fijo; dentro de cada recuadro un AspectRatioFitter
/// mantiene la proporción real del corte, así nada se deforma.
///
/// BuildViewer es reutilizable (la vista Segmentación monta con él sus visores) y
/// conserva la configuración del CTMultiPlaneViewer que reconstruye.
/// </summary>
public static class MedicalCtViewerLayout
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string PreviewDir = "Assets/UI/CTPreview";
    private const float U = 8f;

    // Cuadrícula 2 x 2 de recuadros de 476 px. Tarjeta: 24 + 476 + 16 + 476 + 24 de
    // ancho, y 24 + 56 cabecera + 16 + 968 cuadrícula + 24 de alto.
    public const float CardW = 1016f;
    public const float CardH = 1088f;
    private const float Quad = 476f;
    private const float QuadPad = 14f;
    private const float FilmH = 364f; // 476 - 28 relleno - 32 título - 36 deslizador - 16 espacios

    private static readonly Color CardBg      = new Color(0.055f, 0.063f, 0.078f, 1f);
    private static readonly Color QuadBg      = new Color(0.098f, 0.114f, 0.137f, 1f);
    private static readonly Color FilmBg      = new Color(0.012f, 0.016f, 0.020f, 1f);
    private static readonly Color TextPrimary = new Color(0.91f, 0.93f, 0.95f, 1f);
    private static readonly Color TextMuted   = new Color(0.54f, 0.58f, 0.64f, 1f);
    private static readonly Color Accent      = new Color(0.23f, 0.63f, 1.00f, 1f);
    private static readonly Color Track       = new Color(0.17f, 0.20f, 0.24f, 1f);
    private static readonly Color ButtonBg    = new Color(0.16f, 0.19f, 0.23f, 1f);

    // Espaciado real de cada serie, leído de las cabeceras DICOM (SliceThickness y PixelSpacing).
    private static readonly Dictionary<string, (float slice, float pixel)> Spacing =
        new Dictionary<string, (float slice, float pixel)>
        {
            { "serie_toraxabdomen", (2.5f, 0.762f) },
            { "serie_cuerpo", (3.27f, 0.977f) },
        };

    private static Material _matRounded;
    private static Sprite _knob;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step60 - Visor de TC en cuadricula oscura")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        if (!LoadResources())
        {
            Debug.LogError("[Step60] Faltan el material redondeado, el knob o la fuente.");
            return;
        }

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        Transform canvas = rootGo != null ? rootGo.transform.Find("Canvas_DICOM") : null;
        if (canvas == null)
        {
            Debug.LogError("[Step60] No encuentro Medical_Menu_UI/Canvas_DICOM.");
            return;
        }

        CTMultiPlaneViewer viewer = BuildViewer(canvas.gameObject, "Card_DICOM", sb);
        ApplyInitialState(viewer, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        // Después de guardar: la cámara temporal del render no debe quedar en la escena.
        RenderPreview(canvas.gameObject, OutDir + "step60_canvas_dicom.png", sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step60_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP60_DONE");
    }

    public static bool LoadResources()
    {
        _matRounded = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/Mat_UI_Rounded.mat");
        _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        return _matRounded != null && _knob != null && _font != null;
    }

    /// <summary>Rehace la interfaz del visor dentro de un canvas, conservando su configuración.</summary>
    public static CTMultiPlaneViewer BuildViewer(GameObject canvas, string cardName, StringBuilder sb)
    {
        var old = canvas.GetComponentInChildren<CTMultiPlaneViewer>(true);
        string config = old != null ? EditorJsonUtility.ToJson(old) : null;

        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);
        }

        var canvasRt = canvas.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(CardW + 40f, CardH + 60f);

        GameObject card = Card(cardName, canvas.transform, new Vector2(CardW, CardH));
        var viewer = card.AddComponent<CTMultiPlaneViewer>();

        // Series y órganos vuelven tal cual; las referencias a la interfaz vieja se
        // sobrescriben justo abajo con las nuevas.
        if (config != null) EditorJsonUtility.FromJsonOverwrite(config, viewer);

        Header(card.transform, out TMP_Text seriesLabel, out Button prev, out Button next);

        GameObject grid = NewUI("Grid", card.transform);
        Fixed(grid, -1f, Quad * 2f + 2f * U);
        var g = grid.AddComponent<GridLayoutGroup>();
        g.cellSize = new Vector2(Quad, Quad);
        g.spacing = new Vector2(2f * U, 2f * U);
        g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        g.constraintCount = 2;
        g.childAlignment = TextAnchor.UpperCenter;

        CTPlaneView axial = BuildPlaneQuad(grid.transform, "axial");
        CTPlaneView coronal = BuildPlaneQuad(grid.transform, "coronal");
        CTPlaneView sagital = BuildPlaneQuad(grid.transform, "sagital");
        TMP_Text info = BuildInfoQuad(grid.transform);

        var so = new SerializedObject(viewer);
        var list = so.FindProperty("planes");
        list.arraySize = 3;
        list.GetArrayElementAtIndex(0).objectReferenceValue = axial;
        list.GetArrayElementAtIndex(1).objectReferenceValue = coronal;
        list.GetArrayElementAtIndex(2).objectReferenceValue = sagital;
        so.FindProperty("organLabel").objectReferenceValue = seriesLabel;
        so.FindProperty("infoText").objectReferenceValue = info;
        so.FindProperty("previousButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(viewer);

        sb.AppendLine(canvas.name + "/" + cardName + ": cuadricula 2x2 de " + Quad + " px, tema oscuro, configuracion " +
                      (config != null ? "conservada" : "nueva"));
        return viewer;
    }

    /// <summary>Espaciado de las series, contadores, deslizadores, datos y vistas previas.</summary>
    public static void ApplyInitialState(CTMultiPlaneViewer viewer, StringBuilder sb)
    {
        var so = new SerializedObject(viewer);
        var studies = so.FindProperty("studies");
        var organs = so.FindProperty("organs");

        for (int i = 0; i < studies.arraySize; i++)
        {
            var s = studies.GetArrayElementAtIndex(i);
            string f = s.FindPropertyRelative("folder").stringValue;
            if (!Spacing.TryGetValue(f, out var mm)) continue;
            s.FindPropertyRelative("sliceMm").floatValue = mm.slice;
            s.FindPropertyRelative("pixelMm").floatValue = mm.pixel;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        so.Update();

        string folder;
        string label;
        string details;
        int axialCount, coronalCount, sagitalCount;

        if (studies.arraySize > 0)
        {
            var s = studies.GetArrayElementAtIndex(0);
            var study = new CTMultiPlaneViewer.Study
            {
                folder = s.FindPropertyRelative("folder").stringValue,
                label = s.FindPropertyRelative("label").stringValue,
                axial = s.FindPropertyRelative("axial").intValue,
                coronal = s.FindPropertyRelative("coronal").intValue,
                sagittal = s.FindPropertyRelative("sagittal").intValue,
                sliceMm = s.FindPropertyRelative("sliceMm").floatValue,
                pixelMm = s.FindPropertyRelative("pixelMm").floatValue,
            };
            folder = study.folder;
            label = study.label;
            details = CTMultiPlaneViewer.Describe(study);
            axialCount = study.axial;
            coronalCount = study.coronal;
            sagitalCount = study.sagittal;
        }
        else if (organs.arraySize > 0)
        {
            folder = organs.GetArrayElementAtIndex(0).stringValue;
            label = Capitalize(folder);
            details = "Capturas de 3D Slicer";
            axialCount = 267;
            coronalCount = 512;
            sagitalCount = 512;
        }
        else
        {
            sb.AppendLine("[AVISO] el visor no tiene series ni organos");
            return;
        }

        if (so.FindProperty("organLabel").objectReferenceValue is TMP_Text seriesLabel)
        {
            seriesLabel.text = label;
            EditorUtility.SetDirty(seriesLabel);
        }

        if (so.FindProperty("infoText").objectReferenceValue is TMP_Text infoText)
        {
            infoText.text = details;
            EditorUtility.SetDirty(infoText);
        }

        var planes = so.FindProperty("planes");
        for (int i = 0; i < planes.arraySize; i++)
        {
            if (!(planes.GetArrayElementAtIndex(i).objectReferenceValue is CTPlaneView view)) continue;

            var vso = new SerializedObject(view);
            string plane = vso.FindProperty("plane").stringValue;
            int count = plane == "axial" ? axialCount : plane == "coronal" ? coronalCount : sagitalCount;
            int mid = count / 2;

            vso.FindProperty("sliceCount").intValue = count;
            var raw = vso.FindProperty("display").objectReferenceValue as RawImage;
            var slider = vso.FindProperty("slider").objectReferenceValue as Slider;
            var counter = vso.FindProperty("counter").objectReferenceValue as TMP_Text;
            vso.ApplyModifiedPropertiesWithoutUndo();

            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.minValue = 0;
                slider.maxValue = count - 1;
                slider.SetValueWithoutNotify(mid);
                EditorUtility.SetDirty(slider);
            }

            if (counter != null)
            {
                counter.text = (mid + 1) + " / " + count;
                EditorUtility.SetDirty(counter);
            }

            Texture2D tex = PreviewTexture(folder, plane, mid);
            if (raw != null && tex != null)
            {
                raw.texture = tex;
                if (raw.TryGetComponent(out AspectRatioFitter fitter))
                    fitter.aspectRatio = (float)tex.width / tex.height;
                EditorUtility.SetDirty(raw);
            }

            sb.AppendLine("   " + plane + ": " + count + " cortes, vista previa " +
                          (tex != null ? tex.width + "x" + tex.height : "NO ENCONTRADA"));
        }

        sb.AppendLine("   serie inicial: " + label);
    }

    /// <summary>Copia el corte central a Assets para que se vea también fuera de Play.</summary>
    public static Texture2D PreviewTexture(string folder, string plane, int index)
    {
        string file = folder + "_" + plane + "_" + index.ToString("D4") + ".jpg";
        string src = Application.streamingAssetsPath + "/CT/" + folder + "/" + plane + "/" + file;
        if (!System.IO.File.Exists(src)) return null;

        System.IO.Directory.CreateDirectory(PreviewDir);
        string dst = PreviewDir + "/Preview_" + folder + "_" + plane + ".jpg";
        System.IO.File.Copy(src, dst, true);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        if (AssetImporter.GetAtPath(dst) is TextureImporter importer)
        {
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
    }

    /// <summary>
    /// Fotografía el canvas con una cámara ortográfica temporal, para revisar el diseño
    /// sin abrir Unity. Solo funciona con GPU (batchmode sin -nographics).
    /// </summary>
    public static void RenderPreview(GameObject canvas, string path, StringBuilder sb)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            sb.AppendLine("render: sin GPU (-nographics), no se genera imagen");
            return;
        }

        bool wasActive = canvas.activeSelf;
        canvas.SetActive(true);

        var rt = canvas.GetComponent<RectTransform>();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        Canvas.ForceUpdateCanvases();
        foreach (var t in canvas.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();

        float worldW = rt.rect.width * rt.lossyScale.x;
        float worldH = rt.rect.height * rt.lossyScale.y;

        var camGo = new GameObject("TmpPreviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = worldH * 0.52f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.89f, 0.90f, 0.92f, 1f);

        // Mirando en la misma dirección que el canvas, desde el lado del usuario, y con el
        // plano lejano justo detrás: así no sale lo que haya detrás (la pantalla NDI).
        const float distance = 0.5f;
        cam.transform.rotation = canvas.transform.rotation;
        cam.transform.position = canvas.transform.position - canvas.transform.forward * distance;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = distance + 0.02f;

        int w = 1400;
        int h = Mathf.RoundToInt(w * worldH / worldW);
        cam.aspect = worldW / worldH;

        var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = target;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(w, h, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);
        canvas.SetActive(wasActive);

        sb.AppendLine("render: " + path + " (" + w + "x" + h + ")");
    }

    // ---------------- construcción ----------------

    private static void Header(Transform parent, out TMP_Text seriesLabel, out Button prev, out Button next)
    {
        GameObject row = NewUI("Header", parent);
        Fixed(row, -1f, 7f * U);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = U;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        GameObject titles = NewUI("Titles", row.transform);
        titles.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var v = titles.AddComponent<VerticalLayoutGroup>();
        v.spacing = 2f;
        v.childAlignment = TextAnchor.MiddleLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        TMP_Text kicker = Label(titles.transform, "Tomografía", 16f, TextMuted,
            TextAlignmentOptions.MidlineLeft, "Kicker", FontStyles.UpperCase);
        kicker.characterSpacing = 6f;
        Fixed(kicker.gameObject, -1f, 20f);

        seriesLabel = Label(titles.transform, "", 30f, TextPrimary,
            TextAlignmentOptions.MidlineLeft, "Series", FontStyles.Bold);
        Fixed(seriesLabel.gameObject, -1f, 34f);

        prev = IconButton(row.transform, "<", "Btn_Previous");
        next = IconButton(row.transform, ">", "Btn_Next");
    }

    private static CTPlaneView BuildPlaneQuad(Transform parent, string plane)
    {
        GameObject quad = NewUI("Plane_" + plane, parent);
        Rounded(quad, QuadBg, 20f);

        var v = quad.AddComponent<VerticalLayoutGroup>();
        int p = (int)QuadPad;
        v.padding = new RectOffset(p, p, p, p);
        v.spacing = U;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        GameObject top = NewUI("Top", quad.transform);
        Fixed(top, -1f, 4f * U);
        var th = top.AddComponent<HorizontalLayoutGroup>();
        th.childAlignment = TextAnchor.MiddleLeft;
        th.childControlWidth = true;
        th.childControlHeight = true;
        th.childForceExpandWidth = false;
        th.childForceExpandHeight = false;

        TMP_Text title = Label(top.transform, Capitalize(plane), 22f, TextPrimary,
            TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold | FontStyles.UpperCase);
        title.characterSpacing = 4f;
        title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        TMP_Text counter = Label(top.transform, "", 18f, TextMuted,
            TextAlignmentOptions.MidlineRight, "Counter");
        counter.gameObject.AddComponent<LayoutElement>().minWidth = 15f * U;

        RawImage raw = Film(quad.transform, FilmH);

        GameObject sliderGo = NewUI("Slider", quad.transform);
        Fixed(sliderGo, -1f, 4.5f * U);
        Slider slider = BuildSlider(sliderGo);

        return AddView(quad, plane, raw, slider, title, counter);
    }

    private static TMP_Text BuildInfoQuad(Transform parent)
    {
        GameObject quad = NewUI("Info", parent);
        Rounded(quad, QuadBg, 20f);

        var v = quad.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset(24, 24, 20, 20);
        v.spacing = 12f;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        TMP_Text title = Label(quad.transform, "Estudio", 22f, TextPrimary,
            TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold | FontStyles.UpperCase);
        title.characterSpacing = 4f;
        Fixed(title.gameObject, -1f, 4f * U);

        TMP_Text details = Label(quad.transform, "", 24f, TextPrimary,
            TextAlignmentOptions.TopLeft, "Details");
        details.enableWordWrapping = true;
        details.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

        TMP_Text note = Label(quad.transform, "El modelo 3D de cada órgano aparece en la vista Segmentación.",
            17f, TextMuted, TextAlignmentOptions.BottomLeft, "Note");
        note.enableWordWrapping = true;
        Fixed(note.gameObject, -1f, 6f * U);

        // Hueco reservado para el modelo 3D. Fuera del layout, ocupando todo el recuadro.
        GameObject slot = NewUI("ModelSlot", quad.transform);
        slot.AddComponent<LayoutElement>().ignoreLayout = true;
        var srt = slot.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.offsetMin = Vector2.zero;
        srt.offsetMax = Vector2.zero;

        return details;
    }

    private static RawImage Film(Transform parent, float height)
    {
        GameObject film = NewUI("Film", parent);
        Fixed(film, -1f, height);
        Rounded(film, FilmBg, 12f);

        GameObject inset = NewUI("Inset", film.transform);
        var irt = inset.GetComponent<RectTransform>();
        irt.anchorMin = Vector2.zero;
        irt.anchorMax = Vector2.one;
        irt.offsetMin = new Vector2(U, U);
        irt.offsetMax = new Vector2(-U, -U);

        GameObject slice = NewUI("Slice", inset.transform);
        var raw = slice.AddComponent<RawImage>();
        raw.color = Color.white;

        var fitter = slice.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;
        return raw;
    }

    private static CTPlaneView AddView(GameObject go, string plane, RawImage raw, Slider slider,
        TMP_Text title, TMP_Text counter)
    {
        var view = go.AddComponent<CTPlaneView>();
        var so = new SerializedObject(view);
        so.FindProperty("plane").stringValue = plane;
        so.FindProperty("display").objectReferenceValue = raw;
        so.FindProperty("slider").objectReferenceValue = slider;
        so.FindProperty("title").objectReferenceValue = title;
        so.FindProperty("counter").objectReferenceValue = counter;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ---------------- helpers ----------------

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Fixed(GameObject go, float width, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();

        if (width > 0f)
        {
            le.minWidth = width;
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }

        if (height > 0f)
        {
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleHeight = 0f;
        }
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
        Rounded(go, CardBg, 32f);

        var v = go.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset((int)(3 * U), (int)(3 * U), (int)(3 * U), (int)(3 * U));
        v.spacing = 2f * U;
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

    private static Button IconButton(Transform parent, string glyph, string name)
    {
        GameObject go = NewUI(name, parent);
        Fixed(go, 6f * U, 6f * U);
        Image bg = Rounded(go, ButtonBg, 14f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        var c = btn.colors;
        c.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        c.highlightedColor = Color.white;
        c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        c.fadeDuration = 0.08f;
        btn.colors = c;

        TMP_Text t = Label(go.transform, glyph, 26f, TextPrimary, TextAlignmentOptions.Center, "Glyph", FontStyles.Bold);
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
        Rounded(bg, Track, 0.4f * U);

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
