using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Cada vista enseña solo lo suyo. Segmentación llevaba también el plano de corte, su
/// rebanada del TAC y el panel de controles (corte, volumen, regla), que son de Modelo 3D:
/// al pasar de Modelo 3D a Segmentación seguían ahí. Se sacan de Segmentación y se quitan
/// las referencias vacías que quedaban en las listas de vistas.
/// </summary>
public static class MedicalViewGroups
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private static readonly HashSet<string> NotSegmentation = new HashSet<string>
    {
        "Plano_de_corte", "Corte_TAC", "Canvas_Controles",
    };

    [MenuItem("MedicalViewer/Step104 - Cada vista solo con lo suyo")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        var so = new SerializedObject(actions);

        foreach (string group in new[] { "dicomObjects", "segmentationObjects", "model3DObjects" })
        {
            var list = so.FindProperty(group);
            var keep = new List<Object>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var go = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go == null) { sb.AppendLine(group + ": quitada referencia vacia"); continue; }
                if (group == "segmentationObjects" && NotSegmentation.Contains(go.name))
                {
                    sb.AppendLine(group + ": fuera " + go.name);
                    continue;
                }
                keep.Add(go);
            }

            list.arraySize = keep.Count;
            for (int i = 0; i < keep.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = keep[i];

            var names = new List<string>();
            foreach (var o in keep) names.Add(o.name);
            sb.AppendLine(group + " = " + string.Join(", ", names));
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step104_output.txt", sb.ToString());
        Debug.Log("STEP104_DONE");
    }
}
