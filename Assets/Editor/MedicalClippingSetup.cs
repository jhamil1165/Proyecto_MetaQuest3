using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Parte 4: pone en la escena el efecto de corte que entregó Adriana-28.
///
/// Su shader y su script estaban en el proyecto pero en ninguna escena, así que no se
/// podían ver funcionando. Aquí:
///   - cada órgano pasa a usar un material con el shader "APOSE/URP_ClippingPlane",
///     conservando su color y con una "tapa" de un tono más oscuro en el corte;
///   - se crea un plano que se agarra con la mano (XRGrabInteractable) y que define
///     dónde corta;
///   - el plano se añade a las vistas Modelo 3D y Segmentación del menú.
///
/// El plano se coloca solo en el centro de los órganos al abrirse la vista, porque
/// WorkspaceRecenter los mueve al arrancar según dónde mire el usuario.
/// </summary>
public static class MedicalClippingSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string MatDir = "Assets/Materials/Clipping";
    private const string PlaneName = "Plano_de_corte";

    [MenuItem("MedicalViewer/Step67 - Plano de corte sobre los organos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Shader clipShader = Shader.Find("APOSE/URP_ClippingPlane");
        if (clipShader == null)
        {
            Debug.LogError("[Step67] No encuentro el shader APOSE/URP_ClippingPlane.");
            return;
        }

        var systems = Object.FindObjectOfType<OrganSystemsPanel>(true);
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (systems == null || actions == null)
        {
            Debug.LogError("[Step67] Falta OrganSystemsPanel o MedicalMenuActions.");
            return;
        }

        System.IO.Directory.CreateDirectory(MatDir);

        // ---- 1. Materiales con el shader de corte, conservando el color de cada órgano ----
        var organs = new List<GameObject>();
        var targets = new List<Renderer>();
        var rows = new SerializedObject(systems).FindProperty("rows");

        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var organ = row.FindPropertyRelative("target").objectReferenceValue as GameObject;
            string label = row.FindPropertyRelative("label").stringValue;
            if (organ == null) continue;

            organs.Add(organ);

            // La plataforma de debajo del órgano no se corta, y sobre todo no comparte
            // material: si lo comparte, su gris machaca el color del órgano.
            Renderer main = null;
            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(true))
            {
                if (IsPlatform(renderer)) continue;
                if (main == null) main = renderer;
            }

            if (main == null)
            {
                sb.AppendLine("[AVISO] " + label + " no tiene malla propia");
                continue;
            }

            Material material = ClipMaterial(clipShader, organ.name, main.sharedMaterial);
            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(true))
            {
                if (IsPlatform(renderer)) continue;
                renderer.sharedMaterial = material;
                EditorUtility.SetDirty(renderer);
                targets.Add(renderer);
            }

            sb.AppendLine("organo " + label + " -> " + material.name + ", color " +
                          material.GetColor("_BaseColor").ToString("F2") +
                          (material.GetTexture("_BaseMap") != null ? " con textura" : ""));
        }

        // ---- 2. El plano que se agarra con la mano ----
        GameObject plane = GameObject.Find(PlaneName);
        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.name = PlaneName;
            sb.AppendLine(PlaneName + ": creado");
        }
        else
        {
            sb.AppendLine(PlaneName + ": ya existia, se reconfigura");
        }

        // Una lámina fina y traslúcida: se ve dónde corta sin tapar el órgano.
        plane.transform.localScale = new Vector3(0.45f, 0.006f, 0.45f);
        plane.GetComponent<Renderer>().sharedMaterial = PlaneMaterial();

        var body = GetOrAdd<Rigidbody>(plane);
        body.isKinematic = true;
        body.useGravity = false;

        var grab = GetOrAdd<XRGrabInteractable>(plane);
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        var controller = GetOrAdd<ClippingPlaneController>(plane);
        var cso = new SerializedObject(controller);
        cso.FindProperty("planeHandle").objectReferenceValue = plane.transform;
        var list = cso.FindProperty("targets");
        list.arraySize = targets.Count;
        for (int i = 0; i < targets.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
        cso.FindProperty("clippingEnabled").boolValue = true;
        cso.ApplyModifiedPropertiesWithoutUndo();

        var anchor = GetOrAdd<ClippingPlaneAnchor>(plane);
        var aso = new SerializedObject(anchor);
        var organList = aso.FindProperty("organs");
        organList.arraySize = organs.Count;
        for (int i = 0; i < organs.Count; i++) organList.GetArrayElementAtIndex(i).objectReferenceValue = organs[i];
        aso.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(plane);
        sb.AppendLine("plano: corta " + targets.Count + " renderers de " + organs.Count + " organos");

        // Centrado en los órganos también fuera de Play, para verlo en la escena.
        Bounds bounds = default;
        bool found = false;
        foreach (var organ in organs)
        {
            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(true))
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
        if (found)
        {
            plane.transform.position = bounds.center;
            plane.transform.rotation = Quaternion.identity;
            sb.AppendLine("plano colocado en " + bounds.center.ToString("F2"));
        }

        // ---- 3. El menú lo enciende con los órganos ----
        var mso = new SerializedObject(actions);
        foreach (string field in new[] { "model3DObjects", "segmentationObjects" })
        {
            var group = mso.FindProperty(field);
            bool already = false;
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue == plane) already = true;
            }
            if (!already)
            {
                group.arraySize += 1;
                group.GetArrayElementAtIndex(group.arraySize - 1).objectReferenceValue = plane;
            }
            sb.AppendLine(field + ": " + group.arraySize + " objetos" + (already ? " (el plano ya estaba)" : " (plano anadido)"));
        }
        mso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        RenderCheck(plane, organs, sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step67_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP67_DONE");
    }

    private static Color ColorOf(Material material)
    {
        if (material == null) return Color.white;
        if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color")) return material.GetColor("_Color");
        return Color.white;
    }

    /// <summary>La plataforma holográfica que hay bajo cada órgano, que no se corta.</summary>
    private static bool IsPlatform(Renderer renderer)
    {
        return renderer.gameObject.name.StartsWith("Holo_Platform");
    }

    private static Material ClipMaterial(Shader shader, string organ, Material source)
    {
        string path = MatDir + "/Mat_Clip_" + organ + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        Color color = ColorOf(source);

        material.shader = shader;
        material.SetColor("_BaseColor", color);

        // El corazón lleva textura en vez de color plano: hay que copiarla.
        if (source != null && source.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", source.GetTexture("_BaseMap"));
        }
        // La "tapa" del corte, un tono más oscuro del propio órgano: parece tejido cortado.
        material.SetColor("_CapColor", new Color(color.r * 0.55f, color.g * 0.45f, color.b * 0.45f, 1f));

        // Sin cortar mientras no haya plano: el valor por defecto del shader (1000) deja
        // todo el organo del lado descartado y no se ve nada fuera de Play.
        material.SetVector("_PlaneNormal", new Vector4(0f, 1f, 0f, 0f));
        material.SetFloat("_PlaneDistance", -100000f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material PlaneMaterial()
    {
        string path = MatDir + "/Mat_PlanoCorte.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        // Transparente: en URP no basta con poner alfa, hay que configurar la mezcla.
        material.SetColor("_BaseColor", new Color(0.25f, 0.75f, 1f, 0.22f));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_ZWrite", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    /// <summary>Foto de los órganos con el plano, para revisar el corte sin abrir Unity.</summary>
    private static void RenderCheck(GameObject plane, List<GameObject> organs, StringBuilder sb)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            sb.AppendLine("render: sin GPU, no se genera imagen");
            return;
        }

        // Los organos suelen estar apagados en la escena (los enciende el menu en Play):
        // para la foto se encienden y despues se dejan como estaban.
        var restore = new List<(GameObject go, bool active)>();
        foreach (var organ in organs)
        {
            restore.Add((organ, organ.activeSelf));
            organ.SetActive(true);
        }
        restore.Add((plane, plane.activeSelf));
        plane.SetActive(true);

        Bounds bounds = default;
        bool found = false;
        foreach (var organ in organs)
        {
            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(true))
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }
        if (!found)
        {
            for (int i = restore.Count - 1; i >= 0; i--) restore[i].go.SetActive(restore[i].active);
            return;
        }

        // El plano corta por el centro de los organos, para que se vea el efecto.
        plane.transform.position = bounds.center;
        plane.transform.rotation = Quaternion.identity;

        // Camara del lado del usuario: mirando en la misma direccion que el menu.
        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        Vector3 forward = rootGo != null ? rootGo.transform.forward : Vector3.forward;
        float distance = bounds.extents.magnitude * 2.2f + 0.3f;

        var camGo = new GameObject("TmpClipCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.18f, 0.22f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = bounds.extents.magnitude * 0.85f;
        cam.transform.position = bounds.center - forward * distance + Vector3.up * bounds.extents.y * 0.6f;
        cam.transform.LookAt(bounds.center);
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 10f;

        // Para la foto se simula lo que hace el controlador en Play: el plano corta por su
        // posicion. Se usa un property block, que no se guarda en el material.
        var mpb = new MaterialPropertyBlock();
        var cut = new List<Renderer>();
        foreach (var organ in organs)
        {
            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(mpb);
                mpb.SetVector("_PlaneNormal", new Vector4(0f, 1f, 0f, 0f));
                mpb.SetFloat("_PlaneDistance", -plane.transform.position.y);
                renderer.SetPropertyBlock(mpb);
                cut.Add(renderer);
            }
        }

        const int w = 1400;
        const int h = 800;
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

        string path = OutDir + "step67_corte.png";
        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);

        foreach (var renderer in cut)
        {
            renderer.GetPropertyBlock(mpb);
            mpb.SetFloat("_PlaneDistance", -100000f);
            renderer.SetPropertyBlock(mpb);
        }

        for (int i = restore.Count - 1; i >= 0; i--) restore[i].go.SetActive(restore[i].active);

        sb.AppendLine("render: " + path);
    }
}
