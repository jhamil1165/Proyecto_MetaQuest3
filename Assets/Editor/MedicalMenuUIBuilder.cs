using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Construye el menú como Canvas en World Space dispuestos en arco, con un sistema
/// de diseño explícito: retícula de 8, escala tipográfica, esquinas por SDF y sombra.
///
/// Dos decisiones que vienen de errores previos:
///  - Los elementos de tamaño fijo (punto, interruptor, miniatura) van dentro de un
///    contenedor que el layout sí puede estirar. Ponerlos directos hacía que el
///    HorizontalLayoutGroup los estirara a la altura de la fila y se deformaran.
///  - Las esquinas las dibuja un shader por SDF, no el sprite integrado de Unity,
///    cuyo radio fijo se deforma al escalar el panel.
/// </summary>
public static class MedicalMenuUIBuilder
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string MatDir = "Assets/Materials/UI";

    // ---------- tokens de diseño ----------
    private const float U = 8f;            // unidad de retícula

    private const float RadiusCard = 28f;
    private const float RadiusRow = 14f;
    private const float RadiusSmall = 16f;

    private const float FontTitle = 34f;
    private const float FontSection = 15f;
    private const float FontRow = 24f;
    private const float FontMeta = 17f;
    private const float FontFooter = 19f;
    private const float FontBadge = 20f;

    private static readonly Color CardBg      = new Color(1f, 1f, 1f, 1f);
    private static readonly Color TextPrimary = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color TextMuted   = new Color(0.55f, 0.58f, 0.62f, 1f);
    private static readonly Color DividerCol  = new Color(0.91f, 0.92f, 0.93f, 1f);
    private static readonly Color SubtleBg    = new Color(0.955f, 0.960f, 0.968f, 1f);
    private static readonly Color ToggleOn    = new Color(0.13f, 0.15f, 0.18f, 1f);
    private static readonly Color ToggleOff   = new Color(0.83f, 0.84f, 0.86f, 1f);
    private static readonly Color RowHover    = new Color(0.955f, 0.960f, 0.968f, 1f);
    private static readonly Color Accent      = new Color(0.09f, 0.42f, 0.88f, 1f);
    private static readonly Color ShadowCol   = new Color(0.05f, 0.07f, 0.11f, 0.22f);

    // ---------- geometría del arco ----------
    private const float ArcRadius = 1.8f;
    private const float ArcHeight = 1.5f;
    private const float ArcAngle = 22f;
    private const float CanvasScale = 0.0016f;

    private static Material _matRounded;
    private static Material _matShadow;
    private static Sprite _knob;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step23 - Design Pass")]
    public static void BuildDesignedMenu()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Shader roundedShader = Shader.Find("MedicalViewer/UIRoundedRect");
        _knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (roundedShader == null || _knob == null || _font == null)
        {
            Debug.LogError("STEP23_FAILED faltan shader, knob o fuente");
            return;
        }

        System.IO.Directory.CreateDirectory(MatDir);
        _matRounded = MakeMaterial(MatDir + "/Mat_UI_Rounded.mat", roundedShader, 1.5f);
        _matShadow = MakeMaterial(MatDir + "/Mat_UI_Shadow.mat", roundedShader, 22f);

        GameObject uiManager = GameObject.Find("UI_Manager");
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        GameObject heart = GameObject.Find("Heart");
        GameObject stomach = GameObject.Find("estomago_sin_render");
        var actions = Object.FindObjectOfType<MedicalMenuActions>();

        if (uiManager == null || centerEye == null || actions == null)
        {
            Debug.LogError("STEP23_FAILED falta UI_Manager, CenterEyeAnchor o MedicalMenuActions");
            return;
        }

        Transform old = uiManager.transform.Find("Medical_Menu_UI");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        Transform eye = centerEye.transform;
        Camera eyeCam = centerEye.GetComponent<Camera>();

        GameObject root = new GameObject("Medical_Menu_UI");
        root.transform.SetParent(uiManager.transform, false);
        // La raíz va en la posición de los ojos: los canvas cuelgan de ella a ArcRadius
        // hacia delante, en local. En ejecución WorkspaceRecenter la recoloca según la
        // pose real del usuario, y todo el arco la sigue.
        root.transform.position = eye.position + Vector3.up * ArcHeight;
        root.transform.rotation = eye.rotation;
        root.AddComponent<CanvasGroup>();
        root.AddComponent<MedicalMenuIntro>();

        // ================= panel izquierdo =================
        GameObject canvasActions = ArcCanvas("Canvas_Actions", root.transform, eye, -ArcAngle, eyeCam);
        GameObject actionsCard = Card("Card_Actions", canvasActions.transform, new Vector2(560f, 460f));

        Header(actionsCard.transform, "Medical Viewer", out _, sb);
        SectionLabel(actionsCard.transform, "VISTAS");
        ActionRow(actionsCard.transform, "DICOM", "Imágenes por cortes", actions.OpenDICOM, "OpenDICOM", sb);
        ActionRow(actionsCard.transform, "Segmentación", "Capas anatómicas", actions.OpenSegmentation, "OpenSegmentation", sb);
        ActionRow(actionsCard.transform, "Modelo 3D", "Órganos interactivos", actions.Open3DModel, "Open3DModel", sb);
        Line(actionsCard.transform);
        ActionRow(actionsCard.transform, "Salir", null, actions.ExitMenu, "ExitMenu", sb);

        // ================= panel derecho =================
        GameObject canvasSystems = ArcCanvas("Canvas_Systems", root.transform, eye, ArcAngle, eyeCam);
        GameObject systemsCard = Card("Card_Systems", canvasSystems.transform, new Vector2(580f, 860f));

        Header(systemsCard.transform, "Sistemas", out TMP_Text badge, sb);
        SectionLabel(systemsCard.transform, "ANATOMÍA");

        var panel = systemsCard.AddComponent<OrganSystemsPanel>();

        var labels = new[] { "Corazón", "Hígado", "Estómago", "Páncreas", "Vesícula" };
        var targets = new[]
        {
            heart,
            GameObject.Find("Hígado"),
            GameObject.Find("Estómago"),
            GameObject.Find("Páncreas"),
            GameObject.Find("Vesícula"),
        };
        var thumbPaths = new[]
        {
            "Assets/UI/Thumbnails/Thumb_Heart.png",
            "Assets/UI/Thumbnails/Thumb_Higado.png",
            "Assets/UI/Thumbnails/Thumb_Estomago.png",
            "Assets/UI/Thumbnails/Thumb_Pancreas.png",
            "Assets/UI/Thumbnails/Thumb_Vesicula.png",
        };
        var dots = new[]
        {
            new Color(0.78f, 0.22f, 0.24f),
            new Color(0.60f, 0.22f, 0.20f),
            new Color(0.80f, 0.64f, 0.42f),
            new Color(0.85f, 0.72f, 0.35f),
            new Color(0.36f, 0.60f, 0.36f),
        };

        var toggles = new Toggle[labels.Length];
        var counts = new TMP_Text[labels.Length];

        for (int i = 0; i < labels.Length; i++)
        {
            Sprite thumb = AssetDatabase.LoadAssetAtPath<Sprite>(thumbPaths[i]);
            string meta = Metadata(targets[i]);
            SystemRow(systemsCard.transform, labels[i], meta, thumb, dots[i], out toggles[i], out counts[i]);
            sb.AppendLine($"fila: {labels[i]} | {meta} | thumb={(thumb != null ? "sí" : "NO")}");
        }

        Line(systemsCard.transform);
        Footer(systemsCard.transform, out TMP_Text footerText, out Button hideAll);

        var so = new SerializedObject(panel);
        var rowsArray = so.FindProperty("rows");
        rowsArray.arraySize = labels.Length;
        for (int i = 0; i < labels.Length; i++)
        {
            var el = rowsArray.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("label").stringValue = labels[i];
            el.FindPropertyRelative("target").objectReferenceValue = targets[i];
            el.FindPropertyRelative("toggle").objectReferenceValue = toggles[i];
            el.FindPropertyRelative("countText").objectReferenceValue = counts[i];
        }
        so.FindProperty("headerBadge").objectReferenceValue = badge;
        so.FindProperty("footerText").objectReferenceValue = footerText;
        so.FindProperty("hideAllButton").objectReferenceValue = hideAll;
        so.ApplyModifiedPropertiesWithoutUndo();

        // ================= panel DICOM =================
        // Va al frente, entre los otros dos, y pertenece a la vista DICOM: solo
        // aparece al pulsar esa acción.
        GameObject canvasDicom = ArcCanvas("Canvas_DICOM", root.transform, eye, ArcAngle, eyeCam);
        GameObject dicomCard = Card("Card_DICOM", canvasDicom.transform, new Vector2(600f, 820f));
        Header(dicomCard.transform, "Tomografía", out _, sb);
        SectionLabel(dicomCard.transform, "CORTES TC");

        var viewer = dicomCard.AddComponent<CTSliceViewer>();

        GameObject imageBox = NewUI("SliceBox", dicomCard.transform);
        imageBox.AddComponent<LayoutElement>().minHeight = 56 * U;
        Rounded(imageBox, new Color(0.10f, 0.11f, 0.13f, 1f), RadiusSmall, _matRounded);

        GameObject rawGo = NewUI("Slice", imageBox.transform);
        var raw = rawGo.AddComponent<RawImage>();
        raw.color = Color.white;
        var rawRt = raw.rectTransform;
        rawRt.anchorMin = Vector2.zero;
        rawRt.anchorMax = Vector2.one;
        rawRt.offsetMin = new Vector2(U, U);
        rawRt.offsetMax = new Vector2(-U, -U);

        GameObject infoRow = NewUI("InfoRow", dicomCard.transform);
        infoRow.AddComponent<LayoutElement>().minHeight = 4.5f * U;
        var ih = infoRow.AddComponent<HorizontalLayoutGroup>();
        ih.padding = new RectOffset((int)(1.5f * U), (int)(1.5f * U), 0, 0);
        ih.childAlignment = TextAnchor.MiddleLeft;
        ih.childControlWidth = true;
        ih.childControlHeight = true;

        TMP_Text organLabel = Label(infoRow.transform, "Hígado", FontRow, TextPrimary,
            TextAlignmentOptions.MidlineLeft, "Organ");
        organLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        TMP_Text planeLabel = Label(infoRow.transform, "Axial", FontMeta, Accent,
            TextAlignmentOptions.MidlineRight, "Plane");
        planeLabel.gameObject.AddComponent<LayoutElement>().minWidth = 12 * U;

        TMP_Text sliceLabel = Label(infoRow.transform, "0 / 0", FontMeta, TextMuted,
            TextAlignmentOptions.MidlineRight, "Slice");
        sliceLabel.gameObject.AddComponent<LayoutElement>().minWidth = 14 * U;

        GameObject sliderGo = NewUI("SliceSlider", dicomCard.transform);
        sliderGo.AddComponent<LayoutElement>().minHeight = 5 * U;
        Slider slider = BuildSlider(sliderGo);

        Line(dicomCard.transform);

        GameObject planeRow = NewUI("PlaneRow", dicomCard.transform);
        planeRow.AddComponent<LayoutElement>().minHeight = 6 * U;
        var ph = planeRow.AddComponent<HorizontalLayoutGroup>();
        ph.padding = new RectOffset((int)(1.5f * U), (int)(1.5f * U), 0, 0);
        ph.spacing = (int)U;
        ph.childAlignment = TextAnchor.MiddleCenter;
        ph.childControlWidth = true;
        ph.childControlHeight = true;

        PlaneButton(planeRow.transform, "Axial", viewer.ShowAxial, "ShowAxial");
        PlaneButton(planeRow.transform, "Coronal", viewer.ShowCoronal, "ShowCoronal");
        PlaneButton(planeRow.transform, "Sagital", viewer.ShowSagital, "ShowSagital");

        GameObject organRow = NewUI("OrganRow", dicomCard.transform);
        organRow.AddComponent<LayoutElement>().minHeight = 6 * U;
        var oh = organRow.AddComponent<HorizontalLayoutGroup>();
        oh.padding = new RectOffset((int)(1.5f * U), (int)(1.5f * U), 0, 0);
        oh.spacing = (int)U;
        oh.childAlignment = TextAnchor.MiddleCenter;
        oh.childControlWidth = true;
        oh.childControlHeight = true;

        PlaneButton(organRow.transform, "Anterior", viewer.PreviousOrgan, "PreviousOrgan");
        PlaneButton(organRow.transform, "Siguiente", viewer.NextOrgan, "NextOrgan");

        var vso = new SerializedObject(viewer);
        vso.FindProperty("display").objectReferenceValue = raw;
        vso.FindProperty("sliceSlider").objectReferenceValue = slider;
        vso.FindProperty("sliceLabel").objectReferenceValue = sliceLabel;
        vso.FindProperty("organLabel").objectReferenceValue = organLabel;
        vso.FindProperty("planeLabel").objectReferenceValue = planeLabel;
        vso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("panel DICOM construido (visor de cortes de TC)");

        // ================= plumbing =================
        var eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem != null)
        {
            var standalone = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null) Object.DestroyImmediate(standalone);
            if (eventSystem.GetComponent<XRUIInputModule>() == null)
                eventSystem.gameObject.AddComponent<XRUIInputModule>();
        }

        var aso = new SerializedObject(actions);
        aso.FindProperty("menuRoot").objectReferenceValue = root;
        var m3d = aso.FindProperty("model3DObjects");
        // Solo el panel: el propio OrganSystemsPanel se encarga de encender y apagar
        // los organos. Meterlos tambien aqui hacia que ambos se pisaran.
        var valid = new System.Collections.Generic.List<Object> { canvasSystems };
        m3d.arraySize = valid.Count;
        for (int i = 0; i < valid.Count; i++) m3d.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
        aso.ApplyModifiedPropertiesWithoutUndo();

        var dicomArr = aso.FindProperty("dicomObjects");
        GameObject ndi = GameObject.Find("NDI_Screen");
        var dicomList = new System.Collections.Generic.List<Object> { canvasDicom };
        if (ndi != null) dicomList.Add(ndi);
        dicomArr.arraySize = dicomList.Count;
        for (int i = 0; i < dicomList.Count; i++)
            dicomArr.GetArrayElementAtIndex(i).objectReferenceValue = dicomList[i];
        aso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("dicomObjects = [Canvas_DICOM, NDI_Screen]");

        var toggleInput = Object.FindObjectOfType<MenuToggleInput>();
        if (toggleInput != null)
        {
            var tso = new SerializedObject(toggleInput);
            tso.FindProperty("menuRoot").objectReferenceValue = root;
            tso.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        // Dejar la escena guardada en el mismo estado con el que arranca (SoloMenu):
        // si no, en el editor se ven los tres paneles apilados en el mismo sitio.
        canvasSystems.SetActive(false);
        canvasDicom.SetActive(false);
        sb.AppendLine("Canvas_Systems y Canvas_DICOM desactivados (estado inicial SoloMenu)");

        EditorUtility.SetDirty(root);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"arco ±{ArcAngle}°, radio {ArcRadius} m, altura {ArcHeight} m");
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step23_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP23_DONE");
    }

    private static Material MakeMaterial(string path, Shader shader, float softness)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.shader = shader;
        m.SetColor("_Color", Color.white);
        m.SetFloat("_Softness", softness);
        m.SetFloat("_BorderWidth", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static GameObject ArcCanvas(string name, Transform parent, Transform eye, float angle, Camera cam)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        go.AddComponent<TrackedDeviceGraphicRaycaster>();

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(640f, 900f);

        // Colocación LOCAL respecto a la raíz, no en coordenadas del mundo.
        //
        // Antes se usaba eye.rotation aquí. Como la raíz también se rota en tiempo de
        // ejecución hacia donde mira el usuario, las dos rotaciones se sumaban y los
        // paneles acababan mirando 180° al lado contrario: se veían por detrás, con
        // el texto invertido. Ahora la raíz es la única que decide la orientación.
        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        // anchoredPosition3D, no localPosition: en un RectTransform la X/Y de
        // localPosition es un valor derivado que Unity recalcula desde
        // m_AnchoredPosition al recargar la escena. Escribirla parece funcionar hasta
        // que cierras y vuelves a abrir, y entonces los paneles aparecen apilados en
        // X=0 con la rotación correcta pero sin separación en el arco.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = yaw * Vector3.forward * ArcRadius;
        rt.localRotation = yaw;
        rt.localScale = Vector3.one * CanvasScale;
        return go;
    }

    private static string Metadata(GameObject target)
    {
        if (target == null) return "sin modelo";

        int tris = 0, pieces = 0;
        foreach (var mf in target.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
            tris += mf.sharedMesh.triangles.Length / 3;
            pieces++;
        }
        return $"{tris:N0} triángulos · {pieces} pieza{(pieces == 1 ? "" : "s")}";
    }

    // ---------------- helpers ----------------

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static Image Rounded(GameObject go, Color color, float radius, Material mat)
    {
        var img = go.AddComponent<Image>();
        img.material = mat;
        img.color = color;
        go.AddComponent<UIRoundedRect>();

        var so = new SerializedObject(go.GetComponent<UIRoundedRect>());
        so.FindProperty("radius").floatValue = radius;
        so.ApplyModifiedPropertiesWithoutUndo();
        return img;
    }

    /// <summary>Tarjeta con sombra difusa detrás: sin elevación, un panel blanco sobre fondo claro se ve plano.</summary>
    private static GameObject Card(string name, Transform parent, Vector2 size)
    {
        GameObject wrapper = NewUI(name + "_Wrapper", parent);
        var wrt = wrapper.GetComponent<RectTransform>();
        wrt.anchorMin = wrt.anchorMax = wrt.pivot = new Vector2(0.5f, 0.5f);
        wrt.anchoredPosition = Vector2.zero;
        wrt.sizeDelta = size;

        GameObject shadow = NewUI("Shadow", wrapper.transform);
        var srt = shadow.GetComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = Vector2.one;
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.offsetMin = new Vector2(-2f * U, -3f * U);  // se estira con la tarjeta
        srt.offsetMax = new Vector2(2f * U, U);
        Rounded(shadow, ShadowCol, RadiusCard + 12f, _matShadow);

        GameObject card = NewUI(name, wrapper.transform);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = size;
        crt.anchoredPosition = Vector2.zero;
        Rounded(card, CardBg, RadiusCard, _matRounded);

        // La altura la decide el contenido: con altura fija sobraba espacio muerto
        // al final de la tarjeta de acciones.
        var fitter = card.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var v = card.AddComponent<VerticalLayoutGroup>();
        v.padding = new RectOffset((int)(3 * U), (int)(3 * U), (int)(3 * U), (int)(2.5f * U));
        v.spacing = (int)U;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childControlHeight = true;
        return card;
    }

    private static TMP_Text Label(Transform parent, string content, float size, Color color,
        TextAlignmentOptions align, string name, FontStyles style = FontStyles.Normal, float spacing = 0f)
    {
        GameObject go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.characterSpacing = spacing;
        t.enableWordWrapping = false;
        return t;
    }

    /// <summary>Contenedor estirable con un hijo de tamaño fijo centrado.</summary>
    private static GameObject FixedSlot(Transform parent, float w, float h, string name)
    {
        GameObject slot = NewUI(name + "_Slot", parent);
        var le = slot.AddComponent<LayoutElement>();
        le.minWidth = w;
        le.preferredWidth = w;
        le.flexibleWidth = 0f;

        GameObject inner = NewUI(name, slot.transform);
        var rt = inner.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
        return inner;
    }

    private static void Header(Transform parent, string title, out TMP_Text badge, StringBuilder sb)
    {
        GameObject row = NewUI("Header", parent);
        row.AddComponent<LayoutElement>().minHeight = 6 * U;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childForceExpandWidth = false;
        h.childControlWidth = true;
        h.childControlHeight = true;

        Label(row.transform, title, FontTitle, TextPrimary, TextAlignmentOptions.MidlineLeft, "Title", FontStyles.Bold)
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        GameObject badgeBox = FixedSlot(row.transform, 7 * U, 4.5f * U, "Badge");
        Rounded(badgeBox, SubtleBg, RadiusSmall, _matRounded);
        badge = Label(badgeBox.transform, "0", FontBadge, TextMuted, TextAlignmentOptions.Center, "BadgeText");
        Stretch(badge.rectTransform);

        sb.AppendLine("header: " + title);
    }

    /// <summary>Etiqueta pequeña en mayúsculas que agrupa las filas siguientes.</summary>
    private static void SectionLabel(Transform parent, string text)
    {
        GameObject go = NewUI("Section_" + text, parent);
        go.AddComponent<LayoutElement>().minHeight = 3 * U;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = text;
        t.fontSize = FontSection;
        t.color = TextMuted;
        t.alignment = TextAlignmentOptions.BottomLeft;
        t.characterSpacing = 14f;
        t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false;
    }

    private static void Line(Transform parent)
    {
        GameObject go = NewUI("Divider", parent);
        go.AddComponent<LayoutElement>().minHeight = 1.5f;
        go.AddComponent<Image>().color = DividerCol;
    }

    private static void ActionRow(Transform parent, string title, string subtitle,
        UnityEngine.Events.UnityAction call, string methodName, StringBuilder sb)
    {
        GameObject row = NewUI("Row_" + methodName, parent);
        row.AddComponent<LayoutElement>().minHeight = subtitle == null ? 7 * U : 8.5f * U;

        Image bg = Rounded(row, new Color(1f, 1f, 1f, 0f), RadiusRow, _matRounded);

        var btn = row.AddComponent<Button>();
        btn.targetGraphic = bg;
        var colors = btn.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0f);
        colors.highlightedColor = RowHover;
        colors.pressedColor = DividerCol;
        colors.selectedColor = new Color(1f, 1f, 1f, 0f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(btn.onClick, call);

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset((int)(2 * U), (int)(2 * U), 0, 0);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;

        GameObject textCol = NewUI("Text", row.transform);
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var v = textCol.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleLeft;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.spacing = 2;

        Label(textCol.transform, title, FontRow, TextPrimary, TextAlignmentOptions.MidlineLeft, "Title")
            .gameObject.AddComponent<LayoutElement>().minHeight = 3.5f * U;

        if (subtitle != null)
        {
            Label(textCol.transform, subtitle, FontMeta, TextMuted, TextAlignmentOptions.MidlineLeft, "Subtitle")
                .gameObject.AddComponent<LayoutElement>().minHeight = 2.5f * U;
        }

        GameObject chevronSlot = FixedSlot(row.transform, 3 * U, 3 * U, "Chevron");
        var chev = chevronSlot.AddComponent<TextMeshProUGUI>();
        chev.font = _font;
        chev.text = ">";
        chev.fontSize = FontRow;
        chev.color = TextMuted;
        chev.alignment = TextAlignmentOptions.Center;

        sb.AppendLine("fila de acción: " + title + " -> " + methodName);
    }

    private static void SystemRow(Transform parent, string label, string meta, Sprite thumb, Color dotColor,
        out Toggle toggle, out TMP_Text countText)
    {
        GameObject row = NewUI("Row_" + label, parent);
        row.AddComponent<LayoutElement>().minHeight = 11 * U;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset((int)(1.5f * U), (int)(1.5f * U), (int)U, (int)U);
        h.spacing = (int)(1.5f * U);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;

        GameObject thumbBox = FixedSlot(row.transform, 9 * U, 9 * U, "Thumb");
        Rounded(thumbBox, SubtleBg, RadiusSmall, _matRounded);
        if (thumb != null)
        {
            GameObject imgGo = NewUI("Image", thumbBox.transform);
            var iImg = imgGo.AddComponent<Image>();
            iImg.sprite = thumb;
            iImg.preserveAspect = true;
            Stretch(iImg.rectTransform);
        }

        GameObject textCol = NewUI("Text", row.transform);
        textCol.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var v = textCol.AddComponent<VerticalLayoutGroup>();
        v.childAlignment = TextAnchor.MiddleLeft;
        v.childForceExpandHeight = false;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.spacing = 2;

        GameObject titleRow = NewUI("TitleRow", textCol.transform);
        titleRow.AddComponent<LayoutElement>().minHeight = 3.5f * U;
        var th = titleRow.AddComponent<HorizontalLayoutGroup>();
        th.spacing = (int)(1.25f * U);
        th.childAlignment = TextAnchor.MiddleLeft;
        th.childControlWidth = true;
        th.childControlHeight = true;

        GameObject dot = FixedSlot(titleRow.transform, 2f * U, 2f * U, "Dot");
        var dImg = dot.AddComponent<Image>();
        dImg.sprite = _knob;
        dImg.color = dotColor;

        Label(titleRow.transform, label, FontRow, TextPrimary, TextAlignmentOptions.MidlineLeft, "Title")
            .gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        Label(textCol.transform, meta, FontMeta, TextMuted, TextAlignmentOptions.MidlineLeft, "Meta")
            .gameObject.AddComponent<LayoutElement>().minHeight = 2.75f * U;

        GameObject countSlot = FixedSlot(row.transform, 6 * U, 3 * U, "Count");
        countText = countSlot.AddComponent<TextMeshProUGUI>();
        countText.font = _font;
        countText.text = "0";
        countText.fontSize = FontBadge;
        countText.color = TextMuted;
        countText.alignment = TextAlignmentOptions.MidlineRight;

        // Interruptor: contenedor estirable + píldora de tamaño fijo dentro.
        GameObject pillGo = FixedSlot(row.transform, 7.25f * U, 4f * U, "Toggle");
        Image bg = Rounded(pillGo, ToggleOn, 2f * U, _matRounded);

        toggle = pillGo.AddComponent<Toggle>();
        toggle.targetGraphic = bg;
        toggle.isOn = true;
        toggle.graphic = null;

        GameObject knob = NewUI("Knob", pillGo.transform);
        var krt = knob.GetComponent<RectTransform>();
        krt.anchorMin = krt.anchorMax = krt.pivot = new Vector2(0.5f, 0.5f);
        krt.sizeDelta = new Vector2(3.25f * U, 3.25f * U);
        krt.anchoredPosition = new Vector2(1.5f * U, 0f);
        var kImg = knob.AddComponent<Image>();
        kImg.sprite = _knob;
        kImg.color = Color.white;

        var pill = pillGo.AddComponent<PillToggle>();
        var pso = new SerializedObject(pill);
        pso.FindProperty("knob").objectReferenceValue = krt;
        pso.FindProperty("background").objectReferenceValue = bg;
        pso.FindProperty("onColor").colorValue = ToggleOn;
        pso.FindProperty("offColor").colorValue = ToggleOff;
        pso.FindProperty("travel").floatValue = 3f * U;
        pso.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Footer(Transform parent, out TMP_Text footerText, out Button hideAll)
    {
        GameObject row = NewUI("Footer", parent);
        row.AddComponent<LayoutElement>().minHeight = 5 * U;

        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.padding = new RectOffset((int)(1.5f * U), (int)(1.5f * U), 0, 0);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;

        footerText = Label(row.transform, "0 piezas visibles", FontFooter, TextMuted,
            TextAlignmentOptions.MidlineLeft, "VisibleCount");
        footerText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

        GameObject btn = FixedSlot(row.transform, 16 * U, 4.5f * U, "HideAll");
        Image bImg = Rounded(btn, new Color(1f, 1f, 1f, 0f), RadiusSmall, _matRounded);

        hideAll = btn.AddComponent<Button>();
        hideAll.targetGraphic = bImg;
        var c = hideAll.colors;
        c.normalColor = new Color(1f, 1f, 1f, 0f);
        c.highlightedColor = SubtleBg;
        c.fadeDuration = 0.08f;
        hideAll.colors = c;

        TMP_Text t = Label(btn.transform, "Ocultar todo", FontFooter, Accent, TextAlignmentOptions.Center, "Label");
        Stretch(t.rectTransform);
    }

    /// <summary>Slider minimalista: pista, relleno de acento y asa redonda.</summary>
    private static Slider BuildSlider(GameObject go)
    {
        var slider = go.AddComponent<Slider>();

        GameObject bg = NewUI("Background", go.transform);
        var brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0.5f);
        brt.anchorMax = new Vector2(1f, 0.5f);
        brt.sizeDelta = new Vector2(-4f * U, 0.75f * U);
        brt.anchoredPosition = Vector2.zero;
        Rounded(bg, DividerCol, 0.4f * U, _matRounded);

        GameObject fillArea = NewUI("Fill Area", go.transform);
        var frt = fillArea.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0.5f);
        frt.anchorMax = new Vector2(1f, 0.5f);
        frt.sizeDelta = new Vector2(-4f * U, 0.75f * U);
        frt.anchoredPosition = Vector2.zero;

        GameObject fill = NewUI("Fill", fillArea.transform);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.sizeDelta = Vector2.zero;
        Rounded(fill, Accent, 0.4f * U, _matRounded);

        GameObject handleArea = NewUI("Handle Slide Area", go.transform);
        var hrt = handleArea.GetComponent<RectTransform>();
        hrt.anchorMin = Vector2.zero;
        hrt.anchorMax = Vector2.one;
        hrt.sizeDelta = new Vector2(-4f * U, 0f);
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

    private static void PlaneButton(Transform parent, string label,
        UnityEngine.Events.UnityAction call, string methodName)
    {
        GameObject go = NewUI("Btn_" + methodName, parent);
        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        Image bg = Rounded(go, SubtleBg, RadiusSmall, _matRounded);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bg;
        var c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.88f, 0.90f, 0.93f);
        c.pressedColor = DividerCol;
        c.fadeDuration = 0.08f;
        btn.colors = c;
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(btn.onClick, call);

        TMP_Text t = Label(go.transform, label, FontMeta, TextPrimary, TextAlignmentOptions.Center, "Label");
        Stretch(t.rectTransform);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
