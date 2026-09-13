using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Coloca la pantalla NDI de leamsi1je como tercera pantalla del puesto de trabajo.
///
/// Estaba suelta en la raiz de la escena, en una posicion fija del mundo, mientras
/// WorkspaceRecenter recoloca Medical_Menu_UI hacia donde mira el usuario al arrancar.
/// Resultado: todo lo demas te sigue y la pantalla NDI se queda donde estaba, casi
/// siempre a la espalda. Colgandola de la raiz del menu viaja con el resto.
///
/// Reparto del arco: el menu al centro como eje, la tomografia a la izquierda (como
/// pidio el usuario) y el video NDI a la derecha, simetrico.
/// </summary>
public static class MedicalNdiScreen
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    private const float MenuAngle = 0f;       // el menu manda, al centro
    private const float CtAngle = -38f;       // tomografia a la izquierda
    private const float NdiAngle = 38f;       // video en directo a la derecha

    private const float CanvasRadius = 1.8f;
    private const float NdiRadius = 1.9f;     // la pantalla es grande: un poco mas lejos

    [MenuItem("MedicalViewer/Step51 - Colocar pantalla NDI")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        if (rootGo == null)
        {
            Debug.LogError("[Step51] No encuentro Medical_Menu_UI.");
            return;
        }

        Transform root = rootGo.transform;

        PlaceCanvas(root, "Canvas_Actions", MenuAngle, sb);
        PlaceCanvas(root, "Canvas_Systems", CtAngle, sb);
        PlaceCanvas(root, "Canvas_DICOM", CtAngle, sb);
        PlaceNdi(root, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step51_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP51_DONE");
    }

    private static void PlaceCanvas(Transform root, string name, float angle, StringBuilder sb)
    {
        var rt = root.Find(name) as RectTransform;
        if (rt == null) { sb.AppendLine("[AVISO] " + name + " no encontrado"); return; }

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);

        // anchoredPosition3D y no localPosition: en un RectTransform la X/Y de
        // localPosition se recalcula desde m_AnchoredPosition al cargar la escena.
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition3D = yaw * Vector3.forward * CanvasRadius;
        rt.localRotation = yaw;

        EditorUtility.SetDirty(rt);
        sb.AppendLine(name + ": " + angle.ToString("F0") + " grados -> " +
                      rt.anchoredPosition3D.ToString("F3"));
    }

    private static void PlaceNdi(Transform root, StringBuilder sb)
    {
        GameObject ndi = GameObject.Find("NDI_Screen");
        if (ndi == null) { sb.AppendLine("[AVISO] NDI_Screen no encontrado"); return; }

        Undo.RecordObject(ndi.transform, "Colocar pantalla NDI");

        Vector3 scaleBefore = ndi.transform.localScale;
        string parentBefore = ndi.transform.parent == null ? "<raiz de la escena>" : ndi.transform.parent.name;

        // worldPositionStays:false conserva la escala local (1.6 x 0.9, formato 16:9)
        // en vez de recalcularla contra el padre nuevo.
        ndi.transform.SetParent(root, false);

        Quaternion yaw = Quaternion.Euler(0f, NdiAngle, 0f);
        ndi.transform.localPosition = yaw * Vector3.forward * NdiRadius;
        ndi.transform.localRotation = yaw;
        ndi.transform.localScale = scaleBefore;

        EditorUtility.SetDirty(ndi.transform);

        sb.AppendLine("NDI_Screen: padre " + parentBefore + " -> " + root.name);
        sb.AppendLine("   " + NdiAngle.ToString("F0") + " grados (derecha)  local=" +
                      ndi.transform.localPosition.ToString("F3") +
                      "  escala=" + ndi.transform.localScale.ToString("F2"));
        sb.AppendLine("   tamano real: " + (scaleBefore.x).ToString("F2") + " x " +
                      (scaleBefore.y).ToString("F2") + " metros");
    }
}
