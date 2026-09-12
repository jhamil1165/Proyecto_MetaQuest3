using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Arregla lo que solo se ve en el dispositivo: el contenido aparecía por debajo y
/// desviado respecto a donde mira el usuario.
///
/// Dos causas, dos arreglos:
///  1. EditorEyeHeightSimulator subía el rig 1,6 m también con el Quest conectado,
///     porque su detección no esperaba lo suficiente a que el tracking se asentara.
///     Con headset físico ese componente solo estorba: se elimina.
///  2. Todo estaba colocado en posiciones fijas del mundo, calculadas en el editor.
///     Ahora WorkspaceRecenter lo coloca en tiempo de ejecución usando la pose real.
/// </summary>
public static class MedicalDeviceFix
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step32 - Fix On-Device Placement")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        // ---------- 1. fuera el simulador de altura ----------
        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig != null)
        {
            var eyeSim = rig.GetComponent<EditorEyeHeightSimulator>();
            if (eyeSim != null)
            {
                Object.DestroyImmediate(eyeSim);
                sb.AppendLine("EditorEyeHeightSimulator ELIMINADO (subía el rig 1,6 m también con el Quest)");
            }
        }

        // ---------- 2. colocación en tiempo de ejecución ----------
        GameObject uiManager = GameObject.Find("UI_Manager");
        GameObject centerEye = GameObject.Find("CenterEyeAnchor");
        GameObject menuRoot = GameObject.Find("Medical_Menu_UI");

        if (uiManager == null || centerEye == null || menuRoot == null)
        {
            Debug.LogError("STEP32_FAILED falta UI_Manager, CenterEyeAnchor o Medical_Menu_UI");
            return;
        }

        var recenter = uiManager.GetComponent<WorkspaceRecenter>();
        if (recenter == null)
        {
            recenter = Undo.AddComponent<WorkspaceRecenter>(uiManager);
            sb.AppendLine("UI_Manager -> WorkspaceRecenter añadido");
        }

        string[] organNames = { "Heart", "Hígado", "Estómago", "Páncreas", "Vesícula" };
        var found = new System.Collections.Generic.List<Transform>();
        foreach (var n in organNames)
        {
            GameObject go = GameObject.Find(n);
            if (go != null) found.Add(go.transform);
            else sb.AppendLine($"[WARN] órgano {n} no encontrado");
        }

        var so = new SerializedObject(recenter);
        so.FindProperty("headAnchor").objectReferenceValue = centerEye.transform;
        so.FindProperty("menuRoot").objectReferenceValue = menuRoot.transform;

        var organsProp = so.FindProperty("organs");
        organsProp.arraySize = found.Count;
        for (int i = 0; i < found.Count; i++)
        {
            organsProp.GetArrayElementAtIndex(i).objectReferenceValue = found[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recenter);
        sb.AppendLine($"WorkspaceRecenter enlazado: cabeza + menú + {found.Count} órganos");

        // El botón de reabrir el menú también recentra, para que nunca quede a la espalda.
        var toggle = Object.FindObjectOfType<MenuToggleInput>();
        if (toggle != null)
        {
            var tso = new SerializedObject(toggle);
            tso.FindProperty("recenter").objectReferenceValue = recenter;
            tso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(toggle);
            sb.AppendLine("MenuToggleInput -> recentra al reabrir el menú");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step32_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP32_DONE");
    }
}
