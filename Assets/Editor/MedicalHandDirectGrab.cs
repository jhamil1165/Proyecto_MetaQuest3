using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Añade agarre por CONTACTO a cada mano: acercas la mano al órgano, pellizcas, y se
/// viene contigo. Es el gesto natural con manos; el rayo sigue disponible para lo que
/// esté lejos.
///
/// El interactor directo cuelga del mismo objeto que el controlador de mano, así que
/// hereda su pose y su gesto de pinza sin cablear nada más.
/// </summary>
public static class MedicalHandDirectGrab
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    // Radio de la zona de agarre alrededor de la punta de los dedos.
    private const float GrabRadius = 0.09f;

    // Distancia a la que se colocan los órganos: dentro del alcance del brazo, para
    // que tocarlos sea posible sin caminar.
    private const float OrganReach = 0.62f;

    [MenuItem("MedicalViewer/Step41 - Hand Direct Grab")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var handControllers = Object.FindObjectsOfType<OVRHandXRController>(true);
        sb.AppendLine($"--- {handControllers.Length} manos ---");

        foreach (var controller in handControllers)
        {
            AddDirectInteractor(controller, sb);
        }

        BringOrgansIntoReach(sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step41_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP41_DONE");
    }

    private static void AddDirectInteractor(OVRHandXRController controller, StringBuilder sb)
    {
        Transform parent = controller.transform;

        const string name = "PinchGrab";
        Transform existing = parent.Find(name);
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        // Un poco por delante del origen del puntero, donde queda la punta de los dedos.
        go.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        Undo.RegisterCreatedObjectUndo(go, "Add pinch grab");

        var sphere = go.AddComponent<SphereCollider>();
        sphere.isTrigger = true; // obligatorio para que XRI lo use como zona de agarre
        sphere.radius = GrabRadius;

        var direct = go.AddComponent<XRDirectInteractor>();
        direct.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State;

        // Sin esto el interactor buscaría un XRBaseController en su propio objeto y no
        // encontraría ninguno: el de la mano está en el padre.
        var so = new SerializedObject(direct);
        var controllerProp = so.FindProperty("m_XRController");
        if (controllerProp != null) controllerProp.objectReferenceValue = controller;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(direct);
        sb.AppendLine($"{parent.name}: zona de agarre por contacto (radio {GrabRadius} m)");
    }

    private static void BringOrgansIntoReach(StringBuilder sb)
    {
        var recenter = Object.FindObjectOfType<WorkspaceRecenter>();
        if (recenter == null)
        {
            sb.AppendLine("[WARN] sin WorkspaceRecenter, no se ajusta el alcance");
            return;
        }

        var so = new SerializedObject(recenter);
        var distance = so.FindProperty("organDistance");
        float before = distance.floatValue;
        distance.floatValue = OrganReach;

        // Un poco más abajo, a la altura natural de las manos en reposo.
        var height = so.FindProperty("organHeightOffset");
        height.floatValue = -0.28f;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recenter);

        sb.AppendLine($"órganos: distancia {before:F2} -> {OrganReach:F2} m (al alcance del brazo)");
    }
}
