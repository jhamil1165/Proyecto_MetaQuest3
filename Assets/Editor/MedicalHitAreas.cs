using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Agranda los botones a los que cuesta acertar con el mando.
///
/// A metro y medio o dos metros, un botón se mide por el ángulo que ocupa, no por los
/// milímetros que tiene: apuntar con un mando tiene un temblor de más o menos un grado, así
/// que por debajo de unos dos grados y medio se falla constantemente. Las flechas de cambiar
/// de corte ocupaban 1,3 grados y el enlace "Ocultar todo" 1,8: se pulsaba al lado.
///
/// Se agranda el rectángulo, no el dibujo: el icono se queda del mismo tamaño y lo que crece
/// es la zona que responde.
/// </summary>
public static class MedicalHitAreas
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    /// <summary>Ángulo mínimo cómodo, en grados.</summary>
    private const float MinDegrees = 2.6f;

    [MenuItem("MedicalViewer/Step87 - Agrandar los botones dificiles de pulsar")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Canvas.ForceUpdateCanvases();
        foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
        {
            var rect = canvas.GetComponent<RectTransform>();
            if (rect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        GameObject root = GameObject.Find("Medical_Menu_UI");
        Vector3 eye = root != null ? root.transform.position : Vector3.zero;
        int grown = 0;

        foreach (var button in Object.FindObjectsOfType<Button>(true))
        {
            var rect = button.GetComponent<RectTransform>();
            if (rect == null) continue;

            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            float height = Vector3.Distance(corners[0], corners[1]);
            float distance = Vector3.Distance((corners[0] + corners[2]) * 0.5f, eye);
            if (distance < 0.01f || height <= 0f) continue;

            float degrees = 2f * Mathf.Atan2(height * 0.5f, distance) * Mathf.Rad2Deg;
            if (degrees >= MinDegrees) continue;

            float factor = MinDegrees / degrees;

            // Alto siempre; ancho sólo si también se queda corto, para no deformar filas.
            float width = Vector3.Distance(corners[0], corners[3]);
            float degreesW = 2f * Mathf.Atan2(width * 0.5f, distance) * Mathf.Rad2Deg;

            Vector2 size = rect.sizeDelta;
            float before = size.y;
            size.y *= factor;
            if (degreesW < MinDegrees) size.x *= MinDegrees / degreesW;
            rect.sizeDelta = size;

            // Si lo coloca un layout, el rectángulo solo no basta.
            var element = button.GetComponent<LayoutElement>();
            if (element != null)
            {
                if (element.preferredHeight > 0f) element.preferredHeight *= factor;
                if (element.minHeight > 0f) element.minHeight *= factor;
            }

            EditorUtility.SetDirty(rect);
            grown++;
            sb.AppendLine(string.Format("{0}: {1:F1} grados de alto -> {2:F1}  (de {3:F0} a {4:F0} px)",
                button.name, degrees, degrees * factor, before, size.y));
        }

        sb.AppendLine("botones agrandados: " + grown);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step87_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP87_DONE");
    }
}
