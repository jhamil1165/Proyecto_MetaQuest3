using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de controles bajo el menú, con dos filas:
///
///   Plano de corte   activar o quitar el corte, centrarlo sobre los órganos y girarlo
///                    90 grados (corte horizontal o vertical)
///   Volumen 3D       tejido, esqueleto, solo órganos, u ocultarlo
///
/// El plano solo, flotando, no se explica: el usuario tiene que poder decidir cuándo
/// corta. El botón B/Y del mando hace lo mismo que el primer botón.
///
/// El esqueleto no se saca de un umbral de grises: la segmentación trae marcadas las
/// costillas, vértebras, caderas y fémures, así que se enciende como un elemento más.
/// </summary>
public static class MedicalCutPanel
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string CanvasName = "Canvas_Controles";
    private const float U = 8f;
    private const float Scale = 0.0016f;

    // Vidrio oscuro. Meta pide no pasar de #DADADA en claro ni bajar de #1A1A1A en oscuro,
    // y sobre passthrough Apple recomienda material oscuro con texto blanco.
    private static readonly Color CardBg = new Color32(0x1E, 0x22, 0x28, 224);
    private static readonly Color Subtle = new Color(1f, 1f, 1f, 0.10f);

    // Color "al 100%" de una pastilla. El estado normal del boton lo rebaja al 45%,
    // que es el 0,10 de siempre; asi queda margen para encenderla al apuntar.
    private static readonly Color PillFull = new Color(1f, 1f, 1f, 0.22f);
    private static readonly Color TextPrimary = new Color32(0xF0, 0xF2, 0xF5, 255);
    private static readonly Color TextMuted = new Color32(0xA8, 0xB1, 0xBC, 255);

    private static Material _rounded;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step73 - Panel de corte y volumen")]
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
        GameObject volume = GameObject.Find("Volumen_3D");
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);

        if (rootGo == null || plane == null || actions == null || _rounded == null || _font == null)
        {
            Debug.LogError("[Step73] Falta Medical_Menu_UI, Plano_de_corte, el menu, el material o la fuente.");
            return;
        }

        var input = plane.GetComponent<ClippingPlaneInput>();
        if (input == null) input = plane.AddComponent<ClippingPlaneInput>();

        // Se rehace entero, y tambien se limpia el panel anterior si existia.
        foreach (string old in new[] { CanvasName, "Canvas_Corte" })
        {
            Transform existing = rootGo.transform.Find(old);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
        }

        var go = new GameObject(CanvasName, typeof(RectTransform));
        go.transform.SetParent(rootGo.transform, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Object.FindObjectOfType<Camera>();
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(700f, 330f);
        // Por debajo de todo lo que hay delante del usuario. Los organos llegan a -0,46 y
        // el plano de corte a -0,36, pero estan a 1 metro y el panel a 1,8, asi que desde
        // los ojos se ponen delante aunque esten mas altos. Mirando por angulo, lo de
        // delante baja hasta unos -21 grados; el panel empieza por debajo de eso.
        rt.anchoredPosition3D = new Vector3(0f, -1.20f, 1.8f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one * Scale;

        GameObject card = NewUI("Card_Controles", go.transform);
        var crt = card.GetComponent<RectTransform>();
        crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(660f, 290f);
        Rounded(card, CardBg, 24f);

        // La tarjeta se ajusta a lo que lleve dentro: con una altura fija quedaba un
        // hueco blanco debajo de los botones.
        var fitter = card.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset((int)(2 * U), (int)(2 * U), (int)(2 * U), (int)(2 * U));
        layout.spacing = (int)U;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // ---- fila 1: plano de corte ----
        Title(card.transform, "Plano de corte");
        GameObject cutRow = Row(card.transform);
        Button toggle = PillButton(cutRow.transform, "Activar corte", out Image toggleBg, out TMP_Text toggleText);
        Button center = PillButton(cutRow.transform, "Centrar", out _, out _);
        Button rotate = PillButton(cutRow.transform, "Girar 90°", out _, out _);
        Button hold = PillButton(cutRow.transform, "Suelto", out _, out _);

        // Corto a proposito: mas largo se sale del ancho de la tarjeta y se corta la ultima
        // palabra.
        Hint(card.transform, "Agárralo con el gatillo  ·  joysticks: inclinar y subir  ·  " +
                            "menú: botón ☰ del mando");

        UnityEventTools.AddVoidPersistentListener(toggle.onClick, input.Toggle);
        UnityEventTools.AddVoidPersistentListener(center.onClick, input.Center);
        UnityEventTools.AddVoidPersistentListener(rotate.onClick, input.Rotate90);

        // Sujetar el cuerpo mientras se corta: al apuntar al plano es facil rozar lo que hay
        // detras y llevarselo sin querer.
        var still = WireHoldStill(rootGo, hold, sb);

        var iso = new SerializedObject(input);
        iso.FindProperty("stateLabel").objectReferenceValue = toggleText;
        iso.FindProperty("stateBackground").objectReferenceValue = toggleBg;
        iso.FindProperty("cutting").boolValue = false;
        iso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(input);
        sb.AppendLine("fila 1: activar corte, centrar y girar 90 grados (tambien el boton B/Y del mando)");

        // ---- fila 2: los tres cortes cruzados ----
        var tri = Object.FindObjectOfType<TriPlaneView>(true);
        if (tri != null)
        {
            Title(card.transform, "Tres planos (neuronavegador)");
            GameObject triRow = Row(card.transform);

            Button show = PillButton(triRow.transform, "Mostrar", out _, out _);
            Button recenter = PillButton(triRow.transform, "Centrar cruz", out _, out _);

            UnityEventTools.AddVoidPersistentListener(show.onClick, tri.Toggle);
            UnityEventTools.AddVoidPersistentListener(recenter.onClick, tri.Center);

            var tso = new SerializedObject(tri);
            tso.FindProperty("toggleButton").objectReferenceValue = show;
            tso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tri);

            sb.AppendLine("fila 2: mostrar los tres cortes y centrar la cruz");
        }
        else
        {
            sb.AppendLine("[AVISO] no hay TriPlaneView: ejecuta antes el Step94");
        }

        // ---- fila 3: volumen 3D ----
        if (volume != null)
        {
            Title(card.transform, "Volumen 3D");
            GameObject volumeRow = Row(card.transform);

            var display = volume.GetComponent<VolumeDisplay>();
            if (display == null) display = volume.AddComponent<VolumeDisplay>();

            var buttons = new List<Button>
            {
                PillButton(volumeRow.transform, "Tejido", out _, out _),
                PillButton(volumeRow.transform, "Esqueleto", out _, out _),
                PillButton(volumeRow.transform, "Órganos", out _, out _),
            };
            Button hide = PillButton(volumeRow.transform, "Ocultar", out _, out _);

            for (int i = 0; i < buttons.Count; i++)
            {
                UnityEventTools.AddIntPersistentListener(buttons[i].onClick, display.Show, i);
            }
            UnityEventTools.AddVoidPersistentListener(hide.onClick, display.ToggleVisible);

            var vso = new SerializedObject(display);
            vso.FindProperty("volume").objectReferenceValue = volume.GetComponent<Renderer>();

            var modes = vso.FindProperty("modes");
            modes.arraySize = 3;
            SetMode(modes.GetArrayElementAtIndex(0), "Tejido", 40f, 500f, 0.12f, 0.85f, 0f);
            SetMode(modes.GetArrayElementAtIndex(1), "Esqueleto", 300f, 1200f, 0.03f, 0.25f, 0.95f);
            SetMode(modes.GetArrayElementAtIndex(2), "Órganos", 40f, 400f, 0.03f, 0.95f, 0f);

            var list = vso.FindProperty("buttons");
            list.arraySize = buttons.Count;
            for (int i = 0; i < buttons.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = buttons[i];

            vso.FindProperty("current").intValue = 0;
            vso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(display);
            sb.AppendLine("fila 2: tejido, esqueleto, organos y ocultar");
        }
        else
        {
            sb.AppendLine("[AVISO] no hay Volumen_3D: la fila del volumen no se crea");
        }

        // ---- fila 3: medicion ----
        MeasureTool tool = MedicalMeasureSetup.Ensure(rootGo, sb);
        if (tool != null)
        {
            Title(card.transform, "Medición");
            GameObject measureRow = Row(card.transform);

            Button measure = PillButton(measureRow.transform, "Medir", out _, out _);
            Button clear = PillButton(measureRow.transform, "Borrar", out _, out _);

            UnityEventTools.AddVoidPersistentListener(measure.onClick, tool.Toggle);
            UnityEventTools.AddVoidPersistentListener(clear.onClick, tool.Clear);

            var mso = new SerializedObject(tool);
            mso.FindProperty("toggleButton").objectReferenceValue = measure;
            mso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tool);

            sb.AppendLine("fila 3: medir y borrar (los puntos se clavan con el boton A del mando)");
        }

        // ---- el menú enciende el panel donde hay algo que manejar ----
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
        MedicalCtViewerLayout.RenderPreview(go, OutDir + "step73_panel.png", sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step73_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP73_DONE");
    }

    private static void SetMode(SerializedProperty mode, string label, float center, float width,
        float tissue, float organ, float bone)
    {
        mode.FindPropertyRelative("label").stringValue = label;
        mode.FindPropertyRelative("windowCenter").floatValue = center;
        mode.FindPropertyRelative("windowWidth").floatValue = width;
        mode.FindPropertyRelative("tissueOpacity").floatValue = tissue;
        mode.FindPropertyRelative("organOpacity").floatValue = organ;
        mode.FindPropertyRelative("boneOpacity").floatValue = bone;
    }

    private static void Title(Transform parent, string text)
    {
        TMP_Text title = Label(parent, text, 20f, TextMuted, TextAlignmentOptions.MidlineLeft, "Title_" + text, FontStyles.UpperCase);
        title.characterSpacing = 5f;
        Fixed(title.gameObject, 3 * U);
    }

    /// <summary>
    /// Deja enganchado el fijador del cuerpo. Se fija todo lo que se pueda agarrar menos el
    /// propio plano de corte, que tiene que seguir moviendose.
    /// </summary>
    private static HoldStill WireHoldStill(GameObject root, Button button, StringBuilder sb)
    {
        GameObject go = GameObject.Find("Fijacion_Cuerpo");
        if (go == null)
        {
            go = new GameObject("Fijacion_Cuerpo");
            if (root != null) go.transform.SetParent(root.transform, false);
        }

        var still = go.GetComponent<HoldStill>();
        if (still == null) still = go.AddComponent<HoldStill>();

        var grabs = new List<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
        foreach (var grab in Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>(true))
        {
            if (grab.name == "Plano_de_corte") continue;   // este tiene que moverse
            grabs.Add(grab);
        }

        var so = new SerializedObject(still);
        var list = so.FindProperty("targets");
        list.arraySize = grabs.Count;
        for (int i = 0; i < grabs.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = grabs[i];

        so.FindProperty("toggleButton").objectReferenceValue = button;
        so.FindProperty("holding").boolValue = false;   // suelto de entrada
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(still);

        UnityEventTools.AddVoidPersistentListener(button.onClick, still.Toggle);

        sb.AppendLine("fijacion del cuerpo: " + grabs.Count + " objetos (el plano queda libre)");
        return still;
    }

    /// <summary>Línea de ayuda, más pequeña y apagada que un título.</summary>
    private static void Hint(Transform parent, string text)
    {
        TMP_Text hint = Label(parent, text, 15f, TextMuted, TextAlignmentOptions.MidlineLeft, "Ayuda");
        Fixed(hint.gameObject, (int)(2.4f * U));
    }

    private static GameObject Row(Transform parent)
    {
        GameObject row = NewUI("Fila", parent);

        // 9*U y no 7*U: a 1,8 m del usuario esto son unos 3,6 grados de alto. Con 7*U
        // se quedaba por debajo de 3 y apuntar con el mando se volvia puntilloso.
        Fixed(row, 9 * U);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = (int)U;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        return row;
    }

    private static Button PillButton(Transform parent, string label, out Image background, out TMP_Text text)
    {
        GameObject go = NewUI("Btn_" + label, parent);
        go.AddComponent<LayoutElement>().flexibleWidth = 1f;
        background = Rounded(go, PillFull, 20f);

        var button = go.AddComponent<Button>();
        button.targetGraphic = background;

        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.45f);        // en reposo, discreta
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);   // al apuntar, se enciende
        colors.pressedColor = new Color(0.42f, 0.63f, 1f, 1f);    // al pulsar, azul
        colors.selectedColor = new Color(1f, 1f, 1f, 0.45f);
        colors.fadeDuration = 0.06f;
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
