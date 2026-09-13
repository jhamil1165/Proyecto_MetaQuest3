using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Un GameObject con dos interactables encima solo funciona a medias.
///
/// XRInteractionManager guarda un diccionario collider -> interactable y al registrar
/// hace "si la clave no existe, anadela" (XRInteractionManager.cs:916). O sea: el
/// PRIMERO que se registra se queda con los colliders y el segundo queda inerte, sin
/// ningun aviso. Si un organo conserva el XRSimpleInteractable antiguo junto al
/// XRGrabInteractable, el rayo resuelve al simple y el agarre nunca ocurre: el organo
/// se ilumina al apuntarlo pero no se mueve.
///
/// Aqui se localizan esos duplicados y se desactiva el simple (no se borra: el proyecto
/// es de grupo). Un componente desactivado no se registra, porque el registro ocurre
/// en OnEnable.
/// </summary>
public static class MedicalInteractableConflict
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step49 - Fix Interactable Conflicts")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        FixConflicts(sb);
        ReportButtons(sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step49_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP49_DONE");
    }

    private static void FixConflicts(StringBuilder sb)
    {
        sb.AppendLine("=== interactables duplicados ===");
        int fixedCount = 0;

        foreach (var grab in Object.FindObjectsOfType<XRGrabInteractable>(true))
        {
            GameObject go = grab.gameObject;
            var all = go.GetComponents<XRBaseInteractable>();

            if (all.Length <= 1)
            {
                sb.AppendLine($"{go.name}: solo XRGrabInteractable, correcto");
                continue;
            }

            sb.AppendLine($"{go.name}: {all.Length} interactables en el mismo objeto -> CONFLICTO");

            foreach (var other in all)
            {
                if (other == grab) continue;

                sb.AppendLine($"   desactivando {other.GetType().Name} (habilitado={other.enabled})");
                other.enabled = false;
                EditorUtility.SetDirty(other);
                fixedCount++;
            }
        }

        sb.AppendLine($"conflictos resueltos: {fixedCount}");
    }

    private static void ReportButtons(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("=== cableado de los botones del menu ===");

        GameObject actionsCanvas = null;
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == "Canvas_Actions" && t.gameObject.scene.IsValid())
            {
                actionsCanvas = t.gameObject;
                break;
            }
        }

        if (actionsCanvas == null)
        {
            sb.AppendLine("[FALLO] Canvas_Actions no encontrado");
            return;
        }

        foreach (var btn in actionsCanvas.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            int n = btn.onClick.GetPersistentEventCount();
            sb.AppendLine($"Button '{btn.name}': {n} llamada(s) persistente(s)  interactable={btn.interactable}");

            for (int i = 0; i < n; i++)
            {
                Object target = btn.onClick.GetPersistentTarget(i);
                sb.AppendLine($"   -> {(target == null ? "<VACIO>" : target.GetType().Name)}." +
                              $"{btn.onClick.GetPersistentMethodName(i)}");
            }
        }

        foreach (var si in actionsCanvas.GetComponentsInChildren<XRSimpleInteractable>(true))
        {
            int n = si.selectEntered.GetPersistentEventCount();
            sb.AppendLine($"SimpleInteractable '{si.name}': {n} llamada(s) en selectEntered");

            for (int i = 0; i < n; i++)
            {
                Object target = si.selectEntered.GetPersistentTarget(i);
                sb.AppendLine($"   -> {(target == null ? "<VACIO>" : target.GetType().Name)}." +
                              $"{si.selectEntered.GetPersistentMethodName(i)}");
            }
        }
    }
}
