using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Quita de las manos lo que no tiene que estar ahí cuando alguien usa la aplicación.
///
/// El panel de depuración del hand tracking (Hand_Debug_HUD) seguía encendido y colgado del
/// ancla izquierda. Es una ayuda para desarrollar: enseña si la mano se rastrea, si detecta
/// la pinza y qué tiene delante el rayo. Colgado de la mano izquierda se mueve con ella y
/// además se reorienta hacia el usuario en cada fotograma, así que tiembla con cualquier
/// vibración del mando y da la sensación de que la mano izquierda va mal.
///
/// No se borra: se apaga. Sigue ahí para el día que haga falta depurar el hand tracking, que
/// sin él es adivinar.
///
/// De paso se deja un solo juego de interactores activo en la escena guardada. Al arrancar,
/// InteractorModeSwitch lo arregla en el primer fotograma, pero hasta entonces conviven
/// cuatro rayos, y al agarrar algo el primero que llega se queda el objeto.
/// </summary>
public static class MedicalRigCleanup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step92 - Limpiar lo que cuelga de las manos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        // ---- el panel de depuración ----
        foreach (var hud in Object.FindObjectsOfType<HandDebugHud>(true))
        {
            if (!hud.gameObject.activeSelf)
            {
                sb.AppendLine("panel de depuracion '" + hud.name + "': ya estaba apagado");
                continue;
            }

            hud.gameObject.SetActive(false);
            EditorUtility.SetDirty(hud.gameObject);
            sb.AppendLine("panel de depuracion '" + hud.name + "' APAGADO (colgaba de " +
                          (hud.transform.parent != null ? hud.transform.parent.name : "la raiz") + ")");
        }

        // ---- un solo juego de interactores en la escena guardada ----
        var mode = Object.FindObjectOfType<InteractorModeSwitch>(true);
        if (mode != null)
        {
            var so = new SerializedObject(mode);

            // Se arranca con mandos: es lo que casi siempre hay en la mano al ponerse el visor.
            Set(so.FindProperty("handInteractors"), false, sb, "interactores de mano");
            Set(so.FindProperty("controllerInteractors"), true, sb, "interactores de mando");
            Set(so.FindProperty("controllerModels"), true, sb, "modelos de mando");

            // El valor vive en la escena: cambiar el valor por defecto del script no toca
            // lo que ya estaba guardado, asi que hay que escribirlo aqui.
            var delay = so.FindProperty("switchDelay");
            float before = delay.floatValue;
            if (before < 1.0f)
            {
                delay.floatValue = 1.2f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(mode);
            }

            sb.AppendLine("margen para cambiar de modo: " + before + " s -> " + delay.floatValue + " s");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step92_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP92_DONE");
    }

    private static void Set(SerializedProperty list, bool active, StringBuilder sb, string label)
    {
        if (list == null) return;

        int changed = 0;
        for (int i = 0; i < list.arraySize; i++)
        {
            var go = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (go == null || go.activeSelf == active) continue;

            go.SetActive(active);
            EditorUtility.SetDirty(go);
            changed++;
        }

        sb.AppendLine(label + ": " + (active ? "encendidos" : "apagados") +
                      " (" + changed + " cambiados de " + list.arraySize + ")");
    }
}
