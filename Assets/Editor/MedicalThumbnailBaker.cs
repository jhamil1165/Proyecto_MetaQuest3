using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Renderiza una miniatura PNG de cada órgano para usarla en las filas del menú.
///
/// Este paso necesita GPU, así que se ejecuta en batch mode SIN -nographics, a
/// diferencia del resto de las herramientas. El objeto se aísla en una capa propia
/// para que no salgan otros objetos de la escena en la foto.
/// </summary>
public static class MedicalThumbnailBaker
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string ThumbDir = "Assets/UI/Thumbnails";
    private const int IsolationLayer = 31;
    private const int Size = 256;

    [MenuItem("MedicalViewer/Step21 - Bake Organ Thumbnails")]
    public static void BakeThumbnails()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        System.IO.Directory.CreateDirectory(ThumbDir);

        Bake("Heart", "Heart", sb);
        Bake("Hígado", "Higado", sb);
        Bake("Estómago", "Estomago", sb);
        Bake("Páncreas", "Pancreas", sb);
        Bake("Vesícula", "Vesicula", sb);

        AssetDatabase.Refresh();

        System.IO.File.WriteAllText(OutDir + "step21_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP21_DONE");
    }

    private static void Bake(string objectName, string fileName, StringBuilder sb)
    {
        GameObject target = GameObject.Find(objectName);
        if (target == null)
        {
            sb.AppendLine($"[WARN] {objectName} no encontrado");
            return;
        }

        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            sb.AppendLine($"[WARN] {objectName} sin renderers");
            return;
        }

        var hiddenPlatforms = new System.Collections.Generic.List<GameObject>();
        foreach (var t in target.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Holo_Platform_") || !t.gameObject.activeSelf) continue;
            t.gameObject.SetActive(false);
            hiddenPlatforms.Add(t.gameObject);
        }

        renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            foreach (var g in hiddenPlatforms) g.SetActive(true);
            sb.AppendLine($"[WARN] {objectName} sin renderers visibles");
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // Aislar el objeto en una capa propia para que la foto salga limpia.
        var all = target.GetComponentsInChildren<Transform>(true);
        var originalLayers = new int[all.Length];
        for (int i = 0; i < all.Length; i++)
        {
            originalLayers[i] = all[i].gameObject.layer;
            all[i].gameObject.layer = IsolationLayer;
        }

        var camGo = new GameObject("__ThumbCam");
        var cam = camGo.AddComponent<Camera>();
        cam.cullingMask = 1 << IsolationLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // fondo transparente
        cam.orthographic = true;

        float extent = Mathf.Max(bounds.extents.x, bounds.extents.y);
        cam.orthographicSize = extent * 1.25f; // algo de aire alrededor
        cam.nearClipPlane = 0.001f;
        cam.farClipPlane = bounds.size.magnitude * 10f + 10f;

        // Vista de tres cuartos, que lee mejor que una frontal plana.
        Vector3 dir = (Quaternion.Euler(15f, -30f, 0f) * Vector3.back).normalized;
        camGo.transform.position = bounds.center + dir * (bounds.size.magnitude * 2f + 1f);
        camGo.transform.LookAt(bounds.center);

        var lightGo = new GameObject("__ThumbLight");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightGo.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
        lightGo.layer = IsolationLayer;

        var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        string path = $"{ThumbDir}/Thumb_{fileName}.png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());

        // Limpieza
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(camGo);
        Object.DestroyImmediate(lightGo);
        for (int i = 0; i < all.Length; i++) all[i].gameObject.layer = originalLayers[i];
        foreach (var g in hiddenPlatforms) g.SetActive(true);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        sb.AppendLine($"{objectName} -> {path} (bounds {bounds.size:F3})");
    }
}
