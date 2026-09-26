using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Devuelve su material a las plataformas de debajo de los órganos.
///
/// Al pasar los órganos al shader de corte, la plataforma del corazón se quedó con el
/// material de corte y las demás, al deshacer aquel cambio, heredaron el material de su
/// propio órgano. Por eso aparecían como losas de color en vez del aro holográfico.
/// </summary>
public static class MedicalPlatformFix
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string PlatformMat = "Assets/Materials/Holo/Mat_HoloPlatform.mat";

    [MenuItem("MedicalViewer/Step72 - Devolver su material a las plataformas")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var material = AssetDatabase.LoadAssetAtPath<Material>(PlatformMat);
        if (material == null)
        {
            Debug.LogError("[Step72] No encuentro " + PlatformMat);
            return;
        }

        int fixedCount = 0;
        foreach (var renderer in Object.FindObjectsOfType<Renderer>(true))
        {
            if (!renderer.gameObject.name.StartsWith("Holo_Platform")) continue;

            string before = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "sin material";
            if (renderer.sharedMaterial == material)
            {
                sb.AppendLine(renderer.gameObject.name + ": ya era correcto");
                continue;
            }

            renderer.sharedMaterial = material;
            EditorUtility.SetDirty(renderer);
            fixedCount++;
            sb.AppendLine(renderer.gameObject.name + ": " + before + " -> Mat_HoloPlatform");
        }

        sb.AppendLine("plataformas corregidas: " + fixedCount);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        // Foto desde los ojos del usuario con la vista Modelo 3D encendida.
        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        var restore = new System.Collections.Generic.List<(GameObject go, bool active)>();

        if (actions != null)
        {
            var group = new SerializedObject(actions).FindProperty("model3DObjects");
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)
                {
                    restore.Add((go, go.activeSelf));
                    go.SetActive(true);
                }
            }
        }

        if (rootGo != null) MedicalWorkspaceLayout.RenderOverview(rootGo.transform, OutDir + "step72_plataformas.png", sb);

        for (int i = restore.Count - 1; i >= 0; i--) restore[i].go.SetActive(restore[i].active);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step72_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP72_DONE");
    }
}
