using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Termina la parte de NDI: buscador de emisiones y cartel de estado a la vista.
///
/// Lo que faltaba:
///
/// 1. El nombre de la fuente estaba escrito a mano en la escena. El dia que el ordenador del
///    laboratorio cambie de IP o se emita desde otro equipo, deja de funcionar y no hay forma
///    de arreglarlo desde el visor. Ahora se busca en la red.
/// 2. El cartel que avisa de "sin señal" existia y estaba bien enganchado, pero su objeto no
///    estaba en ninguna vista del menu, asi que no se encendia nunca y nadie lo vio jamas.
/// </summary>
public static class MedicalNdiFinish
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step89 - Terminar la parte de NDI")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        int wired = 0;

        foreach (var status in Object.FindObjectsOfType<NdiScreenStatus>(true))
        {
            GameObject go = status.gameObject;

            // El buscador vive junto al receptor, en la propia pantalla.
            var picker = go.GetComponent<NdiSourcePicker>();
            if (picker == null) picker = go.AddComponent<NdiSourcePicker>();

            var receiver = FindReceiver(go);
            var pso = new SerializedObject(picker);
            pso.FindProperty("receiver").objectReferenceValue = receiver;

            // La fuente que hubiera puesta pasa a ser la preferida, no la unica.
            string current = receiver != null ? ReadName(receiver) : null;
            if (!string.IsNullOrEmpty(current))
            {
                pso.FindProperty("preferred").stringValue = current;
                sb.AppendLine(go.name + ": fuente preferida '" + current + "' (ya no es la unica)");
            }
            else
            {
                sb.AppendLine(go.name + ": sin fuente previa; se enganchara a la primera que vea");
            }

            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(picker);

            var sso = new SerializedObject(status);
            sso.FindProperty("picker").objectReferenceValue = picker;
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(status);

            wired++;
        }

        sb.AppendLine("pantallas con buscador: " + wired);

        // ---- que el cartel de estado se encienda con la vista DICOM ----
        GameObject label = GameObject.Find("Canvas_NDI_Status");
        if (label == null)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
            {
                if (t.name == "Canvas_NDI_Status") { label = t.gameObject; break; }
            }
        }

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (label != null && actions != null)
        {
            var aso = new SerializedObject(actions);
            var group = aso.FindProperty("dicomObjects");

            bool already = false;
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue == label) already = true;
            }

            if (!already)
            {
                group.arraySize += 1;
                group.GetArrayElementAtIndex(group.arraySize - 1).objectReferenceValue = label;
                aso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(actions);
            }

            label.SetActive(true);
            EditorUtility.SetDirty(label);
            sb.AppendLine("cartel de estado: se enciende con la vista DICOM" +
                          (already ? " (ya estaba)" : " (antes no se encendia nunca)"));
        }
        else
        {
            sb.AppendLine("[AVISO] no encuentro Canvas_NDI_Status o el menu");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step89_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP89_DONE");
    }

    /// <summary>El NdiReceiver del objeto, sin depender del tipo en compilacion.</summary>
    private static MonoBehaviour FindReceiver(GameObject go)
    {
        foreach (var behaviour in go.GetComponents<MonoBehaviour>())
        {
            if (behaviour != null && behaviour.GetType().Name == "NdiReceiver") return behaviour;
        }
        return null;
    }

    private static string ReadName(MonoBehaviour receiver)
    {
        var property = new SerializedObject(receiver).FindProperty("_ndiName");
        return property != null ? property.stringValue : null;
    }
}
