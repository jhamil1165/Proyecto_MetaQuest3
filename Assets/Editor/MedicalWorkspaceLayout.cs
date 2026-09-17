using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Reparte el arco del puesto de trabajo para que ningún panel se solape.
///
/// Con los tres planos en fila el visor de TC mide 1,5 m o más de ancho. A −38° y
/// 1,87 m su borde derecho se metía dentro del menú central, y además seguía encima
/// de la pantalla NDI. Aquí cada panel va a un ángulo calculado según lo que ocupa:
///
///   menú (centro, 0°)     640 px x 0,0016 = 1,02 m a 1,8 m  -> ±16°
///   TC (izquierda, −42°)  1548 px x 0,001 = 1,55 m a 2,0 m  -> ±21°
///   NDI (derecha, +42°)   1,6 m a 2,1 m                     -> ±21°
///
/// Separación necesaria entre centros: 16 + 21 + margen 3 = 40°. Se usan 42°.
/// El paso mide después el hueco real entre paneles y avisa si alguno se solapa.
/// </summary>
public static class MedicalWorkspaceLayout
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private const float CtAngle = -42f;
    private const float CtRadius = 2.0f;
    private const float CtScale = 0.001f;

    private const float NdiAngle = 42f;
    private const float NdiRadius = 2.1f;

    [MenuItem("MedicalViewer/Step63 - Repartir TC, menu y NDI sin solaparse")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        if (rootGo == null)
        {
            Debug.LogError("[Step63] No encuentro Medical_Menu_UI.");
            return;
        }
        Transform root = rootGo.transform;

        PlaceCanvas(root, "Canvas_DICOM", CtAngle, CtRadius, CtScale, sb);
        PlaceTransform(root, "NDI_Screen", NdiAngle, NdiRadius, sb);
        // El cartel de "sin señal" sigue desactivado, pero acompaña a su pantalla.
        PlaceCanvas(root, "Canvas_NDI_Status", NdiAngle, NdiRadius - 0.02f, -1f, sb);

        sb.AppendLine();
        sb.AppendLine("=== espacio que ocupa cada panel (grados) ===");
        var spans = new List<(string name, float from, float to)>();
        foreach (string name in new[] { "Canvas_DICOM", "Canvas_Actions", "NDI_Screen" })
        {
            if (TrySpan(root, name, out float from, out float to))
            {
                spans.Add((name, from, to));
                sb.AppendLine("   " + name + ": " + from.ToString("F1") + " a " + to.ToString("F1"));
            }
        }

        spans.Sort((a, b) => a.from.CompareTo(b.from));
        bool overlap = false;
        for (int i = 0; i + 1 < spans.Count; i++)
        {
            float gap = spans[i + 1].from - spans[i].to;
            sb.AppendLine("   hueco entre " + spans[i].name + " y " + spans[i + 1].name + ": " +
                          gap.ToString("F1") + " grados" + (gap < 0f ? "  <-- SOLAPE" : ""));
            if (gap < 0f) overlap = true;
        }
        sb.AppendLine(overlap ? "RESULTADO: HAY SOLAPES" : "RESULTADO: sin solapes");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        if (MedicalCtViewerLayout.LoadResources())
        {
            Transform ct = root.Find("Canvas_DICOM");
            if (ct != null) MedicalCtViewerLayout.RenderPreview(ct.gameObject, OutDir + "step63_canvas_dicom.png", sb);
        }
        RenderOverview(root, OutDir + "step63_vista_usuario.png", sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step63_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP63_DONE");
    }

    private static void PlaceCanvas(Transform root, string name, float angle, float radius, float scale, StringBuilder sb)
    {
        var rt = root.Find(name) as RectTransform;
        if (rt == null)
        {
            sb.AppendLine("[AVISO] " + name + " no encontrado");
            return;
        }

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        // anchoredPosition3D y no localPosition: en un RectTransform la X/Y de
        // localPosition se recalcula desde m_AnchoredPosition al cargar la escena.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = yaw * Vector3.forward * radius;
        rt.localRotation = yaw;
        if (scale > 0f) rt.localScale = Vector3.one * scale;

        EditorUtility.SetDirty(rt);
        sb.AppendLine(name + ": " + angle.ToString("F0") + " grados, " + radius.ToString("F2") + " m, " +
                      (rt.rect.width * rt.localScale.x).ToString("F2") + " x " +
                      (rt.rect.height * rt.localScale.y).ToString("F2") + " m");
    }

    private static void PlaceTransform(Transform root, string name, float angle, float radius, StringBuilder sb)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            sb.AppendLine("[AVISO] " + name + " no encontrado");
            return;
        }

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        t.localPosition = yaw * Vector3.forward * radius;
        t.localRotation = yaw;

        EditorUtility.SetDirty(t);
        sb.AppendLine(name + ": " + angle.ToString("F0") + " grados, " + radius.ToString("F2") + " m, " +
                      t.localScale.x.ToString("F2") + " x " + t.localScale.y.ToString("F2") + " m");
    }

    /// <summary>Ángulos (respecto al centro del arco) que cubre un panel de lado a lado.</summary>
    private static bool TrySpan(Transform root, string name, out float from, out float to)
    {
        from = to = 0f;
        Transform t = root.Find(name);
        if (t == null) return false;

        Vector3 local;
        float width;
        if (t is RectTransform rt)
        {
            local = rt.anchoredPosition3D;
            width = rt.rect.width * rt.localScale.x;
        }
        else
        {
            local = t.localPosition;
            width = t.localScale.x; // Quad de 1 m escalado
        }

        float radius = new Vector2(local.x, local.z).magnitude;
        if (radius < 0.01f) return false;

        float centre = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
        float half = Mathf.Atan2(width * 0.5f, radius) * Mathf.Rad2Deg;
        from = centre - half;
        to = centre + half;
        return true;
    }

    /// <summary>Imagen desde los ojos del usuario, mirando al frente, para ver el arco completo.</summary>
    public static void RenderOverview(Transform root, string path, StringBuilder sb)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            sb.AppendLine("render vista usuario: sin GPU, no se genera imagen");
            return;
        }

        Canvas.ForceUpdateCanvases();
        foreach (var t in root.GetComponentsInChildren<TMP_Text>(false)) t.ForceMeshUpdate();

        var camGo = new GameObject("TmpOverviewCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.transform.SetPositionAndRotation(root.position, root.rotation);
        cam.fieldOfView = 70f; // vertical; con 2400x1100 da unos 114 grados en horizontal
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;
        cam.clearFlags = CameraClearFlags.Skybox;

        const int w = 2400;
        const int h = 1100;
        cam.aspect = (float)w / h;

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

        sb.AppendLine("render vista usuario: " + path + " (" + w + "x" + h + ")");
    }
}
