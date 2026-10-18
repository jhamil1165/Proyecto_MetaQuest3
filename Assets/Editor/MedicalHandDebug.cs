using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Enciende o apaga el panel de diagnóstico de las manos (va sobre la mano izquierda). Sirve
/// para las pruebas en el visor: con una captura del panel mientras se pinza sobre un botón
/// se ve si falla la detección de la pinza o la interfaz.
/// </summary>
public static class MedicalHandDebug
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("MedicalViewer/Step106 - Panel de diagnostico de manos: encender")]
    public static void On() => Set(true);

    [MenuItem("MedicalViewer/Step106 - Panel de diagnostico de manos: apagar")]
    public static void Off() => Set(false);

    private static void Set(bool on)
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        foreach (var hud in Object.FindObjectsOfType<HandDebugHud>(true))
        {
            hud.gameObject.SetActive(on);
            var so = new SerializedObject(hud);
            foreach (string f in new[] { "output", "leftHand", "rightHand", "leftRay", "rightRay" })
                sb.AppendLine(f + " = " + (so.FindProperty(f).objectReferenceValue != null ? "ok" : "FALTA"));
            sb.AppendLine((on ? "encendido: " : "apagado: ") + hud.name);
        }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));
        System.IO.File.WriteAllText("Logs/EditorAutomation/step106_output.txt", sb.ToString());
    }
}
