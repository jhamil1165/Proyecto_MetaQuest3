using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Arreglos de la primera prueba en el visor de verdad (17/10/2026):
///
///  1. El rayo de las manos salía de otro sitio. El rig está girado 180° y desplazado, pero
///     OVRHand coloca su PointerPose en coordenadas del espacio de seguimiento sin padre, es
///     decir, como si el rig estuviera en el origen. Los rayos se cruzaban en X y la
///     selección con la mano (sobre todo la izquierda) no acertaba nada. Se le da como raíz
///     el TrackingSpace.
///  2. Las manos negras encima de las manos reales: con el passthrough ya se ven las manos
///     de verdad, así que el modelo de mano sobra. Se sigue rastreando; solo no se dibuja.
///  3. El láser se cambia por un círculo donde apunta: la línea se hace invisible y cada
///     rayo lleva una retícula.
///  4. El mando "caído" al volver a cogerlo: ControllerPoseFollower en cada anclaje de mando.
///  5. Tirones en la vista Modelo 3D: renderizado foveado y menos pasos en el volumen.
/// </summary>
public static class MedicalHeadsetFixes
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string ReticlePrefab = "Assets/Prefabs/Reticula_Rayo.prefab";
    private const string ReticleMaterial = "Assets/Materials/Mat_Reticula.mat";
    private const string VolumeMaterial = "Assets/Materials/Volume/Mat_Volumen.mat";
    private const int VolumeSteps = 96;

    [MenuItem("MedicalViewer/Step102 - Arreglos de la prueba en el visor")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var rig = Object.FindObjectOfType<OVRCameraRig>(true);
        Transform trackingSpace = rig.transform.Find("TrackingSpace");

        // ---- 1 y 2: manos ----
        foreach (var hand in Object.FindObjectsOfType<OVRHand>(true))
        {
            var so = new SerializedObject(hand);
            so.FindProperty("_pointerPoseRoot").objectReferenceValue = trackingSpace;
            so.ApplyModifiedPropertiesWithoutUndo();

            var meshRenderer = hand.GetComponent<OVRMeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = false;
            var skinned = hand.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null) skinned.enabled = false;
            var skeletonRenderer = hand.GetComponent<OVRSkeletonRenderer>();
            if (skeletonRenderer != null) skeletonRenderer.enabled = false;

            EditorUtility.SetDirty(hand.gameObject);
            sb.AppendLine("mano " + hand.name + ": rayo desde TrackingSpace, modelo oculto");
        }

        // ---- 3: retícula en vez de láser ----
        GameObject reticle = BuildReticle(sb);
        var clear = new Gradient();
        clear.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0f, 1f) });

        foreach (var visual in Object.FindObjectsOfType<XRInteractorLineVisual>(true))
        {
            if (Branch(visual.transform).ToUpperInvariant().Contains("ORPHAN")) continue;

            visual.reticle = reticle;
            visual.lineWidth = 0f;
            visual.validColorGradient = clear;
            visual.invalidColorGradient = clear;
            visual.blockedColorGradient = clear;
            EditorUtility.SetDirty(visual);
            sb.AppendLine("rayo sin laser y con circulo: " + Branch(visual.transform));
        }

        // ---- 4: mandos en su sitio ----
        foreach (var pair in new[] { ("LeftControllerAnchor", OVRInput.Controller.LTouch),
                                     ("RightControllerAnchor", OVRInput.Controller.RTouch) })
        {
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != pair.Item1 || Branch(t).ToUpperInvariant().Contains("ORPHAN")) continue;

                var follower = t.GetComponent<ControllerPoseFollower>();
                if (follower == null) follower = t.gameObject.AddComponent<ControllerPoseFollower>();
                var so = new SerializedObject(follower);
                so.FindProperty("controller").intValue = (int)pair.Item2;
                so.FindProperty("rig").objectReferenceValue = rig;
                so.ApplyModifiedPropertiesWithoutUndo();
                sb.AppendLine("mando en su pose real: " + Branch(t));
            }
        }

        // ---- 5: rendimiento ----
        if (rig.GetComponent<QuestPerformance>() == null) rig.gameObject.AddComponent<QuestPerformance>();
        sb.AppendLine("renderizado foveado alto y dinamico al arrancar");

        var volume = AssetDatabase.LoadAssetAtPath<Material>(VolumeMaterial);
        if (volume != null)
        {
            sb.AppendLine(string.Format("volumen: pasos {0} -> {1}", volume.GetFloat("_Steps"), VolumeSteps));
            volume.SetFloat("_Steps", VolumeSteps);
            EditorUtility.SetDirty(volume);
        }

        AssetDatabase.SaveAssets();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step102_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP102_DONE");
    }

    /// <summary>Quad con el sombreador del aro, sin collider (el rayo no debe chocar con él).</summary>
    private static GameObject BuildReticle(StringBuilder sb)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ReticleMaterial);
        if (material == null)
        {
            material = new Material(Shader.Find("MedicalViewer/Reticle"));
            AssetDatabase.CreateAsset(material, ReticleMaterial);
        }
        material.SetColor("_Color", new Color(1f, 1f, 1f, 0.95f));
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Reticula_Rayo";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        go.AddComponent<ReticleScaler>();

        System.IO.Directory.CreateDirectory("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, ReticlePrefab);
        Object.DestroyImmediate(go);
        sb.AppendLine("reticula: " + ReticlePrefab);
        return prefab;
    }

    private static string Branch(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
