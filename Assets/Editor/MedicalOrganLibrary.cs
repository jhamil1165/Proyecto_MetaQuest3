using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Coloca en la escena los órganos segmentados con 3D Slicer, cada uno equipado
/// igual que los anteriores: collider, agarre, ficha de información y plataforma.
///
/// Los modelos vienen a escala de la tomografía (milímetros o unidades arbitrarias),
/// así que cada uno se normaliza a un tamaño objetivo medido por sus bounds reales,
/// no por un factor inventado.
/// </summary>
public static class MedicalOrganLibrary
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string ModelDir = "Assets/Models/Organs";

    private const float ArcRadius = 1.30f;
    private const float ArcHeight = 1.15f;
    private const float TargetSize = 0.20f;   // dimensión mayor de cada órgano, en metros

    private struct OrganDef
    {
        public string file;
        public string display;
        public Color dot;
        public float angle;
    }

    [MenuItem("MedicalViewer/Step26 - Place Slicer Organs")]
    public static void PlaceOrgans()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye == null)
        {
            Debug.LogError("STEP26_FAILED CenterEyeAnchor no encontrado");
            return;
        }
        Transform eye = centerEye.transform;

        // El estómago viejo traía cámara, luz, un cubo de 1 mm y malla duplicada.
        // El nuevo del paquete es el mismo órgano, limpio y 11 veces más ligero.
        GameObject oldStomach = GameObject.Find("estomago_sin_render");
        if (oldStomach != null && oldStomach.activeSelf)
        {
            oldStomach.SetActive(false);
            sb.AppendLine("estomago_sin_render (antiguo, 2.8 MB) -> DESACTIVADO, reemplazado por el limpio");
        }

        var defs = new OrganDef[]
        {
            new OrganDef { file = "higado",   display = "Hígado",   dot = new Color(0.60f, 0.22f, 0.20f), angle = -24f },
            new OrganDef { file = "estomago", display = "Estómago", dot = new Color(0.80f, 0.64f, 0.42f), angle = -8f },
            new OrganDef { file = "pancreas", display = "Páncreas", dot = new Color(0.85f, 0.72f, 0.35f), angle = 8f },
            new OrganDef { file = "vesicula", display = "Vesícula", dot = new Color(0.36f, 0.60f, 0.36f), angle = 24f },
        };

        GameObject parent = GameObject.Find("Organs_Slicer");
        if (parent == null)
        {
            parent = new GameObject("Organs_Slicer");
            Undo.RegisterCreatedObjectUndo(parent, "Create Organs_Slicer");
        }

        foreach (var def in defs)
        {
            Place(def, parent.transform, eye, sb);
        }

        // El corazón existente se recoloca en el mismo arco para que el conjunto sea coherente.
        GameObject heart = GameObject.Find("Heart");
        if (heart != null)
        {
            Reposition(heart.transform, eye, -40f, sb, "Heart");
        }

        EditorUtility.SetDirty(parent);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step26_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP26_DONE");
    }

    private static void Place(OrganDef def, Transform parent, Transform eye, StringBuilder sb)
    {
        string path = $"{ModelDir}/{def.file}.fbx";
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model == null)
        {
            sb.AppendLine($"[WARN] no se pudo cargar {path}");
            return;
        }

        // Reconstruir si ya existe, para que el paso sea repetible.
        Transform existing = parent.Find(def.display);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        go.name = def.display;
        Undo.RegisterCreatedObjectUndo(go, "Place organ");

        if (!TryBounds(go.transform, out Bounds raw))
        {
            sb.AppendLine($"[WARN] {def.display} sin renderers");
            return;
        }

        float maxDim = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
        if (maxDim < 1e-6f)
        {
            sb.AppendLine($"[WARN] {def.display} con bounds degenerados");
            return;
        }

        float factor = TargetSize / maxDim;
        go.transform.localScale *= factor;

        Reposition(go.transform, eye, def.angle, sb, def.display);

        // Equipamiento: igual que los órganos que ya estaban.
        MeshFilter mf = go.GetComponent<MeshFilter>();
        if (mf == null) mf = go.GetComponentInChildren<MeshFilter>();
        GameObject host = mf != null ? mf.gameObject : go;

        if (host.GetComponent<MeshCollider>() == null)
        {
            var mc = Undo.AddComponent<MeshCollider>(host);
            if (mf != null) mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
        }

        if (host.GetComponent<XRGrabInteractable>() == null)
        {
            var grab = Undo.AddComponent<XRGrabInteractable>(host);
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;

            var rb = host.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        var info = host.GetComponent<OrganSelectionInfo>();
        if (info == null) info = Undo.AddComponent<OrganSelectionInfo>(host);

        var so = new SerializedObject(info);
        so.FindProperty("organName").stringValue = def.display.ToUpperInvariant();
        so.FindProperty("organInfo").stringValue =
            $"{def.display}\nSegmentado de TC con 3D Slicer.\n(Completar con datos médicos reales.)";
        so.ApplyModifiedPropertiesWithoutUndo();

        AppendOutline(host, sb);
        AddPlatform(go, def.display, sb);

        TryBounds(go.transform, out Bounds final);
        int tris = 0;
        foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
            if (f.sharedMesh != null) tris += f.sharedMesh.triangles.Length / 3;

        sb.AppendLine($"{def.display}: {tris:N0} tris | tamaño {final.size:F3} m | escala x{factor:F5} | ángulo {def.angle}°");
    }

    private static void Reposition(Transform t, Transform eye, float angle, StringBuilder sb, string name)
    {
        if (!TryBounds(t, out Bounds b)) return;

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        Vector3 target = eye.position
                         + (eye.rotation * yaw * Vector3.forward) * ArcRadius
                         + Vector3.up * ArcHeight;

        // Se mueve por el centro de los bounds, no por el pivote: los modelos de
        // Slicer tienen el pivote en el origen del volumen, lejos de la malla.
        Vector3 pivotToCentre = b.center - t.position;
        t.position = target - pivotToCentre;
    }

    private static void AppendOutline(GameObject host, StringBuilder sb)
    {
        Material outline = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloOutline.mat");
        if (outline == null || !host.TryGetComponent(out Renderer r)) return;

        foreach (var m in r.sharedMaterials)
        {
            if (m != null && m.shader != null && m.shader.name == "MedicalViewer/HoloOutline") return;
        }

        var list = new System.Collections.Generic.List<Material>(r.sharedMaterials) { outline };
        r.sharedMaterials = list.ToArray();
    }

    private static void AddPlatform(GameObject organ, string name, StringBuilder sb)
    {
        Material platformMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloPlatform.mat");
        if (platformMat == null) return;
        if (!TryBounds(organ.transform, out Bounds b)) return;

        var platform = GameObject.CreatePrimitive(PrimitiveType.Quad);
        platform.name = $"Holo_Platform_{name}";
        Object.DestroyImmediate(platform.GetComponent<Collider>());

        if (platform.TryGetComponent(out Renderer pr))
        {
            pr.sharedMaterial = platformMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
        }

        platform.transform.SetParent(organ.transform, false);
        platform.transform.position = new Vector3(b.center.x, b.min.y - 0.015f, b.center.z);
        platform.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float diameter = Mathf.Max(b.size.x, b.size.z) * 2.2f;
        float parentScale = organ.transform.lossyScale.x;
        if (Mathf.Abs(parentScale) < 1e-8f) parentScale = 1f;
        float local = diameter / parentScale;
        platform.transform.localScale = new Vector3(local, local, 1f);
    }

    private static bool TryBounds(Transform t, out Bounds bounds)
    {
        bounds = default;
        var renderers = t.GetComponentsInChildren<Renderer>();
        bool found = false;

        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return found;
    }
}
