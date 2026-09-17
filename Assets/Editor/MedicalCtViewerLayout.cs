using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visor de TC con el AXIAL como vista principal: grande a la izquierda, y coronal y
/// sagital más pequeños, uno sobre otro, a la derecha. Es lo que pide el documento de
/// la Fase 2 ("visor axial (Principal)") y como se reparte una estación de radiología.
///
/// Las medidas de las imágenes son fijas y cuadradas a propósito. Si un layout las
/// estirara, la anatomía se deformaría; dentro de cada recuadro, un AspectRatioFitter
/// mantiene la proporción real del corte (las capturas de Slicer no son cuadradas).
///
/// BuildViewer es reutilizable: la vista Segmentación monta con él sus dos visores
/// (TC normal y TC con el órgano pintado). Al reconstruir se conserva la configuración
/// del CTMultiPlaneViewer existente (series y órganos), solo se rehace la interfaz.
/// </summary>
public static class MedicalCtViewerLayout
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string PreviewDir = "Assets/UI/CTPreview";
    private const float U = 8f;

    // Tarjeta de 1060 x 700. Con cabecera, pie y márgenes quedan ~534 px de alto para
    // los planos: axial = 32 título + 418 imagen + 24 contador + 36 deslizador + 24 de
    // espacios; a la derecha, dos filas de 259 + 16 de separación.
    public const float CardW = 1060f;
    public const float CardH = 700f;
    private const float AxialFilm = 418f;
    private const float SmallFilm = 259f;

    private static readonly Color CardBg      = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TextPrimary = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color TextMuted   = new Color(0.55f, 0.58f, 0.62f, 1f);
    private static readonly Color SubtleBg    = new Color(0.955f, 0.960f, 0.968f, 1f);
    private static readonly Color Accent      = new Color(0.09f, 0.42f, 0.88f, 1f);
    private static readonly Color DividerCol  = new Color(0.91f, 0.92f, 0.93f, 1f);
    private static readonly Color FilmBg      = new Color(0.07f, 0.08f, 0.10f, 1f);

    private static Material _matRounded;
    private static Sprite _knob;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step59 - Visor con axial principal")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        if (!LoadResources())
        {
            Debug.LogError("[Step59] Faltan el material redondeado, el knob o la fuente.");
            return;
        }

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        Transform canvas = rootGo != null ? rootGo.transform.Find("Canvas_DICOM") : null;
        if (canvas == null)
        {
            Debug.LogError("[Step59] No encuentro Medical_Menu_UI/Canvas_DICOM.");
            return;
        }

        CTMultiPlaneViewer viewer = BuildViewer(canvas.gameObject, "Card_DICOM", "Tomografía", sb);
        ApplyInitialState(viewer, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        // Después de guardar: la cámara temporal del render no debe quedar en la escena.
        RenderPreview(canvas.gameObject, OutDir + "step59_canvas_dicom.png", sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step59_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP59_DONE");
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
    public static CTMultiPlaneViewer BuildViewer(GameObject canvas, string cardName, string title, StringBuilder sb)
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

        Header(card.transform, title, out TMP_Text organLabel);

        GameObject planes = NewUI("Planes", card.transform);
        planes.AddComponent<LayoutElement>().flexibleHeight = 1f;
        var ph = planes.AddComponent<HorizontalLayoutGroup>();
        ph.spacing = 2 * U;
        ph.childAlignment = TextAnchor.UpperLeft;
        ph.childControlWidth = true;
        ph.childControlHeight = true;
        ph.childForceExpandWidth = false;
        ph.childForceExpandHeight = false;

        CTPlaneView axial = BuildAxial(planes.transform);

        GameObject side = NewUI("Side", planes.transform);
        side.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var sv = side.AddComponent<VerticalLayoutGroup>();
        sv.spacing = 2 * U;
        sv.childAlignment = TextAnchor.UpperLeft;
        sv.childControlWidth = true;
        sv.childControlHeight = true;
        sv.childForceExpandWidth = true;
        sv.childForceExpandHeight = false;

        CTPlaneView coronal = BuildSmall(side.transform, "coronal");
        CTPlaneView sagital = BuildSmall(side.transform, "sagital");

        Divider(card.transform);
        Footer(card.transform, out Button prev, out Button next);

        var so = new SerializedObject(viewer);
        var list = so.FindProperty("planes");
        list.arraySize = 3;
        list.GetArrayElementAtIndex(0).objectReferenceValue = axial;
        list.GetArrayElementAtIndex(1).objectReferenceValue = coronal;
        list.GetArrayElementAtIndex(2).objectReferenceValue = sagital;
        so.FindProperty("organLabel").objectReferenceValue = organLabel;
        so.FindProperty("previousButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(viewer);

        sb.AppendLine(canvas.name + "/" + cardName + ": axial " + AxialFilm + " px, coronal y sagital " +
                      SmallFilm + " px, configuracion " + (config != null ? "conservada" : "nueva"));
        return viewer;
    }

    /// <summary>Contadores, deslizadores y vistas previas según la primera serie u órgano.</summary>
    public static void ApplyInitialState(CTMultiPlaneViewer viewer, StringBuilder sb)
    {
        var so = new SerializedObject(viewer);
        var studies = so.FindProperty("studies");
        var organs = so.FindProperty("organs");

        string folder, label;
        int axialCount, coronalCount, sagitalCount;

        if (studies.arraySize > 0)
        {
            var s = studies.GetArrayElementAtIndex(0);
            folder = s.FindPropertyRelative("folder").stringValue;
            label = s.FindPropertyRelative("label").stringValue;
            axialCount = s.FindPropertyRelative("axial").intValue;
            coronalCount = s.FindPropertyRelative("coronal").intValue;
            sagitalCount = s.FindPropertyRelative("sagittal").intValue;
        }
        else if (organs.arraySize > 0)
        {
            folder = organs.GetArrayElementAtIndex(0).stringValue;
            label = Capitalize(folder);
            axialCount = 267;
            coronalCount = 512;
            sagitalCount = 512;
        }
        else
        {
            sb.AppendLine("[AVISO] el visor no tiene series ni organos");
            return;
        }

        if (so.FindProperty("organLabel").objectReferenceValue is TMP_Text organLabel)
        {
            organLabel.text = label;
            EditorUtility.SetDirty(organLabel);
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

        int w = 1600;
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

    private static CTPlaneView BuildAxial(Transform parent)
    {
        GameObject column = NewUI("Plane_axial", parent);
        Fixed(column, AxialFilm, -1f);

        var v = column.AddComponent<VerticalLayoutGroup>();
        v.spacing = U;
        v.childAlignment = TextAnchor.UpperLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        TMP_Text title = Label(column.transform, "Axial", 26f, TextPrimary,
            TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold);
        Fixed(title.gameObject, -1f, 4 * U);

        RawImage raw = Film(column.transform, AxialFilm);

        TMP_Text counter = Label(column.transform, "", 18f, TextMuted,
            TextAlignmentOptions.MidlineLeft, "Counter");
        Fixed(counter.gameObject, -1f, 3 * U);

        GameObject sliderGo = NewUI("Slider", column.transform);
        Fixed(sliderGo, -1f, 4.5f * U);
        Slider slider = BuildSlider(sliderGo);

        return AddView(column, "axial", raw, slider, title, counter);
    }

    private static CTPlaneView BuildSmall(Transform parent, string plane)
    {
        GameObject row = NewUI("Plane_" + plane, parent);
        Fixed(row, -1f, SmallFilm);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 2 * U;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        RawImage raw = Film(row.transform, SmallFilm);

        GameObject controls = NewUI("Controls", row.transform);
        controls.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var v = controls.AddComponent<VerticalLayoutGroup>();
        v.spacing = U;
        v.childAlignment = TextAnchor.MiddleLeft;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        TMP_Text title = Label(controls.transform, Capitalize(plane), 24f, TextPrimary,
            TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold);
        Fixed(title.gameObject, -1f, 4 * U);

        TMP_Text counter = Label(controls.transform, "", 18f, TextMuted,
            TextAlignmentOptions.MidlineLeft, "Counter");
        Fixed(counter.gameObject, -1f, 3 * U);

        GameObject sliderGo = NewUI("Slider", controls.transform);
        Fixed(sliderGo, -1f, 4.5f * U);
        Slider slider = BuildSlider(sliderGo);

        return AddView(row, plane, raw, slider, title, counter);
    }

    private static RawImage Film(Transform parent, float size)
    {
        // Fondo oscuro: una tomografía se lee mucho mejor sobre negro.
        GameObject film = NewUI("Film", parent);
        Fixed(film, size, size);
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

    // ---------------- helpers (mismo estilo que el resto de la interfaz) ----------------

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

    private static void Header(Transform parent, string title, out TMP_Text organLabel)
    {
        GameObject row = NewUI("Header", parent);
        Fixed(row, -1f, 6 * U);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;

        Label(row.transform, title, 34f, TextPrimary, TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        organLabel = Label(row.transform, "", 26f, Accent, TextAlignmentOptions.MidlineRight, "Organ");
        organLabel.gameObject.AddComponent<LayoutElement>().minWidth = 30 * U;
    }

    private static void Divider(Transform parent)
    {
        GameObject go = NewUI("Divider", parent);
        Fixed(go, -1f, 1.5f);
        go.AddComponent<Image>().color = DividerCol;
    }

    private static void Footer(Transform parent, out Button previous, out Button next)
    {
        GameObject row = NewUI("Footer", parent);
        Fixed(row, -1f, 6 * U);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = U;
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
