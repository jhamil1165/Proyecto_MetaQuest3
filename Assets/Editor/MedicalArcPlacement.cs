using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Coloca los paneles en el arco de verdad.
///
/// Los tres canvas son RectTransform, y ahi la X/Y de localPosition es un valor
/// DERIVADO: se recalcula a partir de m_AnchoredPosition cada vez que se carga la
/// escena. Escribir localPosition parece funcionar mientras la escena sigue abierta y
/// se revierte en silencio al recargarla, que es como los tres paneles acabaron
/// apilados en X=0 con la rotacion correcta pero sin separacion.
///
/// anchoredPosition3D es la propiedad real: su X/Y van a m_AnchoredPosition y su Z a
/// m_LocalPosition.z, que es lo que Unity vuelve a leer.
/// </summary>
public static class MedicalArcPlacement
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private const float Radius = 1.8f;
    private const float MenuAngle = 34f;      // menu a la derecha
    private const float ContentAngle = -20f;  // TC y sistemas a la izquierda

    [MenuItem("MedicalViewer/Step50 - Fix Arc Placement")]
    public static void Apply()
    {
        // Abrir la escena explicitamente: en batchmode no hay ninguna cargada, y
        // SaveCurrentModifiedScenesIfUserWantsTo no puede preguntar nada sin interfaz.
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);

        var sb = new StringBuilder();

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        if (rootGo == null)
        {
            Debug.LogError("[Step50] No encuentro Medical_Menu_UI en la escena abierta.");
            return;
        }

        Transform root = rootGo.transform;

        Place(root, "Canvas_Actions", MenuAngle, sb);
        Place(root, "Canvas_Systems", ContentAngle, sb);
        Place(root, "Canvas_DICOM", ContentAngle, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step50_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP50_DONE");
    }

    private static void Place(Transform root, string name, float angle, StringBuilder sb)
    {
        Transform t = root.Find(name);
        if (t == null)
        {
            sb.AppendLine("[AVISO] " + name + " no encontrado");
            return;
        }

        var rt = t as RectTransform;
        if (rt == null)
        {
            sb.AppendLine("[AVISO] " + name + " no es RectTransform");
            return;
        }

        Undo.RecordObject(rt, "Colocar en arco");

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        Vector3 target = yaw * Vector3.forward * Radius;

        // Anclas al centro: sin esto anchoredPosition se mide contra un borde y el
        // panel se desplaza segun el tamano del padre.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        rt.anchoredPosition3D = target;
        rt.localRotation = yaw;

        EditorUtility.SetDirty(rt);

        // Leer de vuelta: si anchoredPosition sigue sin cuadrar, el fallo es otro.
        sb.AppendLine(name + ": " + angle.ToString("F0") + " grados (" +
                      (angle < 0 ? "izquierda" : "derecha") + ")");
        sb.AppendLine("   anchoredPosition3D=" + rt.anchoredPosition3D.ToString("F3") +
                      "  localPosition=" + rt.localPosition.ToString("F3"));
    }
}
