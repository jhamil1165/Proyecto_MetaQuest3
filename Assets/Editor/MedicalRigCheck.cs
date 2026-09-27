using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Enseña el arbol del rig de camara con lo que cuelga de cada mano: interactores, modelos,
/// paneles y cualquier cosa rara que haya quedado de montajes anteriores. Sirve para buscar
/// por que una mano se comporta distinto de la otra.
/// </summary>
public static class MedicalRigCheck
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step91 - Que cuelga de cada mano")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Transform rig = null;
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t.parent == null && t.name.Contains("Camera Rig")) { rig = t; break; }
        }

        if (rig == null)
        {
            Debug.LogError("[Step91] No encuentro el Camera Rig.");
            return;
        }

        Dump(rig, sb, 0);

        sb.AppendLine();
        sb.AppendLine("=== INTERACTOR MODE SWITCH ===");
        var mode = Object.FindObjectOfType<InteractorModeSwitch>(true);
        if (mode == null)
        {
            sb.AppendLine("no hay InteractorModeSwitch en la escena");
        }
        else
        {
            var so = new SerializedObject(mode);
            foreach (string field in new[] { "handInteractors", "controllerInteractors", "controllerModels" })
            {
                var list = so.FindProperty(field);
                sb.AppendLine(field + ": " + list.arraySize);
                for (int i = 0; i < list.arraySize; i++)
                {
                    var go = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    sb.AppendLine("     " + (go != null ? go.name + " (activo=" + go.activeSelf + ")" : "(vacio)"));
                }
            }
            sb.AppendLine("switchDelay: " + so.FindProperty("switchDelay").floatValue + " s");
            sb.AppendLine("leftHand: " + Name(so.FindProperty("leftHand").objectReferenceValue));
            sb.AppendLine("rightHand: " + Name(so.FindProperty("rightHand").objectReferenceValue));
        }

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step91_rig.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP91_DONE");
    }

    private static string Name(Object o)
    {
        return o != null ? o.name : "SIN ASIGNAR";
    }

    private static void Dump(Transform t, StringBuilder sb, int depth)
    {
        // Solo lo que importa: componentes que se ven o que interactuan.
        var parts = new System.Collections.Generic.List<string>();
        foreach (var c in t.GetComponents<Component>())
        {
            if (c == null) { parts.Add("SCRIPT ROTO"); continue; }

            string n = c.GetType().Name;
            if (n == "Transform" || n == "RectTransform") continue;
            parts.Add(n);
        }

        sb.AppendLine(new string(' ', depth * 2) + (t.gameObject.activeSelf ? "- " : "- (apagado) ") +
                      t.name + (parts.Count > 0 ? "   [" + string.Join(", ", parts) + "]" : ""));

        foreach (Transform child in t) Dump(child, sb, depth + 1);
    }
}
