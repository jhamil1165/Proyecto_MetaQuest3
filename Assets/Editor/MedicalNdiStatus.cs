using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Monta el cartel de "sin señal" de la pantalla NDI.
///
/// El cartel NO cuelga del Quad: ese tiene escala 1,6 x 0,9 para dar el formato 16:9,
/// y un hijo heredaria esa escala no uniforme dejando el texto estirado. Se hace como
/// un canvas hermano con la misma colocacion en el arco que el resto del menu, que ya
/// sabemos que queda orientada hacia el usuario.
/// </summary>
public static class MedicalNdiStatus
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    private const float NdiAngle = 38f;
    private const float LabelRadius = 1.88f;   // 2 cm por delante de la pantalla
    private const float CanvasScale = 0.0016f;

    [MenuItem("MedicalViewer/Step52 - Cartel sin senal NDI")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) { Debug.LogError("[Step52] Falta la fuente TMP."); return; }

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        GameObject ndi = GameObject.Find("NDI_Screen");
        if (rootGo == null || ndi == null)
        {
            Debug.LogError("[Step52] Falta Medical_Menu_UI o NDI_Screen.");
            return;
        }

        Transform root = rootGo.transform;

        // Reconstruir: si ya existe de una pasada anterior, se reemplaza entero.
        Transform old = root.Find("Canvas_NDI_Status");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
            sb.AppendLine("cartel anterior reemplazado");
        }

        Camera cam = Object.FindObjectOfType<Camera>();

        var go = new GameObject("Canvas_NDI_Status", typeof(RectTransform));
        go.transform.SetParent(root, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = cam;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        // Sin GraphicRaycaster a proposito: es un cartel, no debe capturar los rayos
        // ni tapar lo que haya detras.

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(900f, 260f);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Quaternion yaw = Quaternion.Euler(0f, NdiAngle, 0f);
        rt.anchoredPosition3D = yaw * Vector3.forward * LabelRadius;
        rt.localRotation = yaw;
        rt.localScale = Vector3.one * CanvasScale;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;

        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = 46f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.62f, 0.70f, 0.78f, 1f);
        tmp.text = "NDI · SIN SEÑAL";
        tmp.enableWordWrapping = false;

        // Cablear el componente de estado en la pantalla.
        var status = ndi.GetComponent<NdiScreenStatus>();
        if (status == null) status = Undo.AddComponent<NdiScreenStatus>(ndi);

        var so = new SerializedObject(status);
        so.FindProperty("screenRenderer").objectReferenceValue = ndi.GetComponent<Renderer>();
        so.FindProperty("statusLabel").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(status);

        sb.AppendLine("Canvas_NDI_Status creado en " + NdiAngle.ToString("F0") + " grados");
        sb.AppendLine("   anchoredPosition3D=" + rt.anchoredPosition3D.ToString("F3"));
        sb.AppendLine("   tamano real: " + (900f * CanvasScale).ToString("F2") + " x " +
                      (260f * CanvasScale).ToString("F2") + " metros");
        sb.AppendLine("NdiScreenStatus cableado:");
        sb.AppendLine("   screenRenderer=" + (ndi.GetComponent<Renderer>() != null ? "ok" : "FALTA"));
        sb.AppendLine("   statusLabel=ok");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step52_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP52_DONE");
    }
}
