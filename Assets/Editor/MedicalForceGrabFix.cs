using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Desactiva "Force Grab" en todos los rayos de interacción.
///
/// Es una opción de XRI que viene activada por defecto, y su propia documentación
/// dice qué hace:
///
///     "Force grab moves the object to your hand rather than
///      interacting with it at a distance."
///
/// Es decir: al agarrar, trae el objeto hasta tu mano de un salto. Eso era el
/// "teletransporte" al seleccionar, no un fallo de pivotes ni de colliders.
///
/// Con la opción apagada, XRRayInteractor coloca el punto de anclaje en el punto
/// exacto donde impactó el rayo, y el objeto se manipula ahí mismo.
/// </summary>
public static class MedicalForceGrabFix
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step46 - Disable Force Grab")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var rays = Object.FindObjectsOfType<XRRayInteractor>(true);
        sb.AppendLine($"=== {rays.Length} rayos de interacción ===");

        foreach (var ray in rays)
        {
            bool before = ray.useForceGrab;
            ray.useForceGrab = false;

            // Sin distancia máxima de agarre razonable, un objeto agarrado muy lejos
            // se manipula de forma poco natural.
            ray.enableUIInteraction = true;

            EditorUtility.SetDirty(ray);
            sb.AppendLine($"{Path(ray.transform)}: useForceGrab {before} -> false");
        }

        // Con force grab apagado, el objeto se queda a la distancia a la que lo
        // agarraste. El control por joystick permite acercarlo si hace falta.
        foreach (var ray in rays)
        {
            bool isHand = ray.GetComponent<OVRHandXRController>() != null;
            ray.allowAnchorControl = !isHand; // con la mano se acerca moviendo el brazo
            EditorUtility.SetDirty(ray);
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step46_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP46_DONE");
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}
