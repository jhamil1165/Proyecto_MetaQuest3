using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// De dónde sale el color de cada órgano.
///
/// Al pasar los órganos al shader de corte perdieron su color: los cinco materiales
/// nuevos salieron del mismo gris, así que el color no estaba en el material. Este paso
/// devuelve a cada órgano el material que traía su prefab (deshaciendo el cambio anterior)
/// y luego informa de dónde viene realmente el color: del material, de una textura o de
/// los colores por vértice de la malla, que es como Slicer suele exportar.
/// </summary>
public static class MedicalOrganMaterialCheck
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step68 - De donde sale el color de los organos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var systems = Object.FindObjectOfType<OrganSystemsPanel>(true);
        if (systems == null)
        {
            Debug.LogError("[Step68] Falta OrganSystemsPanel.");
            return;
        }

        var rows = new SerializedObject(systems).FindProperty("rows");
        for (int i = 0; i < rows.arraySize; i++)
        {
            var row = rows.GetArrayElementAtIndex(i);
            var organ = row.FindPropertyRelative("target").objectReferenceValue as GameObject;
            if (organ == null) continue;

            sb.AppendLine("=== " + row.FindPropertyRelative("label").stringValue + " (" + organ.name + ") ===");

            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(true))
            {
                sb.AppendLine("  renderer " + Path(renderer.transform) + ": " + Describe(renderer.sharedMaterial));

                // Deshacer el cambio de material: vuelve el que trae el prefab del modelo.
                if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                {
                    var so = new SerializedObject(renderer);
                    var prop = so.FindProperty("m_Materials");
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
                    so.Update();
                    sb.AppendLine("     material original restaurado: " + Describe(renderer.sharedMaterial));
                }

                var filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                if (mesh != null)
                {
                    var colors = mesh.colors32;
                    string sample = colors != null && colors.Length > 0
                        ? string.Format("si, {0} vertices, ejemplo rgb({1}, {2}, {3})",
                            colors.Length, colors[0].r, colors[0].g, colors[0].b)
                        : "no";
                    sb.AppendLine("     colores por vertice en la malla: " + sample);
                    sb.AppendLine("     materiales del renderer: " + renderer.sharedMaterials.Length);
                }
            }
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step68_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP68_DONE");
    }

    private static string Describe(Material material)
    {
        if (material == null) return "sin material";

        string color = "sin color";
        if (material.HasProperty("_BaseColor")) color = "_BaseColor " + material.GetColor("_BaseColor").ToString("F2");
        else if (material.HasProperty("_Color")) color = "_Color " + material.GetColor("_Color").ToString("F2");

        string texture = material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null
            ? ", textura " + material.GetTexture("_BaseMap").name
            : "";

        return material.name + " [" + material.shader.name + "] " + color + texture;
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
