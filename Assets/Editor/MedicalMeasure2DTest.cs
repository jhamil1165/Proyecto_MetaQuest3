using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Prueba la regla 2D sin el visor: carga una imagen de TAC de verdad en el visor DICOM,
/// clava dos puntos con píxeles conocidos, y comprueba los milímetros, que la medida cuelga
/// de la imagen y que se esconde al pasar a otro corte. Saca una foto del resultado.
///
/// Las cuentas que tienen que salir (serie de cuerpo completo):
///   axial,   200 píxeles en horizontal a 0,977 mm  = 195,3 mm
///   coronal, 200 píxeles en vertical   a 1,705 mm  = 341,0 mm
///
/// No guarda nada: deja la escena como estaba.
/// </summary>
public static class MedicalMeasure2DTest
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string Images = "Assets/StreamingAssets/CT/serie_cuerpo/";

    [MenuItem("MedicalViewer/Step99 - Probar la regla 2D")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var tool = Object.FindObjectOfType<CTMeasure2D>(true);
        if (tool == null) { Debug.LogError("[Step99] No hay CTMeasure2D: ejecuta el Step98."); return; }

        var planes = (CTPlaneView[])typeof(CTMeasure2D)
            .GetField("planes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tool);

        CTPlaneView axial = null, coronal = null;
        foreach (var p in planes)
        {
            if (p == null) continue;
            if (p.Plane == "axial") axial = p;
            if (p.Plane == "coronal") coronal = p;
        }

        // Lo que haría el visor al cargar la serie de cuerpo completo.
        var study = (pixel: 0.977f, slice: 3.27f, count: 267);
        axial.FieldOfViewMm = new Vector2(512f * study.pixel, 512f * study.pixel);
        coronal.FieldOfViewMm = new Vector2(512f * study.pixel, study.count * study.slice);

        Texture2D axialTex = Load(Images + "axial/serie_cuerpo_axial_0133.jpg");
        Texture2D coronalTex = Load(Images + "coronal/serie_cuerpo_coronal_0256.jpg");
        Texture oldAxial = axial.Display.texture, oldCoronal = coronal.Display.texture;
        axial.Display.texture = axialTex;
        coronal.Display.texture = coronalTex;

        sb.AppendLine(string.Format("mm por pixel: axial {0:F3}, coronal {1:F3}",
            axial.MillimetresPerPixel, coronal.MillimetresPerPixel));

        MethodInfo place = typeof(CTMeasure2D).GetMethod("Place", BindingFlags.Instance | BindingFlags.NonPublic);

        // Axial: de un lado al otro del abdomen, 200 píxeles en horizontal.
        place.Invoke(tool, new object[] { axial, new Vector2(156f, 300f) });
        place.Invoke(tool, new object[] { axial, new Vector2(356f, 300f) });

        // Coronal: 200 píxeles en vertical, por el centro del cuerpo.
        place.Invoke(tool, new object[] { coronal, new Vector2(272f, 160f) });
        place.Invoke(tool, new object[] { coronal, new Vector2(272f, 360f) });

        Report(sb, "axial", axial, "195,3 mm -> 19,5 cm");
        Report(sb, "coronal", coronal, "341,0 mm -> 34,1 cm");

        // Cambiar de corte esconde la medida; volver la enseña.
        Transform caliper = axial.Display.transform.Find("Medida_2D");
        MethodInfo changed = typeof(CTMeasure2D).GetMethod("OnSliceChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        int original = axial.Slice;

        axial.SetSliceWithoutNotify(original + 10);
        changed.Invoke(tool, new object[] { axial, axial.Slice });
        bool hiddenElsewhere = caliper != null && !caliper.gameObject.activeSelf;

        axial.SetSliceWithoutNotify(original);
        changed.Invoke(tool, new object[] { axial, axial.Slice });
        bool backOnItsSlice = caliper != null && caliper.gameObject.activeSelf;

        sb.AppendLine("en otro corte se esconde: " + (hiddenElsewhere ? "SI" : "NO"));
        sb.AppendLine("al volver a su corte reaparece: " + (backOnItsSlice ? "SI" : "NO"));

        // En el editor no corre LateUpdate: se reparte el layout y se ajustan las líneas a mano.
        Canvas.ForceUpdateCanvases();
        typeof(CTMeasure2D).GetMethod("RefreshLines", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(tool, null);
        var lineRect = caliper != null ? caliper.Find("Linea") as RectTransform : null;
        sb.AppendLine(string.Format("linea del axial: {0:F0} px de largo (la imagen mide {1:F0} px de ancho)",
            lineRect != null ? lineRect.sizeDelta.x : -1f, axial.Display.rectTransform.rect.width));

        Shoot(axial.Display.canvas.transform, OutDir + "step99_regla2d.png");
        sb.AppendLine("foto: " + OutDir + "step99_regla2d.png");

        // Dejarlo todo como estaba.
        typeof(CTMeasure2D).GetMethod("Clear").Invoke(tool, null);
        axial.Display.texture = oldAxial;
        coronal.Display.texture = oldCoronal;
        axial.FieldOfViewMm = coronal.FieldOfViewMm = Vector2.zero;
        Object.DestroyImmediate(axialTex);
        Object.DestroyImmediate(coronalTex);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step99_regla2d.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP99_DONE");
    }

    private static void Report(StringBuilder sb, string name, CTPlaneView plane, string expected)
    {
        Transform caliper = plane.Display.transform.Find("Medida_2D");
        var text = caliper != null ? caliper.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        sb.AppendLine(string.Format("{0}: cuelga de la imagen = {1} | marca '{2}' | esperado {3}",
            name, caliper != null ? "SI" : "NO", text != null ? text.text : "(nada)", expected));
    }

    private static Texture2D Load(string path)
    {
        var tex = new Texture2D(2, 2);
        tex.LoadImage(System.IO.File.ReadAllBytes(path));
        return tex;
    }

    /// <summary>Foto de frente al visor DICOM.</summary>
    private static void Shoot(Transform canvas, string path)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

        bool wasActive = canvas.gameObject.activeSelf;
        canvas.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();

        var rect = canvas.GetComponent<RectTransform>();
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        Vector3 center = (corners[0] + corners[2]) * 0.5f;
        float width = Vector3.Distance(corners[0], corners[3]);

        var camGo = new GameObject("TmpRulerCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 10f;

        // Solo la interfaz: el volumen y los órganos quedan por delante desde este ángulo.
        cam.cullingMask = 1 << canvas.gameObject.layer;

        // La cara visible de un lienzo mira hacia -forward: la cámara va por ese lado.
        cam.transform.position = center - canvas.forward * (width * 0.5f / Mathf.Tan(20f * Mathf.Deg2Rad) * 0.62f);
        cam.transform.LookAt(center, canvas.up);

        const int w = 1800, h = 760;
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

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);
        canvas.gameObject.SetActive(wasActive);
    }
}
