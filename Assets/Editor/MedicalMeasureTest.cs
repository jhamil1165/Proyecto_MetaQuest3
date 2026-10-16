using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Comprueba que una medida 3D se queda colgada de su órgano y lo acompaña al moverlo.
///
/// Hace a mano lo que haría el usuario: clava dos puntos sobre el hígado, mueve el hígado
/// 30 cm y lo gira 45 grados, y mira si los puntos se han ido con él sin cambiar de sitio
/// sobre el órgano. También mide las bolitas, que es lo que se rompería si no se deshiciera
/// la escala del órgano. No guarda nada: deja la escena como estaba.
/// </summary>
public static class MedicalMeasureTest
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step97 - Probar medidas 3D hijas del organo")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var tool = Object.FindObjectOfType<MeasureTool>(true);
        if (tool == null) { Debug.LogError("[Step97] No hay MeasureTool."); return; }

        var organsField = typeof(MeasureTool).GetField("organs", BindingFlags.Instance | BindingFlags.NonPublic);
        var organs = (MeasureTool.OrganScale[])organsField.GetValue(tool);

        MeasureTool.OrganScale liver = null;
        foreach (var o in organs) if (o != null && o.label == "Hígado") liver = o;
        if (liver == null || liver.root == null) { Debug.LogError("[Step97] No encuentro el hígado."); return; }

        // Dos puntos de verdad sobre la superficie del hígado.
        var renderer = liver.root.GetComponentInChildren<Renderer>();
        Bounds b = renderer.bounds;
        Vector3 p1 = b.center + new Vector3(-b.extents.x * 0.5f, b.extents.y * 0.3f, 0f);
        Vector3 p2 = b.center + new Vector3(b.extents.x * 0.5f, -b.extents.y * 0.2f, 0f);

        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo begin = typeof(MeasureTool).GetMethod("Begin", Private);
        MethodInfo finish = typeof(MeasureTool).GetMethod("Finish", Private);

        object m = begin.Invoke(tool, new object[] { liver, p1 });
        finish.Invoke(tool, new object[] { m, p2 });

        var mType = m.GetType();
        var root = (Transform)mType.GetField("root").GetValue(m);
        var a = (Transform)mType.GetField("a").GetValue(m);
        var bm = (Transform)mType.GetField("b").GetValue(m);
        var label = (Transform)mType.GetField("label").GetValue(m);

        bool child = root.IsChildOf(liver.root);
        sb.AppendLine("la medida cuelga del higado: " + (child ? "SI" : "NO") + "  (" + Path(root) + ")");
        sb.AppendLine(string.Format("escala del higado: {0:F1}  ->  escala de la medida en el mundo: {1:F3}",
            liver.root.lossyScale.x, root.lossyScale.x));
        sb.AppendLine(string.Format("bolita: {0:F1} mm de diametro en el mundo (deberia ser 14)",
            a.lossyScale.x * 1000f));

        var text = label != null ? label.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
        sb.AppendLine("numero que se ve: " + (text != null ? text.text : "(sin numero)"));

        // Donde caen los puntos respecto del higado, antes de moverlo.
        Vector3 la1 = liver.root.InverseTransformPoint(a.position);
        Vector3 lb1 = liver.root.InverseTransformPoint(bm.position);
        Vector3 worldBefore = a.position;

        // Mover y girar el higado, como al agarrarlo con la mano.
        Vector3 pos0 = liver.root.position;
        Quaternion rot0 = liver.root.rotation;
        liver.root.position += new Vector3(0.30f, 0.05f, -0.10f);
        liver.root.rotation = Quaternion.AngleAxis(45f, Vector3.up) * liver.root.rotation;

        Vector3 la2 = liver.root.InverseTransformPoint(a.position);
        Vector3 lb2 = liver.root.InverseTransformPoint(bm.position);
        float drift = Mathf.Max((la2 - la1).magnitude, (lb2 - lb1).magnitude) * liver.root.lossyScale.x * 1000f;
        float moved = (a.position - worldBefore).magnitude * 1000f;

        sb.AppendLine(string.Format("al mover el higado 32 cm y girarlo 45 grados: la medida se movio {0:F0} mm", moved));
        sb.AppendLine(string.Format("  y sobre el organo se desplazo {0:F3} mm  ->  {1}",
            drift, drift < 0.01f ? "SE QUEDA EN SU SITIO" : "SE HA SOLTADO"));

        // Dejarlo todo como estaba.
        liver.root.SetPositionAndRotation(pos0, rot0);
        Object.DestroyImmediate(root.gameObject);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step97_medidas.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP97_DONE");
    }

    private static string Path(Transform t)
    {
        string p = t.name;
        for (Transform q = t.parent; q != null; q = q.parent) p = q.name + "/" + p;
        return p;
    }
}
