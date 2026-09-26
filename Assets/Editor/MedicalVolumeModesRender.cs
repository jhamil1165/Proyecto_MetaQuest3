using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fotografía el volumen 3D en sus tres modos (tejido, esqueleto y solo órganos) para
/// revisar el resultado sin abrir Unity. No cambia la escena: los valores se mandan con
/// un MaterialPropertyBlock y se dejan como estaban al terminar.
/// </summary>
public static class MedicalVolumeModesRender
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private struct Mode
    {
        public string Name;
        public float Center, Width, Tissue, Organ, Bone, Steps;

        public Mode(string name, float center, float width, float tissue, float organ, float bone, float steps = 160f)
        {
            Name = name; Center = center; Width = width; Tissue = tissue; Organ = organ; Bone = bone; Steps = steps;
        }
    }

    private static readonly Mode[] Modes =
    {
        new Mode("tejido", 40f, 500f, 0.12f, 0.85f, 0f),
        new Mode("esqueleto", 300f, 1200f, 0.03f, 0.25f, 0.95f),
        new Mode("organos", 40f, 400f, 0.03f, 0.95f, 0f),
    };

    [MenuItem("MedicalViewer/Step74 - Fotos de los modos del volumen")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject volume = GameObject.Find("Volumen_3D");
        if (volume == null)
        {
            Debug.LogError("[Step74] No encuentro Volumen_3D.");
            return;
        }

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[Step74] Hace falta GPU: ejecutar sin -nographics.");
            return;
        }

        bool wasActive = volume.activeSelf;
        volume.SetActive(true);

        var renderer = volume.GetComponent<Renderer>();
        var mpb = new MaterialPropertyBlock();

        Bounds bounds = renderer.bounds;
        var camGo = new GameObject("TmpModesCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 20f;

        // De frente al paciente, un poco por encima.
        Vector3 forward = volume.transform.forward;
        cam.transform.position = bounds.center - forward * (bounds.size.y * 1.35f) + Vector3.up * bounds.extents.y * 0.15f;
        cam.transform.LookAt(bounds.center);

        const int w = 700;
        const int h = 1100;
        cam.aspect = (float)w / h;

        foreach (var mode in Modes)
        {
            renderer.GetPropertyBlock(mpb);
            mpb.SetFloat("_WindowCenter", mode.Center);
            mpb.SetFloat("_WindowWidth", mode.Width);
            mpb.SetFloat("_TissueOpacity", mode.Tissue);
            mpb.SetFloat("_OrganOpacity", mode.Organ);
            mpb.SetFloat("_BoneOpacity", mode.Bone);
            mpb.SetFloat("_Steps", mode.Steps);
            renderer.SetPropertyBlock(mpb);

            var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = target;
            cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            string path = OutDir + "step74_" + mode.Name + ".png";
            System.IO.Directory.CreateDirectory(OutDir);
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

            cam.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            sb.AppendLine("render " + mode.Name + ": " + path);
        }

        // Dejar el volumen como estaba.
        renderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_WindowCenter", Modes[0].Center);
        mpb.SetFloat("_WindowWidth", Modes[0].Width);
        mpb.SetFloat("_TissueOpacity", Modes[0].Tissue);
        mpb.SetFloat("_OrganOpacity", Modes[0].Organ);
        mpb.SetFloat("_BoneOpacity", Modes[0].Bone);
        renderer.SetPropertyBlock(mpb);

        Object.DestroyImmediate(camGo);

        WideShot(volume, sb);

        volume.SetActive(wasActive);

        System.IO.File.WriteAllText(OutDir + "step74_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP74_DONE");
    }

    /// <summary>Lo que ve el usuario al arrancar, con el esqueleto puesto.</summary>
    private static void WideShot(GameObject volume, StringBuilder sb)
    {
        GameObject root = GameObject.Find("Medical_Menu_UI");
        if (root == null) return;

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        var restore = new System.Collections.Generic.List<(GameObject go, bool active)>();
        if (actions != null)
        {
            var group = new SerializedObject(actions).FindProperty("model3DObjects");
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)
                {
                    restore.Add((go, go.activeSelf));
                    go.SetActive(true);
                }
            }
        }

        var renderer = volume.GetComponent<Renderer>();
        var mpb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(mpb);
        mpb.SetFloat("_WindowCenter", Modes[1].Center);
        mpb.SetFloat("_WindowWidth", Modes[1].Width);
        mpb.SetFloat("_TissueOpacity", Modes[1].Tissue);
        mpb.SetFloat("_OrganOpacity", Modes[1].Organ);
        mpb.SetFloat("_BoneOpacity", Modes[1].Bone);
        mpb.SetFloat("_Steps", Modes[1].Steps);
        renderer.SetPropertyBlock(mpb);

        var camGo = new GameObject("TmpWideCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;
        cam.fieldOfView = 75f;
        cam.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);

        const int w = 2000;
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

        string path = OutDir + "step74_vista.png";
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);

        for (int i = restore.Count - 1; i >= 0; i--) restore[i].go.SetActive(restore[i].active);
        sb.AppendLine("render vista del usuario: " + path);
    }
}
