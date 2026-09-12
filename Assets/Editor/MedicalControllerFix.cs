using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Resuelve el conflicto de pose de los mandos y habilita rotar y acercar los
/// órganos agarrados.
///
/// El problema: los anchors del rig los mueve OVRCameraRig con el tracking de Meta,
/// pero encima llevan un ActionBasedController de XRI que TAMBIÉN escribe posición y
/// rotación cada frame. Dos sistemas sobre el mismo transform, y gana el que corra
/// último: de ahí que los mandos aparecieran a la altura equivocada y el rayo
/// apuntara mal.
///
/// El reparto correcto es: OVR pone la POSE, XRI aporta el INPUT (gatillo, UI, agarre).
/// </summary>
public static class MedicalControllerFix
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string ActionsAssetPath =
        "Assets/Samples/XR Interaction Toolkit/2.6.5/Starter Assets/XRI Default Input Actions.inputactions";

    [MenuItem("MedicalViewer/Step35 - Fix Controller Pose + Rotation")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var actionRefs = AssetDatabase.LoadAllAssetsAtPath(ActionsAssetPath)
            .OfType<UnityEngine.InputSystem.InputActionReference>()
            .ToArray();

        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig == null)
        {
            Debug.LogError("STEP35_FAILED Camera Rig no encontrado");
            return;
        }

        Transform trackingSpace = rig.transform.Find("TrackingSpace");
        if (trackingSpace == null)
        {
            Debug.LogError("STEP35_FAILED TrackingSpace no encontrado");
            return;
        }

        Fix(trackingSpace, "LeftHandAnchor", "LeftControllerAnchor", "XRI LeftHand Interaction", actionRefs, sb);
        Fix(trackingSpace, "RightHandAnchor", "RightControllerAnchor", "XRI RightHand Interaction", actionRefs, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step35_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP35_DONE");
    }

    private static void Fix(Transform trackingSpace, string handAnchor, string controllerAnchor,
        string interactionMap, UnityEngine.InputSystem.InputActionReference[] refs, StringBuilder sb)
    {
        Transform hand = trackingSpace.Find(handAnchor);
        Transform ctrl = hand != null ? hand.Find(controllerAnchor) : null;
        if (ctrl == null)
        {
            sb.AppendLine($"[WARN] {handAnchor}/{controllerAnchor} no encontrado");
            return;
        }

        // ---- 1. XRI deja de mover el transform: eso lo hace OVR ----
        var controller = ctrl.GetComponent<ActionBasedController>();
        if (controller != null)
        {
            controller.enableInputTracking = false;
            controller.enableInputActions = true; // el input SÍ se sigue usando

            var so = new SerializedObject(controller);
            BindAction(so, "m_RotateAnchorAction", refs, interactionMap, "Rotate Anchor", sb);
            BindAction(so, "m_TranslateAnchorAction", refs, interactionMap, "Translate Anchor", sb);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(controller);
            sb.AppendLine($"{controllerAnchor}: enableInputTracking = false (la pose la pone OVR)");
        }

        // ---- 2. Joystick para girar y acercar lo agarrado ----
        var ray = ctrl.GetComponent<XRRayInteractor>();
        if (ray != null)
        {
            ray.allowAnchorControl = true;
            ray.rotateSpeed = 180f;
            ray.translateSpeed = 1f;
            EditorUtility.SetDirty(ray);
            sb.AppendLine($"{controllerAnchor}: joystick rota y acerca el objeto agarrado");
        }
    }

    private static void BindAction(SerializedObject so, string propertyName,
        UnityEngine.InputSystem.InputActionReference[] refs, string mapName, string actionName, StringBuilder sb)
    {
        var reference = refs.FirstOrDefault(r =>
            r != null && r.action != null && r.action.actionMap != null &&
            r.action.actionMap.name == mapName && r.action.name == actionName);

        if (reference == null)
        {
            sb.AppendLine($"  [WARN] sin referencia para {mapName}/{actionName}");
            return;
        }

        var prop = so.FindProperty(propertyName);
        prop.FindPropertyRelative("m_UseReference").boolValue = true;
        prop.FindPropertyRelative("m_Reference").objectReferenceValue = reference;
    }
}
