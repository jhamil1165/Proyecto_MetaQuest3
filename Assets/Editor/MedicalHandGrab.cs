using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Dos arreglos sobre la interacción con los órganos:
///
///  1. Estabiliza el agarre. Vesícula y estómago salían disparados o caían al
///     suelo: sus Rigidbody no habían quedado cinemáticos, así que la física los
///     movía además del agarre.
///  2. Añade interactores de MANO: un rayo por mano gobernado por el gesto de pinza,
///     para agarrar, mover y rotar los órganos sin mandos.
/// </summary>
public static class MedicalHandGrab
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step36 - Hand Grab + Stable Organs")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        StabiliseOrgans(sb);
        AddHandInteractors(sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step36_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP36_DONE");
    }

    private static void StabiliseOrgans(StringBuilder sb)
    {
        var grabs = Object.FindObjectsOfType<XRGrabInteractable>(true);
        sb.AppendLine($"--- estabilizando {grabs.Length} objetos agarrables ---");

        foreach (var grab in grabs)
        {
            grab.movementType = XRBaseInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;
            grab.useDynamicAttach = true;

            // Suavizado: sin esto el objeto copia el temblor de la mano 1:1 y da la
            // sensación de irse disparado.
            grab.smoothPosition = true;
            grab.smoothPositionAmount = 8f;
            grab.tightenPosition = 0.6f;
            grab.smoothRotation = true;
            grab.smoothRotationAmount = 8f;
            grab.tightenRotation = 0.6f;

            var rb = grab.GetComponent<Rigidbody>();
            if (rb != null)
            {
                bool wasWrong = !rb.isKinematic || rb.useGravity;

                // La causa de que cayeran al suelo: la física seguía actuando.
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                EditorUtility.SetDirty(rb);
                sb.AppendLine($"{grab.name}: kinematic + sin gravedad{(wasWrong ? "  <-- estaba mal" : "")}");
            }

            EditorUtility.SetDirty(grab);
        }
    }

    private static void AddHandInteractors(StringBuilder sb)
    {
        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        Transform trackingSpace = rig != null ? rig.transform.Find("TrackingSpace") : null;
        if (trackingSpace == null)
        {
            sb.AppendLine("[WARN] TrackingSpace no encontrado, sin interactores de mano");
            return;
        }

        AddHand(trackingSpace, "LeftHandAnchor", sb);
        AddHand(trackingSpace, "RightHandAnchor", sb);
    }

    private static void AddHand(Transform trackingSpace, string anchorName, StringBuilder sb)
    {
        Transform anchor = trackingSpace.Find(anchorName);
        if (anchor == null)
        {
            sb.AppendLine($"[WARN] {anchorName} no encontrado");
            return;
        }

        OVRHand hand = anchor.GetComponentInChildren<OVRHand>(true);
        if (hand == null)
        {
            sb.AppendLine($"[WARN] {anchorName} sin OVRHand (¿se corrió el paso de manos?)");
            return;
        }

        const string interactorName = "HandInteractor";
        Transform existing = anchor.Find(interactorName);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject(interactorName);
        go.transform.SetParent(anchor, false);
        Undo.RegisterCreatedObjectUndo(go, "Add hand interactor");

        var controller = go.AddComponent<OVRHandXRController>();
        var ray = go.AddComponent<XRRayInteractor>();

        // Mismo control por joystick que los mandos no aplica aquí: con la mano se
        // rota girando la muñeca, que es justo lo que se pidió.
        ray.allowAnchorControl = false;

        var line = go.AddComponent<LineRenderer>();
        line.widthMultiplier = 0.004f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        go.AddComponent<XRInteractorLineVisual>();

        var so = new SerializedObject(controller);
        so.FindProperty("hand").objectReferenceValue = hand;

        var gated = so.FindProperty("interactorsToGate");
        gated.arraySize = 1;
        gated.GetArrayElementAtIndex(0).objectReferenceValue = ray;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        sb.AppendLine($"{anchorName} -> {interactorName} (rayo + pinza para agarrar)");
    }
}
