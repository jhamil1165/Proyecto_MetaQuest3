using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hace que la aplicacion arranque limpia: solo el menu.
///
/// Arrancaba directamente en la vista de Segmentacion, asi que nada mas ponerse el visor
/// aparecian los paneles de tomografia, el plano de corte y el panel de control, sin que
/// nadie hubiera elegido nada. Ahora se entra al menu y cada vista se abre cuando se pulsa.
///
/// De paso se guarda el plano de corte con lo suyo apagado. En ejecucion ya se apaga solo en
/// cuanto arranca su script, pero guardarlo encendido significa que se ve durante el primer
/// fotograma, y que en el editor aparece siempre aunque no se este cortando.
/// </summary>
public static class MedicalStartClean
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step93 - Arrancar solo con el menu")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        // ---- vista inicial ----
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var so = new SerializedObject(actions);
            var view = so.FindProperty("initialView");

            string[] names = { "SoloMenu", "Modelo3D", "DICOM", "Segmentacion" };
            int before = view.enumValueIndex;

            view.enumValueIndex = 0;   // SoloMenu
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actions);

            sb.AppendLine("vista inicial: " + Describe(names, before) + " -> SoloMenu");
        }
        else
        {
            sb.AppendLine("[FALLO] no encuentro MedicalMenuActions");
        }

        // ---- el plano de corte, guardado apagado ----
        GameObject plane = GameObject.Find("Plano_de_corte");
        if (plane == null)
        {
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
            {
                if (t.name == "Plano_de_corte") { plane = t.gameObject; break; }
            }
        }

        if (plane != null)
        {
            int off = 0;
            foreach (var renderer in plane.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled) continue;

                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
                off++;
                sb.AppendLine("apagado en la escena: " + renderer.name);
            }

            var collider = plane.GetComponent<Collider>();
            if (collider != null && collider.enabled)
            {
                collider.enabled = false;
                EditorUtility.SetDirty(collider);
                sb.AppendLine("apagado en la escena: collider del plano");
            }

            sb.AppendLine("plano de corte: " + off + " cosas apagadas; se encienden con 'Activar corte'");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step93_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP93_DONE");
    }

    private static string Describe(string[] names, int index)
    {
        return index >= 0 && index < names.Length ? names[index] : "valor " + index;
    }
}
