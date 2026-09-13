using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Comprueba que el visor de tomografia esta entero.
///
/// Tiene muchas piezas que se cablean por codigo (tres columnas, cada una con su
/// RawImage, su deslizador y dos textos) y basta con que una referencia se quede
/// vacia para que la columna salga en negro sin ningun error en consola. Aqui se
/// listan todas y ademas se comprueba que el primer archivo que pedira cada columna
/// exista de verdad en StreamingAssets.
/// </summary>
public static class MedicalCtDiagnose
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step53 - Diagnostico visor TC")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var viewer = Object.FindObjectOfType<CTMultiPlaneViewer>(true);
        if (viewer == null)
        {
            sb.AppendLine("[FALLO] no hay CTMultiPlaneViewer en la escena");
            Dump(sb);
            return;
        }

        var so = new SerializedObject(viewer);
        var planes = so.FindProperty("planes");
        var organs = so.FindProperty("organs");

        sb.AppendLine("CTMultiPlaneViewer en: " + Path(viewer.transform));
        sb.AppendLine("  columnas enlazadas: " + planes.arraySize);
        for (int i = 0; i < planes.arraySize; i++)
        {
            var o = planes.GetArrayElementAtIndex(i).objectReferenceValue;
            sb.AppendLine("    [" + i + "] " + (o == null ? "<VACIO>" : o.name));
        }

        sb.AppendLine("  organos: " + organs.arraySize);
        string firstOrgan = organs.arraySize > 0 ? organs.GetArrayElementAtIndex(0).stringValue : null;
        for (int i = 0; i < organs.arraySize; i++)
            sb.AppendLine("    [" + i + "] " + organs.GetArrayElementAtIndex(i).stringValue);

        sb.AppendLine("  organLabel=" + Ref(so, "organLabel"));
        sb.AppendLine("  previousButton=" + Ref(so, "previousButton"));
        sb.AppendLine("  nextButton=" + Ref(so, "nextButton"));

        sb.AppendLine();
        sb.AppendLine("=== columnas ===");

        foreach (var view in Object.FindObjectsOfType<CTPlaneView>(true))
        {
            var vso = new SerializedObject(view);
            string plane = vso.FindProperty("plane").stringValue;
            int count = vso.FindProperty("sliceCount").intValue;

            sb.AppendLine(view.name + "  plano=" + plane + "  cortes=" + count);
            sb.AppendLine("   display=" + Ref(vso, "display") +
                          "  slider=" + Ref(vso, "slider") +
                          "  title=" + Ref(vso, "title") +
                          "  counter=" + Ref(vso, "counter"));

            // El corte central es el que carga al abrir: si ese archivo no existe,
            // la columna aparece en negro aunque todo lo demas este bien.
            if (!string.IsNullOrEmpty(firstOrgan) && count > 0)
            {
                int slice = count / 2;
                string file = firstOrgan + "_" + plane + "_" + slice.ToString("D4") + ".jpg";
                string full = Application.streamingAssetsPath + "/CT/" + firstOrgan + "/" + plane + "/" + file;
                sb.AppendLine("   primer archivo que pedira: " + file +
                              (System.IO.File.Exists(full) ? "  [EXISTE]" : "  [NO EXISTE]"));
            }
        }

        Dump(sb);
    }

    private static string Ref(SerializedObject so, string field)
    {
        var p = so.FindProperty(field);
        if (p == null) return "<campo inexistente>";
        return p.objectReferenceValue == null ? "VACIO" : "ok";
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }

    private static void Dump(StringBuilder sb)
    {
        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step53_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP53_DONE");
    }
}
