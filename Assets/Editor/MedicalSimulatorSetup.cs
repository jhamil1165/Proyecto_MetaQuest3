using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Instala o retira el XR Device Simulator de Unity, que permite probar la escena
/// sin headset simulando cabeza y mandos con teclado y ratón.
///
/// Se retira cuando hay Quest físico: en el dispositivo el simulador sobra y además
/// competiría con el input real.
/// </summary>
public static class MedicalSimulatorSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string SampleDir = "Assets/Samples/XR Interaction Toolkit/2.6.5/XR Device Simulator";
    private const string SimulatorPrefab = SampleDir + "/XR Device Simulator.prefab";

    [MenuItem("MedicalViewer/Step30 - Install XR Device Simulator")]
    public static void Install()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPrefab);
        if (prefab == null)
        {
            Debug.LogError($"STEP30_FAILED no se encontró {SimulatorPrefab}");
            return;
        }

        GameObject existing = GameObject.Find("XR Device Simulator");
        if (existing != null) Object.DestroyImmediate(existing);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "XR Device Simulator";
        Undo.RegisterCreatedObjectUndo(instance, "Install XR Device Simulator");
        sb.AppendLine("XR Device Simulator instalado");

        SetEyeHeightSimulator(false, sb);
        Save(sb, "step30_output.txt", "STEP30_DONE");
    }

    [MenuItem("MedicalViewer/Step31 - Remove XR Device Simulator")]
    public static void Remove()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject existing = GameObject.Find("XR Device Simulator");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
            sb.AppendLine("XR Device Simulator eliminado de la escena");
        }
        else
        {
            sb.AppendLine("no había simulador en la escena");
        }

        // Se reactiva: con el Quest conectado detecta pose real de cabeza y se
        // desactiva solo, así que sigue siendo útil para previsualizar sin headset.
        SetEyeHeightSimulator(true, sb);

        // El objeto de escena debe irse ANTES que el asset, o quedaría una
        // referencia rota al prefab.
        if (AssetDatabase.IsValidFolder(SampleDir))
        {
            if (AssetDatabase.DeleteAsset(SampleDir))
            {
                sb.AppendLine($"carpeta del sample eliminada: {SampleDir}");
            }
            else
            {
                sb.AppendLine($"[WARN] no se pudo eliminar {SampleDir}");
            }
        }

        Save(sb, "step31_output.txt", "STEP31_DONE");
    }

    private static void SetEyeHeightSimulator(bool enabled, StringBuilder sb)
    {
        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        if (rig == null) return;

        var eyeSim = rig.GetComponent<EditorEyeHeightSimulator>();
        if (eyeSim == null) return;

        eyeSim.enabled = enabled;
        EditorUtility.SetDirty(eyeSim);
        sb.AppendLine($"EditorEyeHeightSimulator -> {(enabled ? "activado" : "desactivado")}");
    }

    private static void Save(StringBuilder sb, string file, string marker)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + file, sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log(marker);
    }
}
