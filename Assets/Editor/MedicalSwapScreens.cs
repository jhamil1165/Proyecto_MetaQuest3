using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Intercambia el visor de tomografia y la pantalla NDI.
///
/// La tomografia es lo que el usuario quiere ver en grande y en el sitio principal,
/// que es donde estaba la pantalla NDI. El video en directo pasa al lado contrario:
/// no se quita, porque es el trabajo de otra integrante del grupo, pero deja de
/// ocupar el mejor hueco cuando ni siquiera hay emisor.
/// </summary>
public static class MedicalSwapScreens
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private const float CtAngle = 38f;     // tomografia al sitio principal
    private const float NdiAngle = -38f;   // video en directo al otro lado
    private const float CanvasRadius = 1.8f;
    private const float NdiRadius = 1.9f;

    [MenuItem("MedicalViewer/Step56 - Tomografia a la pantalla grande")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        if (rootGo == null) { Debug.LogError("[Step56] Falta Medical_Menu_UI."); return; }
        Transform root = rootGo.transform;

        PlaceCanvas(root, "Canvas_DICOM", CtAngle, CanvasRadius, sb);
        PlaceCanvas(root, "Canvas_Systems", NdiAngle, CanvasRadius, sb);
        PlaceCanvas(root, "Canvas_NDI_Status", NdiAngle, 1.88f, sb);
        PlaceTransform(root, "NDI_Screen", NdiAngle, NdiRadius, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step56_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP56_DONE");
    }

    private static void PlaceCanvas(Transform root, string name, float angle, float radius, StringBuilder sb)
    {
        var rt = root.Find(name) as RectTransform;
        if (rt == null) { sb.AppendLine("[AVISO] " + name + " no encontrado"); return; }

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        // anchoredPosition3D y no localPosition: en un RectTransform la X/Y de
        // localPosition se recalcula desde m_AnchoredPosition al cargar la escena.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = yaw * Vector3.forward * radius;
        rt.localRotation = yaw;

        EditorUtility.SetDirty(rt);

        var size = rt.sizeDelta * rt.localScale.x;
        sb.AppendLine(name + ": " + angle.ToString("F0") + " grados (" +
                      (angle < 0 ? "izquierda" : "derecha") + ")  " +
                      size.x.ToString("F2") + " x " + size.y.ToString("F2") + " m");
    }

    private static void PlaceTransform(Transform root, string name, float angle, float radius, StringBuilder sb)
    {
        Transform t = root.Find(name);
        if (t == null) { sb.AppendLine("[AVISO] " + name + " no encontrado"); return; }

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        t.localPosition = yaw * Vector3.forward * radius;
        t.localRotation = yaw;

        EditorUtility.SetDirty(t);
        sb.AppendLine(name + ": " + angle.ToString("F0") + " grados (" +
                      (angle < 0 ? "izquierda" : "derecha") + ")");
    }
}
