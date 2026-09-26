using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FellowOakDicom;

/// <summary>
/// Parte 2: deja el visor fo-dicom listo para el Meta Quest.
///
/// Configura el DicomVolumeViewer de la escena DICOM_Test para que lea la serie que ahora
/// viaja dentro de la app (StreamingAssets/DICOM/serie_toraxabdomen) en vez de una carpeta
/// del PC, y comprueba aquí mismo que fo-dicom puede abrir esos archivos y que las cuentas
/// de memoria salen: son los tres problemas que tenía para el visor.
/// </summary>
public static class MedicalDicomSetup
{
    private const string DicomScene = "Assets/Scenes/DICOM_Test.unity";
    private const string MainScene = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string Folder = "DICOM/serie_toraxabdomen";

    [MenuItem("MedicalViewer/Step69 - Visor fo-dicom dentro de la app")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var sb = new StringBuilder();

        // ---- 1. Comprobar que fo-dicom lee la serie que va dentro de la app ----
        string root = Application.streamingAssetsPath + "/" + Folder;
        string indexPath = root + "/index.txt";

        if (!System.IO.File.Exists(indexPath))
        {
            Debug.LogError("[Step69] Falta " + indexPath);
            return;
        }

        string[] names = System.IO.File.ReadAllLines(indexPath)
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .ToArray();
        sb.AppendLine("index.txt: " + names.Length + " archivos");

        int rows = 0, cols = 0;
        double first = 0, last = 0;
        foreach (int i in new[] { 0, names.Length / 2, names.Length - 1 })
        {
            var file = DicomFile.Open(root + "/" + names[i]);
            var data = file.Dataset;
            rows = data.GetSingleValue<ushort>(DicomTag.Rows);
            cols = data.GetSingleValue<ushort>(DicomTag.Columns);
            double z = data.GetValues<double>(DicomTag.ImagePositionPatient)[2];
            int instance = data.GetSingleValueOrDefault(DicomTag.InstanceNumber, -1);
            int bytes = FellowOakDicom.Imaging.DicomPixelData.Create(data).GetFrame(0).Data.Length;

            if (i == 0) first = z;
            if (i == names.Length - 1) last = z;

            sb.AppendLine(string.Format("   {0}: {1}x{2}, instancia {3}, z {4:F2} mm, {5} bytes de pixel",
                names[i], cols, rows, instance, z, bytes));
        }

        long memory = (long)rows * cols * names.Length * 2 / 1048576;
        sb.AppendLine(string.Format("orden: el primero esta en z {0:F2} y el ultimo en z {1:F2} ({2})",
            first, last, first < last ? "de pies a cabeza" : "de cabeza a pies"));
        sb.AppendLine("memoria del volumen en short: " + memory + " MB (antes, en float: " + memory * 2 + " MB)");

        // ---- 2. Configurar el visor de la escena DICOM_Test ----
        EditorSceneManager.OpenScene(DicomScene);
        var viewer = Object.FindObjectOfType<DicomVolumeViewer>(true);
        if (viewer == null)
        {
            Debug.LogError("[Step69] No hay DicomVolumeViewer en " + DicomScene);
            return;
        }

        var so = new SerializedObject(viewer);
        so.FindProperty("origen").enumValueIndex = (int)DicomVolumeViewer.Origen.DentroDeLaApp;
        so.FindProperty("streamingFolder").stringValue = Folder;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(viewer);

        sb.AppendLine("DICOM_Test: el visor lee ahora de la app (" + Folder + ")");
        sb.AppendLine("   paneles conectados: axial " + Connected(so, "axialImage") +
                      ", coronal " + Connected(so, "coronalImage") +
                      ", sagital " + Connected(so, "sagittalImage"));
        sb.AppendLine("   sliders conectados: axial " + Connected(so, "axialSlider") +
                      ", coronal " + Connected(so, "coronalSlider") +
                      ", sagital " + Connected(so, "sagittalSlider"));

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        // Dejar abierta la escena principal, que es la que se usa siempre.
        EditorSceneManager.OpenScene(MainScene);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step69_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP69_DONE");
    }

    private static string Connected(SerializedObject so, string field)
    {
        var prop = so.FindProperty(field);
        return prop != null && prop.objectReferenceValue != null ? "si" : "NO";
    }
}
