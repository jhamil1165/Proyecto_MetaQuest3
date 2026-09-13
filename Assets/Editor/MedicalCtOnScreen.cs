using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Pone la tomografia EN la pantalla grande, que es donde el usuario la quiere ver.
///
/// El visor se coloca 3 cm por delante del monitor, centrado y con el mismo giro, y a
/// una escala que lo deja dentro del marco de 1,6 x 0,9 m: asi el formato 16:9 de la
/// pantalla de la otra integrante no se toca y la TC se ve "dentro" del monitor.
///
/// Ademas se le dan tres imagenes reales de vista previa (el corte central de cada
/// plano). Sin ellas, fuera de Play las RawImage no tienen textura y todo se ve en
/// blanco, que es justo lo que llevaba confundiendo toda la tarde. En Play, CTPlaneView
/// sustituye la vista previa por el corte que toque; no la destruye, porque solo
/// libera las texturas que carga el mismo.
/// </summary>
public static class MedicalCtOnScreen
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string PreviewDir = "Assets/UI/CTPreview";

    private const float InFront = 0.03f;       // metros por delante del monitor
    private const float CanvasScale = 0.00118f; // tarjeta 1060x700 -> 1,25 x 0,83 m

    [MenuItem("MedicalViewer/Step57 - Tomografia en la pantalla grande")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rootGo = GameObject.Find("Medical_Menu_UI");
        if (rootGo == null) { Debug.LogError("[Step57] Falta Medical_Menu_UI."); return; }
        Transform root = rootGo.transform;

        Transform screen = root.Find("NDI_Screen");
        var dicom = root.Find("Canvas_DICOM") as RectTransform;
        if (screen == null || dicom == null)
        {
            Debug.LogError("[Step57] Falta NDI_Screen o Canvas_DICOM bajo Medical_Menu_UI.");
            return;
        }

        // Angulo y radio reales del monitor, leidos de la escena en vez de suponerlos.
        Vector3 p = screen.localPosition;
        float angle = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
        float radius = new Vector2(p.x, p.z).magnitude;
        sb.AppendLine("monitor: " + angle.ToString("F1") + " grados, radio " + radius.ToString("F2") +
                      " m, tamano " + screen.localScale.x.ToString("F2") + " x " + screen.localScale.y.ToString("F2") + " m");

        Undo.RecordObject(dicom, "TC en pantalla");
        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        dicom.anchorMin = dicom.anchorMax = new Vector2(0.5f, 0.5f);
        dicom.pivot = new Vector2(0.5f, 0.5f);
        // anchoredPosition3D y no localPosition: en un RectTransform la X/Y de
        // localPosition se recalcula desde m_AnchoredPosition al cargar la escena.
        dicom.anchoredPosition3D = yaw * Vector3.forward * (radius - InFront) + Vector3.up * p.y;
        dicom.localRotation = yaw;
        dicom.localScale = Vector3.one * CanvasScale;
        EditorUtility.SetDirty(dicom);

        // Visible tambien fuera de Play. En ejecucion MedicalMenuActions.Start decide
        // igualmente que vista se enciende, asi que esto no cambia el comportamiento.
        dicom.gameObject.SetActive(true);

        sb.AppendLine("Canvas_DICOM: " + angle.ToString("F1") + " grados, " + InFront * 100f + " cm por delante, tarjeta " +
                      (1060f * CanvasScale).ToString("F2") + " x " + (700f * CanvasScale).ToString("F2") + " m");

        // El cartel de "sin senal" quedaria debajo de la TC: se apaga (no se borra).
        Transform status = root.Find("Canvas_NDI_Status");
        if (status != null)
        {
            status.gameObject.SetActive(false);
            sb.AppendLine("Canvas_NDI_Status: desactivado (la pantalla ahora muestra la TC)");
        }

        AssignPreviews(sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step57_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP57_DONE");
    }

    private static void AssignPreviews(StringBuilder sb)
    {
        var viewer = Object.FindObjectOfType<CTMultiPlaneViewer>(true);
        if (viewer == null) { sb.AppendLine("[AVISO] sin CTMultiPlaneViewer, no hay vista previa"); return; }

        var organs = new SerializedObject(viewer).FindProperty("organs");
        if (organs.arraySize == 0) { sb.AppendLine("[AVISO] lista de organos vacia"); return; }
        string organ = organs.GetArrayElementAtIndex(0).stringValue;

        System.IO.Directory.CreateDirectory(PreviewDir);

        foreach (var view in Object.FindObjectsOfType<CTPlaneView>(true))
        {
            var vso = new SerializedObject(view);
            string plane = vso.FindProperty("plane").stringValue;
            int count = vso.FindProperty("sliceCount").intValue;
            var raw = vso.FindProperty("display").objectReferenceValue as RawImage;

            if (raw == null || count <= 0) { sb.AppendLine("[AVISO] " + view.name + " sin RawImage"); continue; }

            // El mismo corte con el que arranca CTPlaneView.SetOrgan: el central.
            string file = organ + "_" + plane + "_" + (count / 2).ToString("D4") + ".jpg";
            string src = Application.streamingAssetsPath + "/CT/" + organ + "/" + plane + "/" + file;
            string dst = PreviewDir + "/Preview_" + plane + ".jpg";

            if (!System.IO.File.Exists(src)) { sb.AppendLine("[AVISO] no existe " + src); continue; }

            System.IO.File.Copy(src, dst, true);
            AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(dst) as TextureImporter;
            if (importer != null)
            {
                importer.mipmapEnabled = false;          // es UI: sin mipmaps se ve nitido
                // Los cortes miden 298x268. Por defecto Unity reescala lo que no es
                // potencia de dos y los dejaba en 256x256, perdiendo nitidez.
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
            Undo.RecordObject(raw, "Vista previa TC");
            raw.texture = tex;
            EditorUtility.SetDirty(raw);

            sb.AppendLine(view.name + ": vista previa " + file + (tex != null ? "  [ok " + tex.width + "x" + tex.height + "]" : "  [FALLO al importar]"));
        }
    }
}
