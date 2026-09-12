using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Convierte los órganos en agarrables y añade hand tracking al rig.
///
/// Nota sobre las manos: esto las hace visibles y rastreadas, y habilita el permiso
/// en el manifiesto. Que además puedan pulsar la interfaz es otro asunto: los rayos
/// de interacción son de XRI y están atados a los mandos, mientras que OVRHand es de
/// Meta. Unir ambos requiere el Interaction SDK de Meta, que no se toca aquí.
/// </summary>
public static class MedicalGrabAndHands
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step25 - Grab Organs + Hand Tracking")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        MakeGrabbable("Heart", "Heart", sb);
        MakeGrabbable("estomago_sin_render", "Segmentation", sb);

        AddHands(sb);
        EnableHandTrackingInProjectConfig(sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step25_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP25_DONE");
    }

    private static void MakeGrabbable(string rootName, string hostName, StringBuilder sb)
    {
        GameObject root = GameObject.Find(rootName);
        if (root == null)
        {
            sb.AppendLine($"[WARN] {rootName} no encontrado");
            return;
        }

        Transform host = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == hostName) { host = t; break; }
        }
        if (host == null) host = root.transform;

        GameObject go = host.gameObject;

        var simple = go.GetComponent<XRSimpleInteractable>();
        if (simple != null)
        {
            Object.DestroyImmediate(simple);
            sb.AppendLine($"{go.name}: XRSimpleInteractable eliminado");
        }

        var grab = go.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = Undo.AddComponent<XRGrabInteractable>(go);
            sb.AppendLine($"{go.name}: XRGrabInteractable añadido");
        }

        // Un órgano no debe caerse ni salir volando al soltarlo: es un modelo de
        // estudio, no un objeto físico.
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true; // se agarra desde donde apuntas, no desde su pivote

        var rb = go.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            EditorUtility.SetDirty(rb);
        }

        EditorUtility.SetDirty(go);
        sb.AppendLine($"{go.name}: agarrable (kinematic, sin gravedad, sin lanzamiento)");
    }

    private static void AddHands(StringBuilder sb)
    {
        const string prefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            sb.AppendLine($"[WARN] no se encontró {prefabPath}");
            return;
        }

        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig == null)
        {
            sb.AppendLine("[WARN] Camera Rig no encontrado");
            return;
        }

        Transform trackingSpace = rig.transform.Find("TrackingSpace");
        if (trackingSpace == null)
        {
            sb.AppendLine("[WARN] TrackingSpace no encontrado");
            return;
        }

        AttachHand(trackingSpace, "LeftHandAnchor", OVRHand.Hand.HandLeft,
            OVRSkeleton.SkeletonType.HandLeft, OVRMesh.MeshType.HandLeft, sb);
        AttachHand(trackingSpace, "RightHandAnchor", OVRHand.Hand.HandRight,
            OVRSkeleton.SkeletonType.HandRight, OVRMesh.MeshType.HandRight, sb);
    }

    private static void AttachHand(Transform trackingSpace, string anchorName, OVRHand.Hand handType,
        OVRSkeleton.SkeletonType skeletonType, OVRMesh.MeshType meshType, StringBuilder sb)
    {
        // Find sobre hijos directos, igual que hace OVRCameraRig al enlazar anchors.
        Transform anchor = trackingSpace.Find(anchorName);
        if (anchor == null)
        {
            sb.AppendLine($"[WARN] {anchorName} no encontrado");
            return;
        }

        if (anchor.GetComponentInChildren<OVRHand>(true) != null)
        {
            sb.AppendLine($"{anchorName}: ya tenía una mano, se omite");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Packages/com.meta.xr.sdk.core/Prefabs/OVRHandPrefab.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
        instance.name = $"Hand_{handType}";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        var hand = instance.GetComponent<OVRHand>();
        var skeleton = instance.GetComponent<OVRSkeleton>();
        var mesh = instance.GetComponent<OVRMesh>();

        if (hand != null)
        {
            var so = new SerializedObject(hand);
            so.FindProperty("HandType").enumValueIndex = (int)handType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (skeleton != null)
        {
            var so = new SerializedObject(skeleton);
            so.FindProperty("_skeletonType").enumValueIndex = (int)skeletonType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if (mesh != null)
        {
            var so = new SerializedObject(mesh);
            so.FindProperty("_meshType").enumValueIndex = (int)meshType;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Undo.RegisterCreatedObjectUndo(instance, "Add hand");
        sb.AppendLine($"{anchorName} -> {instance.name} (hand={handType}, skeleton={skeletonType}, mesh={meshType})");
    }

    private static void EnableHandTrackingInProjectConfig(StringBuilder sb)
    {
        // Sin esto el manifiesto de Android no pide el permiso y el Quest nunca
        // entrega datos de manos, por muchos OVRHand que haya en la escena.
        OVRProjectConfig config = OVRProjectConfig.CachedProjectConfig;
        if (config == null)
        {
            sb.AppendLine("[WARN] no se pudo leer OVRProjectConfig");
            return;
        }

        config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.ControllersAndHands;
        OVRProjectConfig.CommitProjectConfig(config);
        sb.AppendLine("OVRProjectConfig.handTrackingSupport -> ControllersAndHands");
    }
}
