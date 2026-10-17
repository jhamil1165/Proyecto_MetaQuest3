using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pone una etiqueta flotante encima de cada órgano ("Hígado · 1665 mL") y el vigilante del
/// passthrough en la escena. Después saca una foto desde donde está la cabeza del usuario
/// para ver cómo quedan, sin guardar las etiquetas (esas se crean al arrancar la app).
///
/// Los volúmenes son los medidos en la segmentación del TAC (ver MedicalMeasureSetup). El
/// corazón viene de otro modelo y no tiene volumen medido: lleva solo el nombre.
/// </summary>
public static class MedicalOrganTagSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private static readonly Dictionary<string, (string title, float ml)> Organs =
        new Dictionary<string, (string, float)>
        {
            { "HÍGADO", ("Hígado", 1665f) },
            { "ESTÓMAGO", ("Estómago", 436f) },
            { "PÁNCREAS", ("Páncreas", 67f) },
            { "VESÍCULA", ("Vesícula", 12f) },
            { "CORAZÓN", ("Corazón", 0f) },
        };

    [MenuItem("MedicalViewer/Step100 - Etiquetas de organos y passthrough")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        var tags = new List<OrganTag>();

        FieldInfo nameField = typeof(OrganSelectionInfo).GetField("organName", BindingFlags.Instance | BindingFlags.NonPublic);

        foreach (var info in Object.FindObjectsOfType<OrganSelectionInfo>(true))
        {
            string key = ((string)nameField.GetValue(info) ?? "").Trim().ToUpperInvariant();
            if (!Organs.TryGetValue(key, out var data))
            {
                sb.AppendLine("  sin etiqueta (no lo conozco): " + key + " en " + Branch(info.transform));
                continue;
            }

            var tag = info.GetComponent<OrganTag>();
            if (tag == null) tag = info.gameObject.AddComponent<OrganTag>();

            var so = new SerializedObject(tag);
            so.FindProperty("title").stringValue = data.title;
            so.FindProperty("millilitres").floatValue = data.ml;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tag);
            tags.Add(tag);

            sb.AppendLine(string.Format("  {0} -> '{1}'{2}", Branch(info.transform), tag.Text,
                info.gameObject.activeInHierarchy ? "" : "  (objeto apagado)"));
        }
        sb.AppendLine("etiquetas: " + tags.Count);

        // ---- passthrough ----
        var manager = Object.FindObjectOfType<OVRManager>(true);
        if (manager == null)
        {
            sb.AppendLine("[AVISO] no hay OVRManager en la escena");
        }
        else
        {
            if (manager.GetComponent<PassthroughGuard>() == null) manager.gameObject.AddComponent<PassthroughGuard>();
            manager.isInsightPassthroughEnabled = true;
            EditorUtility.SetDirty(manager);
            sb.AppendLine("vigilante del passthrough en " + Branch(manager.transform));
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        Shoot(tags, OutDir + "step100_etiquetas.png", sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step100_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP100_DONE");
    }

    /// <summary>
    /// Foto desde la cámara del usuario, mirando a los órganos. Las etiquetas se arman a mano
    /// (en el editor no hay Awake) y se borran al acabar, para no dejarlas en la escena.
    /// </summary>
    private static void Shoot(List<OrganTag> tags, string path, StringBuilder sb)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

        var bounds = new Bounds();
        bool any = false;
        foreach (var t in tags)
        {
            if (!t.gameObject.activeInHierarchy) continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
        }
        if (!any) { sb.AppendLine("foto: no hay organos encendidos"); return; }

        var head = Camera.main;
        Vector3 eye = head != null ? head.transform.position : bounds.center + Vector3.back;
        Vector3 toOrgans = bounds.center - eye;
        toOrgans.y = 0f;
        if (toOrgans.sqrMagnitude < 1e-4f) toOrgans = Vector3.forward;

        var camGo = new GameObject("TmpTagCamera");
        camGo.tag = "MainCamera";   // las etiquetas miran a Camera.main
        if (head != null) head.tag = "Untagged";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.36f, 0.38f, 0.40f, 1f);   // gris de habitación
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 20f;

        float radius = bounds.extents.magnitude;
        cam.transform.position = bounds.center - toOrgans.normalized * (radius * 2.6f) + Vector3.up * radius * 0.6f;
        cam.transform.LookAt(bounds.center + Vector3.up * radius * 0.25f);

        MethodInfo awake = typeof(OrganTag).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo tagField = typeof(OrganTag).GetField("_tag", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo groupField = typeof(OrganTag).GetField("_group", BindingFlags.Instance | BindingFlags.NonPublic);
        var built = new List<GameObject>();
        foreach (var t in tags)
        {
            if (!t.gameObject.activeInHierarchy) continue;
            awake.Invoke(t, null);
            ((CanvasGroup)groupField.GetValue(t)).alpha = 1f;
            var go = ((Transform)tagField.GetValue(t)).gameObject;
            built.Add(go);
        }

        // Dos pasadas: la primera deja que el texto mida su ancho, la segunda ya coloca bien.
        for (int pass = 0; pass < 2; pass++)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var go in built)
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)go.transform.Find("Pastilla"));
            foreach (var t in tags) if (t.gameObject.activeInHierarchy) t.Place();
        }

        const int w = 1600, h = 1000;
        cam.aspect = (float)w / h;
        var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = target;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(w, h, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
        sb.AppendLine("foto: " + path);

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);
        if (head != null) head.tag = "MainCamera";
        foreach (var go in built) Object.DestroyImmediate(go);
    }

    private static string Branch(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
