using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Paso 2 de la interfaz: el menú pasa a controlar los órganos directamente y se retira
/// el panel "Sistemas".
///
/// El profesor dijo que organizar por sistemas es propio de un programa de educación
/// anatómica y que lo que se busca es apoyo en cirugía. El panel era además el único
/// dueño de la visibilidad de los órganos: "Modelo 3D" solo encendía el panel y era el
/// panel quien mostraba los órganos. Aquí "Modelo 3D" enciende los órganos en sí (los
/// mismos que controlaba el panel) y el panel queda desactivado, sin borrarse.
/// </summary>
public static class MedicalViewSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step64 - Vistas del menu y retirar Sistemas")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        var systems = Object.FindObjectOfType<OrganSystemsPanel>(true);
        if (actions == null || systems == null)
        {
            Debug.LogError("[Step64] Falta MedicalMenuActions u OrganSystemsPanel.");
            return;
        }

        // Los órganos que controlaba el panel, leídos de sus filas en vez de por nombre.
        var targets = new List<GameObject>();
        var rows = new SerializedObject(systems).FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var target = row.FindPropertyRelative("target").objectReferenceValue as GameObject;
            string label = row.FindPropertyRelative("label").stringValue;
            if (target == null)
            {
                sb.AppendLine("[AVISO] fila '" + label + "' sin objeto");
                continue;
            }
            targets.Add(target);
            sb.AppendLine("organo: " + label + " -> " + target.name);
        }

        var so = new SerializedObject(actions);
        var model = so.FindProperty("model3DObjects");
        sb.AppendLine("Modelo 3D antes: " + Describe(model));

        model.arraySize = targets.Count;
        for (int i = 0; i < targets.Count; i++)
        {
            model.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);
        so.Update();
        sb.AppendLine("Modelo 3D ahora: " + Describe(so.FindProperty("model3DObjects")));

        // El panel se retira: desactivado y fuera del menú, pero no se borra.
        Canvas canvas = systems.GetComponentInParent<Canvas>(true);
        GameObject panelRoot = canvas != null ? canvas.rootCanvas.gameObject : systems.gameObject;
        panelRoot.SetActive(false);
        EditorUtility.SetDirty(panelRoot);
        sb.AppendLine("panel Sistemas: " + panelRoot.name + " desactivado (no borrado)");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step64_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP64_DONE");
    }

    private static string Describe(SerializedProperty list)
    {
        var names = new List<string>();
        for (int i = 0; i < list.arraySize; i++)
        {
            Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
            names.Add(o == null ? "<vacio>" : o.name);
        }
        return names.Count == 0 ? "(nada)" : string.Join(", ", names);
    }
}
