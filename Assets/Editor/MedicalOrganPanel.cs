using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Devuelve el panel que enciende y apaga cada órgano.
///
/// No se había borrado: seguía en la escena con sus cinco filas enganchadas, pero con el
/// objeto apagado y fuera de todas las vistas del menú, así que no había forma de llegar a
/// él. Aquí se enciende, se coloca donde no estorbe y se mete en la vista Modelo 3D.
///
/// Se pone a la derecha porque en esa vista ese lado está libre (la pantalla NDI solo sale
/// en DICOM) y porque el volumen ocupa la izquierda: así el puesto queda equilibrado.
///
/// De paso se limpian los huecos vacíos que fueron quedando en las listas del menú al ir
/// añadiendo cosas.
/// </summary>
public static class MedicalOrganPanel
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string PanelName = "Canvas_Systems";
    private const string ReferenceName = "Canvas_DICOM";

    // Sitio nuevo: a la derecha, a la misma altura que el menú.
    // A 48 grados y 1,5 m se metia encima de la vesicula, que llega hasta los 37,7.
    // Mas girado y mas lejos: ocupa menos angulo y deja hueco de sobra.
    private const float Angle = 58f;
    private const float Distance = 1.8f;

    [MenuItem("MedicalViewer/Step80 - Devolver el panel de organos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject root = GameObject.Find("Medical_Menu_UI");
        var panelComponent = Object.FindObjectOfType<OrganSystemsPanel>(true);
        if (root == null || panelComponent == null)
        {
            Debug.LogError("[Step80] Falta Medical_Menu_UI o el OrganSystemsPanel.");
            return;
        }

        // El canvas es el antecesor que cuelga directamente del menú.
        Transform panel = panelComponent.transform;
        while (panel.parent != null && panel.parent != root.transform) panel = panel.parent;

        if (panel.name != PanelName)
            sb.AppendLine("[NOTA] el panel cuelga de '" + panel.name + "', no de " + PanelName);

        panel.gameObject.SetActive(true);
        sb.AppendLine("panel encendido: " + panel.name);

        // ---- colocarlo ----
        var rect = panel.GetComponent<RectTransform>();
        Vector3 place = new Vector3(
            Distance * Mathf.Sin(Angle * Mathf.Deg2Rad), 0f,
            Distance * Mathf.Cos(Angle * Mathf.Deg2Rad));

        if (rect != null) rect.anchoredPosition3D = place;
        else panel.localPosition = place;

        // La orientación se copia del convenio que ya usan los demás paneles, en vez de
        // suponerlo: se mira cuánto gira uno que está bien puesto respecto de su ángulo.
        float offset = 0f;
        Transform reference = root.transform.Find(ReferenceName);
        if (reference != null)
        {
            Vector3 p = reference.localPosition;
            float referenceAngle = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            offset = Mathf.DeltaAngle(referenceAngle, reference.localEulerAngles.y);
            sb.AppendLine(string.Format("referencia {0}: esta a {1:F1} grados y gira {2:F1}; desfase {3:F1}",
                ReferenceName, referenceAngle, reference.localEulerAngles.y, offset));
        }

        panel.localRotation = Quaternion.Euler(0f, Angle + offset, 0f);
        sb.AppendLine(string.Format("colocado a {0:F0} grados, {1:F2} m, girando {2:F1}",
            Angle, Distance, Angle + offset));

        EditorUtility.SetDirty(panel.gameObject);

        // ---- que el menú lo encienda en Modelo 3D ----
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var so = new SerializedObject(actions);
            AddOnce(so.FindProperty("model3DObjects"), panel.gameObject, sb);

            foreach (string field in new[] { "dicomObjects", "segmentationObjects", "model3DObjects" })
            {
                int removed = Compact(so.FindProperty(field));
                if (removed > 0) sb.AppendLine("lista " + field + ": " + removed + " huecos vacios quitados");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actions);
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step80_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP80_DONE");
    }

    private static void AddOnce(SerializedProperty list, GameObject go, StringBuilder sb)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == go)
            {
                sb.AppendLine("el menu ya lo encendia");
                return;
            }
        }

        list.arraySize += 1;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = go;
        sb.AppendLine("añadido a la vista Modelo 3D");
    }

    /// <summary>Quita los huecos vacios de una lista, conservando el orden.</summary>
    private static int Compact(SerializedProperty list)
    {
        int write = 0;
        int removed = 0;

        for (int read = 0; read < list.arraySize; read++)
        {
            Object value = list.GetArrayElementAtIndex(read).objectReferenceValue;
            if (value == null) { removed++; continue; }

            list.GetArrayElementAtIndex(write).objectReferenceValue = value;
            write++;
        }

        list.arraySize = write;
        return removed;
    }
}
