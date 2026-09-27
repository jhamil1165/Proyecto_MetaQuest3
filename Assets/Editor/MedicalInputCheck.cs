using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mira por que los botones no responden bien con los mandos: cuantos sistemas de eventos
/// hay, que modulo de entrada usan, si cada lienzo tiene el detector de rayos que hace falta
/// y si los botones son lo bastante grandes para acertarles a un metro y medio.
/// </summary>
public static class MedicalInputCheck
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step85 - Por que fallan los botones")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        sb.AppendLine("=== SISTEMA DE EVENTOS ===");
        var systems = Object.FindObjectsOfType<EventSystem>(true);
        sb.AppendLine("EventSystem encontrados: " + systems.Length +
                      (systems.Length > 1 ? "   <-- con mas de uno se pelean entre ellos" : ""));

        foreach (var system in systems)
        {
            sb.AppendLine("  '" + system.name + "' activo=" + system.gameObject.activeInHierarchy);
            foreach (var module in system.GetComponents<BaseInputModule>())
            {
                sb.AppendLine("     modulo: " + module.GetType().Name + " activo=" + module.enabled);
            }
        }

        sb.AppendLine();
        sb.AppendLine("=== LIENZOS Y SU DETECTOR DE RAYOS ===");
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            if (canvas.transform.parent != null && canvas.GetComponentInParent<Canvas>() != canvas) continue;

            var raycaster = canvas.GetComponent<BaseRaycaster>();
            int buttons = canvas.GetComponentsInChildren<Button>(true).Length;
            int toggles = canvas.GetComponentsInChildren<Toggle>(true).Length;
            if (buttons + toggles == 0) continue;

            sb.AppendLine(string.Format("{0}  modo={1}  botones={2} interruptores={3}  detector={4}",
                canvas.name, canvas.renderMode, buttons, toggles,
                raycaster != null ? raycaster.GetType().Name : "NINGUNO  <-- no se puede pulsar"));
        }

        sb.AppendLine();
        // Sin esto los botones que coloca un layout miden 0: en batchmode el lienzo no se
        // dibuja, y hasta que no se dibuja no se reparte el espacio entre los hijos.
        Canvas.ForceUpdateCanvases();
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            var rect = canvas.GetComponent<RectTransform>();
            if (rect != null) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        sb.AppendLine("=== TAMAÑO REAL DE LOS BOTONES ===");
        sb.AppendLine("(a 1,5 m, un boton comodo ocupa mas de 2 grados de ancho y de alto)");

        GameObject root = GameObject.Find("Medical_Menu_UI");
        Vector3 eye = root != null ? root.transform.position : Vector3.zero;

        foreach (var button in Object.FindObjectsOfType<Button>(true))
        {
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) continue;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float width = Vector3.Distance(corners[0], corners[3]);
            float height = Vector3.Distance(corners[0], corners[1]);
            float distance = Vector3.Distance((corners[0] + corners[2]) * 0.5f, eye);
            if (distance < 0.01f) continue;

            float degW = 2f * Mathf.Atan2(width * 0.5f, distance) * Mathf.Rad2Deg;
            float degH = 2f * Mathf.Atan2(height * 0.5f, distance) * Mathf.Rad2Deg;

            sb.AppendLine(string.Format("{0,-22} {1:F0}x{2:F0} mm a {3:F2} m  ->  {4:F1} x {5:F1} grados{6}",
                button.name, width * 1000f, height * 1000f, distance, degW, degH,
                degH < 2f ? "   <-- muy bajo, dificil de acertar" : ""));
        }

        sb.AppendLine();
        sb.AppendLine("=== INTERFAZ FUERA DEL MENU (no se repinto) ===");
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            if (root != null && canvas.transform.IsChildOf(root.transform)) continue;
            sb.AppendLine("  " + Path(canvas.gameObject) + "  activo=" + canvas.gameObject.activeInHierarchy);
        }

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step85_entrada.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP85_DONE");
    }

    private static string Path(GameObject go)
    {
        string path = go.name;
        for (Transform t = go.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
        return path;
    }
}
