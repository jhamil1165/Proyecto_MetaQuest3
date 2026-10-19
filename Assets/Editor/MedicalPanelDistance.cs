using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Los paneles del menú guardan una distancia mínima con la cabeza (PanelDistanceKeeper),
/// para que la mano real no los atraviese al acercarse a pinzar.
/// </summary>
public static class MedicalPanelDistance
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    private static readonly string[] Panels =
    {
        "Canvas_Actions", "Canvas_Systems", "Canvas_DICOM", "NDI_Screen", "Canvas_NDI_Status",
        "Canvas_SegClean", "Canvas_SegPainted", "Canvas_Controles",
    };

    [MenuItem("MedicalViewer/Step107 - Paneles a distancia de la mano")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject menu = GameObject.Find("UI_Manager/Medical_Menu_UI");
        var keeper = menu.GetComponent<PanelDistanceKeeper>();
        if (keeper == null) keeper = menu.AddComponent<PanelDistanceKeeper>();

        var found = new List<Transform>();
        foreach (string name in Panels)
        {
            Transform t = menu.transform.Find(name);
            if (t == null) { sb.AppendLine("no encuentro " + name); continue; }
            found.Add(t);
            sb.AppendLine("guarda distancia: " + name);
        }

        var so = new SerializedObject(keeper);
        var list = so.FindProperty("panels");
        list.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));
        System.IO.File.WriteAllText("Logs/EditorAutomation/step107_output.txt", sb.ToString());
    }
}
