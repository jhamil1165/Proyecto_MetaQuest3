using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Paso 3 de la interfaz: la vista SEGMENTACIÓN, que junta lo que pidió el profesor.
///
///   izquierda  tomografía normal (serie de cuerpo completo, la misma de las capturas)
///   derecha    la misma tomografía con la MÁSCARA del órgano encima (CT/masks), con los
///              botones de órgano en la cabecera; "Todos" pinta los cuatro a la vez
///   delante    el modelo 3D del órgano elegido
///
/// Las dos tomografías se mueven juntas. El orden de los cortes se comprobó con imágenes:
/// las capturas de Slicer numeran el axial desde los muslos y el coronal desde la espalda,
/// y la serie DICOM al revés; en sagital coinciden (las dos empiezan por el lado derecho
/// del paciente, donde está el hígado).
///
/// Los dos visores se crean copiando Canvas_DICOM, para heredar su Canvas y el raycaster
/// que permite usar los deslizadores con las manos, y se reconstruyen con el mismo diseño.
/// </summary>
public static class MedicalSegmentationView
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const float U = 8f;

    // Mismo reparto que la vista DICOM: TC a −42° y +42° a 2 m, escala 0,001.
    private const float Angle = 42f;
    private const float Radius = 2.0f;
    private const float Scale = 0.001f;

    // Los dos visores muestran la misma serie (el derecho lleva la máscara encima), así que
    // los cortes coinciden uno a uno. Con las capturas de Slicer había que invertir axial y
    // coronal, pero ya no se usan en esta vista.
    private const bool ReverseAxial = false;
    private const bool ReverseCoronal = false;
    private const bool ReverseSagital = false;

    private static readonly (string label, string folder)[] Organs =
    {
        ("Hígado", "higado"),
        ("Estómago", "estomago"),
        ("Páncreas", "pancreas"),
        ("Vesícula", "vesicula"),
    };

    private static readonly Color Accent      = new Color(0.09f, 0.42f, 0.88f, 1f);
    private static readonly Color ChipIdle    = new Color(0.955f, 0.960f, 0.968f, 1f);
    private static readonly Color TextPrimary = new Color(0.13f, 0.15f, 0.18f, 1f);

    private static Material _matRounded;
    private static TMP_FontAsset _font;

    [MenuItem("MedicalViewer/Step66 - Vista Segmentacion con mascaras")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        _matRounded = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/UI/Mat_UI_Rounded.mat");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (!MedicalCtViewerLayout.LoadResources() || _matRounded == null || _font == null)
        {
            Debug.LogError("[Step66] Faltan el material redondeado o la fuente.");
            return;
        }

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        Transform dicom = rootGo != null ? rootGo.transform.Find("Canvas_DICOM") : null;
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        var systems = Object.FindObjectOfType<OrganSystemsPanel>(true);

        if (dicom == null || actions == null || systems == null)
        {
            Debug.LogError("[Step66] Falta Canvas_DICOM, MedicalMenuActions u OrganSystemsPanel.");
            return;
        }

        Transform root = rootGo.transform;

        // ---- 1. Tomografía normal (izquierda) ----
        GameObject clean = GetOrClone(root, dicom.gameObject, "Canvas_SegClean", sb);
        Place(clean, -Angle);
        CTMultiPlaneViewer cleanViewer = MedicalCtViewerLayout.BuildViewer(clean, "Card_SegClean", sb, "Tomografía");
        KeepOnlyStudy(cleanViewer, "serie_cuerpo", sb);
        HideArrows(cleanViewer);
        MedicalCtViewerLayout.ApplyInitialState(cleanViewer, sb);

        // ---- 2. Tomografía con el órgano pintado (derecha) ----
        GameObject painted = GetOrClone(root, dicom.gameObject, "Canvas_SegPainted", sb);
        Place(painted, Angle);
        CTMultiPlaneViewer paintedViewer = MedicalCtViewerLayout.BuildViewer(painted, "Card_SegPainted", sb, "Segmentación");
        KeepOnlyStudy(paintedViewer, "serie_cuerpo", sb);
        HideArrows(paintedViewer);
        MedicalCtViewerLayout.ApplyInitialState(paintedViewer, sb);

        Transform card = paintedViewer.transform;
        Transform header = card.Find("Header");
        GameObject planesRow = card.Find("Planes").gameObject;

        var chips = BuildChips(header, out Button allChip);
        GameObject allNote = BuildAllNote(card);

        // ---- 3. Sincronización de cortes ----
        var sync = GetOrAdd<SliceSync>(painted);
        var cleanPlanes = new SerializedObject(cleanViewer).FindProperty("planes");
        var paintedPlanes = new SerializedObject(paintedViewer).FindProperty("planes");
        var sso = new SerializedObject(sync);
        var pairs = sso.FindProperty("pairs");
        pairs.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var a = cleanPlanes.GetArrayElementAtIndex(i).objectReferenceValue as CTPlaneView;
            var b = paintedPlanes.GetArrayElementAtIndex(i).objectReferenceValue as CTPlaneView;
            string plane = a != null ? new SerializedObject(a).FindProperty("plane").stringValue : "?";
            bool reverse = plane == "axial" ? ReverseAxial : plane == "coronal" ? ReverseCoronal : ReverseSagital;

            var pair = pairs.GetArrayElementAtIndex(i);
            pair.FindPropertyRelative("a").objectReferenceValue = a;
            pair.FindPropertyRelative("b").objectReferenceValue = b;
            pair.FindPropertyRelative("reverse").boolValue = reverse;
            sb.AppendLine("sincronizado " + plane + (reverse ? " (orden invertido)" : " (mismo orden)"));
        }
        sso.ApplyModifiedPropertiesWithoutUndo();

        // ---- 4. Panel de órganos ----
        var models = ModelsByFolder(systems, sb);
        var panel = GetOrAdd<OrganSegmentationPanel>(painted);
        var pso = new SerializedObject(panel);
        var entries = pso.FindProperty("organs");
        entries.arraySize = Organs.Length;
        for (int i = 0; i < Organs.Length; i++)
        {
            var e = entries.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("label").stringValue = Organs[i].label;
            e.FindPropertyRelative("folder").stringValue = Organs[i].folder;
            models.TryGetValue(Organs[i].folder, out GameObject model);
            e.FindPropertyRelative("model").objectReferenceValue = model;
            e.FindPropertyRelative("button").objectReferenceValue = chips[i];
            sb.AppendLine("boton " + Organs[i].label + " -> modelo " + (model != null ? model.name : "NO ENCONTRADO"));
        }
        pso.FindProperty("allButton").objectReferenceValue = allChip;
        pso.FindProperty("paintedViewer").objectReferenceValue = paintedViewer;
        pso.FindProperty("paintedLabel").objectReferenceValue =
            new SerializedObject(paintedViewer).FindProperty("organLabel").objectReferenceValue;
        pso.FindProperty("paintedPlanes").objectReferenceValue = planesRow;
        pso.FindProperty("allNote").objectReferenceValue = allNote;
        pso.FindProperty("sync").objectReferenceValue = sync;
        pso.FindProperty("useMasks").boolValue = true;
        pso.FindProperty("allMasksFolder").stringValue = "todos";
        pso.ApplyModifiedPropertiesWithoutUndo();

        // Lo que se ve fuera de Play: hígado elegido.
        for (int i = 0; i < chips.Count; i++) Style(chips[i], i == 0);
        Style(allChip, false);
        allNote.SetActive(false);
        if (new SerializedObject(paintedViewer).FindProperty("organLabel").objectReferenceValue is TMP_Text lbl)
        {
            lbl.text = Organs[0].label;
            EditorUtility.SetDirty(lbl);
        }

        AssignMaskPreviews(paintedViewer, Organs[0].folder, sb);

        // ---- 5. Menú ----
        var aso = new SerializedObject(actions);
        var seg = aso.FindProperty("segmentationObjects");
        seg.arraySize = 2;
        seg.GetArrayElementAtIndex(0).objectReferenceValue = clean;
        seg.GetArrayElementAtIndex(1).objectReferenceValue = painted;
        // Arranca en Segmentación para poder probarla; Step55 lo devuelve a solo el menú.
        aso.FindProperty("initialView").intValue = (int)MedicalMenuActions.InitialView.Segmentacion;
        aso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);
        sb.AppendLine("menu: Segmentacion -> Canvas_SegClean + Canvas_SegPainted; vista inicial = Segmentacion");

        // Guardados apagados: los enciende el menú al elegir la vista.
        clean.SetActive(false);
        painted.SetActive(false);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        RenderViews(root, dicom.gameObject, clean, painted, models, sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step66_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP66_DONE");
    }

    // ---------------- piezas ----------------

    private static GameObject GetOrClone(Transform root, GameObject source, string name, StringBuilder sb)
    {
        Transform existing = root.Find(name);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            sb.AppendLine(name + ": ya existia, se reconstruye");
            return existing.gameObject;
        }

        GameObject copy = Object.Instantiate(source, root);
        copy.name = name;
        copy.SetActive(true);
        sb.AppendLine(name + ": creado a partir de " + source.name);
        return copy;
    }

    private static void Place(GameObject canvas, float angle)
    {
        var rt = canvas.GetComponent<RectTransform>();
        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        // anchoredPosition3D y no localPosition: en un RectTransform la X/Y de
        // localPosition se recalcula desde m_AnchoredPosition al cargar la escena.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = yaw * Vector3.forward * Radius;
        rt.localRotation = yaw;
        rt.localScale = Vector3.one * Scale;
        EditorUtility.SetDirty(rt);
    }

    private static void KeepOnlyStudy(CTMultiPlaneViewer viewer, string folder, StringBuilder sb)
    {
        var so = new SerializedObject(viewer);
        var studies = so.FindProperty("studies");

        int found = -1;
        for (int i = 0; i < studies.arraySize; i++)
        {
            if (studies.GetArrayElementAtIndex(i).FindPropertyRelative("folder").stringValue == folder) found = i;
        }

        if (found < 0)
        {
            studies.arraySize = 1;
            var s = studies.GetArrayElementAtIndex(0);
            s.FindPropertyRelative("folder").stringValue = folder;
            s.FindPropertyRelative("label").stringValue = "Cuerpo completo";
            s.FindPropertyRelative("axial").intValue = 267;
            s.FindPropertyRelative("coronal").intValue = 512;
            s.FindPropertyRelative("sagittal").intValue = 512;
        }
        else
        {
            if (found > 0) studies.MoveArrayElement(found, 0);
            studies.arraySize = 1;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("   visor normal: solo la serie " + folder);
    }

    private static void UseOrgans(CTMultiPlaneViewer viewer, StringBuilder sb)
    {
        var so = new SerializedObject(viewer);
        so.FindProperty("studies").arraySize = 0;

        var organs = so.FindProperty("organs");
        organs.arraySize = Organs.Length;
        for (int i = 0; i < Organs.Length; i++)
        {
            organs.GetArrayElementAtIndex(i).stringValue = Organs[i].folder;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("   visor pintado: capturas de Slicer por organo");
    }

    /// <summary>Máscara del corte central en la capa de cada plano, para verla fuera de Play.</summary>
    private static void AssignMaskPreviews(CTMultiPlaneViewer viewer, string mask, StringBuilder sb)
    {
        var planes = new SerializedObject(viewer).FindProperty("planes");
        for (int i = 0; i < planes.arraySize; i++)
        {
            if (!(planes.GetArrayElementAtIndex(i).objectReferenceValue is CTPlaneView view)) continue;

            var vso = new SerializedObject(view);
            string plane = vso.FindProperty("plane").stringValue;
            int count = vso.FindProperty("sliceCount").intValue;
            if (!(vso.FindProperty("overlay").objectReferenceValue is RawImage overlay))
            {
                sb.AppendLine("[AVISO] " + plane + " sin capa de mascara");
                continue;
            }

            string file = mask + "_" + plane + "_" + (count / 2).ToString("D4") + ".png";
            string src = Application.streamingAssetsPath + "/CT/masks/" + mask + "/" + plane + "/" + file;
            if (!System.IO.File.Exists(src))
            {
                sb.AppendLine("[AVISO] no existe " + src);
                continue;
            }

            string dst = "Assets/UI/CTPreview/Preview_mask_" + mask + "_" + plane + ".png";
            System.IO.File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(dst) is TextureImporter importer)
            {
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            overlay.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            overlay.enabled = true;
            EditorUtility.SetDirty(overlay);
            sb.AppendLine("   mascara " + mask + " en " + plane + ": " + file);
        }
    }

    private static void HideArrows(CTMultiPlaneViewer viewer)
    {
        // En esta vista el órgano lo eligen los botones de órgano, no las flechas.
        var so = new SerializedObject(viewer);
        foreach (string field in new[] { "previousButton", "nextButton" })
        {
            if (so.FindProperty(field).objectReferenceValue is Button b)
            {
                b.gameObject.SetActive(false);
                EditorUtility.SetDirty(b.gameObject);
            }
        }
    }

    private static Dictionary<string, GameObject> ModelsByFolder(OrganSystemsPanel systems, StringBuilder sb)
    {
        var result = new Dictionary<string, GameObject>();
        var rows = new SerializedObject(systems).FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var target = row.FindPropertyRelative("target").objectReferenceValue as GameObject;
            string key = Key(row.FindPropertyRelative("label").stringValue);
            if (target != null) result[key] = target;
        }
        return result;
    }

    private static string Key(string s)
    {
        // "Hígado" -> "higado", para casar etiquetas con nombres de carpeta.
        var b = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) b.Append(c);
        }
        return b.ToString().ToLowerInvariant();
    }

    private static List<Button> BuildChips(Transform header, out Button allChip)
    {
        Transform old = header.Find("Chips");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        GameObject row = NewUI("Chips", header);
        var h = row.AddComponent<HorizontalLayoutGroup>();
        h.spacing = U;
        h.childAlignment = TextAnchor.MiddleRight;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        var chips = new List<Button>();
        foreach (var organ in Organs) chips.Add(Chip(row.transform, organ.label, 150f));
        allChip = Chip(row.transform, "Todos", 120f);
        return chips;
    }

    private static Button Chip(Transform parent, string label, float width)
    {
        GameObject go = NewUI("Chip_" + label, parent);
        var le = go.AddComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = width;
        le.minHeight = le.preferredHeight = 5.5f * U;

        var img = go.AddComponent<Image>();
        img.material = _matRounded;
        img.color = ChipIdle;
        go.AddComponent<UIRoundedRect>();
        var rso = new SerializedObject(go.GetComponent<UIRoundedRect>());
        rso.FindProperty("radius").floatValue = 22f;
        rso.ApplyModifiedPropertiesWithoutUndo();

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.normalColor = Color.white;
        c.highlightedColor = new Color(0.90f, 0.92f, 0.95f, 1f);
        c.pressedColor = new Color(0.80f, 0.82f, 0.86f, 1f);
        c.selectedColor = Color.white;
        c.fadeDuration = 0.08f;
        btn.colors = c;

        var t = NewUI("Label", go.transform).AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.text = label;
        t.fontSize = 20f;
        t.color = TextPrimary;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return btn;
    }

    private static void Style(Button button, bool selected)
    {
        if (button == null) return;
        if (button.targetGraphic != null)
        {
            button.targetGraphic.color = selected ? Accent : ChipIdle;
            EditorUtility.SetDirty(button.targetGraphic);
        }
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.color = selected ? Color.white : TextPrimary;
            EditorUtility.SetDirty(text);
        }
    }

    private static GameObject BuildAllNote(Transform card)
    {
        Transform old = card.Find("AllNote");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Ocupa el hueco de los planos, fuera del layout de la tarjeta.
        GameObject note = NewUI("AllNote", card);
        note.AddComponent<LayoutElement>().ignoreLayout = true;
        var rt = note.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f * U, 3f * U);
        rt.offsetMax = new Vector2(-3f * U, -(3f * U + 7f * U + 2f * U));

        var img = note.AddComponent<Image>();
        img.material = _matRounded;
        img.color = ChipIdle;
        note.AddComponent<UIRoundedRect>();
        var rso = new SerializedObject(note.GetComponent<UIRoundedRect>());
        rso.FindProperty("radius").floatValue = 20f;
        rso.ApplyModifiedPropertiesWithoutUndo();

        var t = NewUI("Text", note.transform).AddComponent<TextMeshProUGUI>();
        t.font = _font;
        t.fontSize = 28f;
        t.color = TextPrimary;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.text = "<b>Los cuatro órganos</b><br><size=75%>Los modelos 3D ya se muestran juntos. " +
                 "La tomografía con todos los órganos pintados a la vez llegará con la " +
                 "segmentación exportada de 3D Slicer.<br>Elige un órgano para ver sus imágenes segmentadas.</size>";
        var trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(12f * U, 4f * U);
        trt.offsetMax = new Vector2(-12f * U, -4f * U);

        note.SetActive(false);
        return note;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // ---------------- imágenes de comprobación ----------------

    private static void RenderViews(Transform root, GameObject dicom, GameObject clean, GameObject painted,
        Dictionary<string, GameObject> models, StringBuilder sb)
    {
        MedicalCtViewerLayout.RenderPreview(painted, OutDir + "step66_segmentada.png", sb);
        MedicalCtViewerLayout.RenderPreview(clean, OutDir + "step66_normal.png", sb);

        // Vista del usuario simulando la vista Segmentación con el hígado elegido. La escena
        // ya está guardada: estos cambios son solo para la foto y se deshacen después.
        var ndi = root.Find("NDI_Screen");
        var saved = new List<(GameObject go, bool active)>();
        void Set(GameObject go, bool active)
        {
            if (go == null) return;
            saved.Add((go, go.activeSelf));
            go.SetActive(active);
        }

        Set(dicom, false);
        if (ndi != null) Set(ndi.gameObject, false);
        Set(clean, true);
        Set(painted, true);
        foreach (var kv in models) Set(kv.Value, kv.Key == "higado");

        MedicalWorkspaceLayout.RenderOverview(root, OutDir + "step66_vista_usuario.png", sb);

        for (int i = saved.Count - 1; i >= 0; i--) saved[i].go.SetActive(saved[i].active);
    }
}
