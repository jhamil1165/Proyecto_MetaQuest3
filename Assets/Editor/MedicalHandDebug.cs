using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Ajusta los interactores de mano para que el agarre sea lo más permisivo posible y
/// monta un HUD de diagnóstico anclado a la muñeca izquierda.
/// </summary>
public static class MedicalHandDebug
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step37 - Hand Grab Tuning + Debug HUD")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var handControllers = Object.FindObjectsOfType<OVRHandXRController>(true);
        sb.AppendLine($"--- {handControllers.Length} controladores de mano ---");

        OVRHandXRController left = null, right = null;
        XRRayInteractor leftRay = null, rightRay = null;

        foreach (var controller in handControllers)
        {
            var ray = controller.GetComponent<XRRayInteractor>();
            if (ray != null)
            {
                // "State" mantiene la selección mientras la pinza siga cerrada.
                // Con "StateChange" hacía falta un flanco limpio, y el ruido del
                // tracking de manos hace que ese flanco a veces no llegue.
                ray.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State;
                ray.allowAnchorControl = false;
                ray.enableUIInteraction = true;

                // Un poco de tolerancia: apuntar con la mano tiembla más que con mando.
                ray.hoverToSelect = false;
                ray.maxRaycastDistance = 10f;

                EditorUtility.SetDirty(ray);
                sb.AppendLine($"{controller.name}: selectActionTrigger = State");
            }

            bool isLeft = controller.transform.parent != null &&
                          controller.transform.parent.name.Contains("Left");
            if (isLeft) { left = controller; leftRay = ray; }
            else { right = controller; rightRay = ray; }
        }

        BuildHud(left, right, leftRay, rightRay, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step37_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP37_DONE");
    }

    private static void BuildHud(OVRHandXRController left, OVRHandXRController right,
        XRRayInteractor leftRay, XRRayInteractor rightRay, StringBuilder sb)
    {
        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        Transform trackingSpace = rig != null ? rig.transform.Find("TrackingSpace") : null;
        Transform anchor = trackingSpace != null ? trackingSpace.Find("LeftHandAnchor") : null;

        if (anchor == null)
        {
            sb.AppendLine("[WARN] sin LeftHandAnchor, no se monta el HUD");
            return;
        }

        Transform existing = anchor.Find("Hand_Debug_HUD");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject("Hand_Debug_HUD", typeof(RectTransform));
        go.transform.SetParent(anchor, false);
        Undo.RegisterCreatedObjectUndo(go, "Create hand debug HUD");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        if (centerEye != null) canvas.worldCamera = centerEye.GetComponent<Camera>();

        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(420f, 420f);
        // Colgado de la muñeca, mirando al usuario: se consulta girando la mano.
        rt.localPosition = new Vector3(0f, 0.06f, 0.02f);
        rt.localRotation = Quaternion.Euler(40f, 180f, 0f);
        rt.localScale = Vector3.one * 0.0007f;

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.05f, 0.06f, 0.08f, 0.85f);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        text.fontSize = 26f;
        text.color = new Color(0.85f, 0.92f, 1f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text = "esperando...";

        var trt = text.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(16f, 16f);
        trt.offsetMax = new Vector2(-16f, -16f);

        var hud = go.AddComponent<HandDebugHud>();
        var so = new SerializedObject(hud);
        so.FindProperty("output").objectReferenceValue = text;
        so.FindProperty("leftHand").objectReferenceValue = left;
        so.FindProperty("rightHand").objectReferenceValue = right;
        so.FindProperty("leftRay").objectReferenceValue = leftRay;
        so.FindProperty("rightRay").objectReferenceValue = rightRay;
        so.ApplyModifiedPropertiesWithoutUndo();

        sb.AppendLine("HUD de diagnóstico anclado a la muñeca izquierda");
    }
}
