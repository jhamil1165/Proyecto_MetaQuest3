using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Deja una sola pantalla NDI en la escena.
///
/// Habia dos objetos llamados NDI_Screen: la que ya estaba montada (a la derecha del menu,
/// con la fuente 10.36.202.200 (stream0) puesta y la etiqueta de estado enganchada) y otra
/// que se subio despues, sin fuente, sin propiedad de material y colocada a 440 metros del
/// puesto de trabajo, donde no la ve nadie.
///
/// La segunda no se borra: es trabajo de una compañera. Se apaga, se le pone un nombre que
/// diga lo que pasa y se trae al lado de la buena, para que quien la abra la encuentre y
/// decida. Al estar apagada no entra en el APK ni estorba.
/// </summary>
public static class MedicalNdiDuplicate
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step76 - Dejar una sola pantalla NDI")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var screens = new System.Collections.Generic.List<GameObject>();
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t.name.StartsWith("NDI_Screen")) screens.Add(t.gameObject);
        }

        sb.AppendLine("pantallas encontradas: " + screens.Count);
        if (screens.Count < 2)
        {
            sb.AppendLine("no hay duplicado que arreglar");
            Finish(sb);
            return;
        }

        // La buena es la que tiene fuente NDI puesta.
        GameObject good = null;
        foreach (var screen in screens)
        {
            if (!string.IsNullOrEmpty(ReadSourceName(screen))) { good = screen; break; }
        }

        if (good == null)
        {
            sb.AppendLine("[AVISO] ninguna tiene fuente NDI; no toco nada por no elegir a ciegas");
            Finish(sb);
            return;
        }

        sb.AppendLine("se queda: " + Path(good) + "  fuente '" + ReadSourceName(good) + "'");

        foreach (var screen in screens)
        {
            if (screen == good) continue;

            // Al lado de la buena y un poco detras, para que no tape nada si alguien la enciende.
            screen.transform.SetParent(good.transform.parent, false);
            screen.transform.localPosition = good.transform.localPosition + new Vector3(0f, 0f, 0.25f);
            screen.transform.localRotation = good.transform.localRotation;
            screen.name = "NDI_Screen_duplicada_APAGADA";
            screen.SetActive(false);

            EditorUtility.SetDirty(screen);
            sb.AppendLine("apagada y recolocada: " + Path(screen) + " (estaba a " +
                          "440 m del puesto, sin fuente NDI)");
        }

        Finish(sb);
    }

    /// <summary>Lee el campo _ndiName del NdiReceiver sin depender del tipo en compilacion.</summary>
    private static string ReadSourceName(GameObject go)
    {
        foreach (var behaviour in go.GetComponents<MonoBehaviour>())
        {
            if (behaviour == null) continue;
            if (behaviour.GetType().Name != "NdiReceiver") continue;

            var so = new SerializedObject(behaviour);
            var property = so.FindProperty("_ndiName");
            if (property != null) return property.stringValue;
        }
        return null;
    }

    private static string Path(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }

    private static void Finish(StringBuilder sb)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step76_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP76_DONE");
    }
}
