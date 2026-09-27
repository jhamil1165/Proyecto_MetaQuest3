using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Monta los tres cortes cruzados que pidio el profesor, los que usan los neuronavegadores:
/// un corte axial, uno coronal y uno sagital atravesando el cuerpo por el mismo punto.
///
/// Reutiliza el shader del corte, que ya sabe dibujar la tomografia sobre un plano
/// cualquiera. Lo unico nuevo son los tres cuadrados y el punto donde se cruzan.
/// </summary>
public static class MedicalTriPlane
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string RootName = "Tres_Planos";
    private const string MatDir = "Assets/Materials/Volume";

    [MenuItem("MedicalViewer/Step94 - Tres planos cruzados (neuronavegador)")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject volumeGo = GameObject.Find("Volumen_3D");
        if (volumeGo == null)
        {
            Debug.LogError("[Step94] No encuentro Volumen_3D.");
            return;
        }

        var volume = volumeGo.GetComponent<Renderer>();
        Material sliceMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Corte_TAC.mat");
        if (sliceMat == null)
        {
            Debug.LogError("[Step94] Falta Mat_Corte_TAC: ejecuta antes el Step77.");
            return;
        }

        // El grupo cuelga del mismo padre que el volumen: colgarlo del volumen lo aplastaria,
        // porque el volumen esta escalado 0,50 x 0,87 x 0,50.
        Transform parent = volumeGo.transform.parent;
        GameObject root = Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            sb.AppendLine("grupo creado");
        }
        if (parent != null) root.transform.SetParent(parent, false);

        Renderer axial = Plane(root.transform, "Corte_Axial", sliceMat);
        Renderer coronal = Plane(root.transform, "Corte_Coronal", sliceMat);
        Renderer sagittal = Plane(root.transform, "Corte_Sagital", sliceMat);

        Transform focus = Focus(root.transform, sb);

        var view = root.GetComponent<TriPlaneView>();
        if (view == null) view = root.AddComponent<TriPlaneView>();

        var so = new SerializedObject(view);
        so.FindProperty("volume").objectReferenceValue = volume;
        so.FindProperty("focus").objectReferenceValue = focus;
        so.FindProperty("axial").objectReferenceValue = axial;
        so.FindProperty("coronal").objectReferenceValue = coronal;
        so.FindProperty("sagittal").objectReferenceValue = sagittal;
        so.FindProperty("visible").boolValue = false;   // se enciende desde el menu
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);

        axial.enabled = coronal.enabled = sagittal.enabled = false;
        focus.gameObject.SetActive(false);

        sb.AppendLine("tres cortes listos: axial, coronal y sagital, cruzandose en un punto");
        sb.AppendLine("tamaño de cada corte: " + (volume.bounds.size.magnitude * 1.02f).ToString("F2") + " m");

        // ---- que el menu lo encienda con la vista Modelo 3D ----
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var aso = new SerializedObject(actions);
            var group = aso.FindProperty("model3DObjects");

            bool already = false;
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue == root) already = true;
            }

            if (!already)
            {
                group.arraySize += 1;
                group.GetArrayElementAtIndex(group.arraySize - 1).objectReferenceValue = root;
                aso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(actions);
            }

            sb.AppendLine("vista Modelo 3D: incluye los tres planos" + (already ? " (ya estaba)" : ""));
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step94_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP94_DONE");
    }

    private static Renderer Plane(Transform parent, string name, Material material)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);

        // Sin collider: si no, el rayo del mando chocaria con los cortes en vez de con los
        // organos que hay detras.
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        EditorUtility.SetDirty(go);
        return renderer;
    }

    /// <summary>La bolita del punto de cruce: se ve y se agarra.</summary>
    private static Transform Focus(Transform parent, StringBuilder sb)
    {
        Transform existing = parent.Find("Punto_de_cruce");
        GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Punto_de_cruce";
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.035f;

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = FocusMaterial();

        // Se agarra con la mano para recorrer la anatomia.
        var body = go.GetComponent<Rigidbody>();
        if (body == null) body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.trackRotation = false;   // solo importa donde esta, no como esta girado

        EditorUtility.SetDirty(go);
        sb.AppendLine("punto de cruce: 3,5 cm, agarrable, se queda dentro del cuerpo");
        return go.transform;
    }

    private static Material FocusMaterial()
    {
        string path = MatDir + "/Mat_Punto_Cruce.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        var color = (Color)new Color32(0x4C, 0x8D, 0xFF, 255);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject Find(string name)
    {
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t.name == name) return t.gameObject;
        }
        return null;
    }
}
