using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Dice dónde está cada cosa del puesto de trabajo vista desde el usuario: a qué ángulo
/// queda, cuánto ocupa de ancho y a qué distancia. Sirve para colocar algo nuevo sin que
/// se encime con lo que ya había.
/// </summary>
public static class MedicalSpaceReport
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step75 - Dónde está cada cosa")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject root = GameObject.Find("Medical_Menu_UI");
        if (root == null)
        {
            Debug.LogError("[Step75] No encuentro Medical_Menu_UI.");
            return;
        }

        Vector3 eye = root.transform.position;
        Vector3 forward = root.transform.forward;
        Vector3 right = root.transform.right;

        sb.AppendLine("ojos del usuario: " + eye.ToString("F2"));
        sb.AppendLine("nombre | angulo centro | de ... a ... | distancia | alto");
        sb.AppendLine("---");

        foreach (Transform child in root.transform)
        {
            Bounds? b = WorldBounds(child.gameObject);
            if (b == null) continue;

            sb.AppendLine(Line(child.gameObject, eye, forward, right));
        }

        // Y lo mismo para lo que enciende cada vista del menú, que no siempre cuelga de él.
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var so = new SerializedObject(actions);
            foreach (string field in new[] { "dicomObjects", "segmentationObjects", "model3DObjects" })
            {
                sb.AppendLine();
                sb.AppendLine("== vista " + field + " ==");

                var group = so.FindProperty(field);
                for (int i = 0; i < group.arraySize; i++)
                {
                    if (!(group.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)) continue;
                    sb.AppendLine(Line(go, eye, forward, right));
                }
            }
        }

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step75_espacio.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP75_DONE");
    }

    /// <summary>Una línea del reporte: ángulo que ocupa, distancia y altura.</summary>
    private static string Line(GameObject go, Vector3 eye, Vector3 forward, Vector3 right)
    {
        Bounds? b = WorldBounds(go);
        if (b == null) return go.name + " | (sin geometria)";

        Bounds bounds = b.Value;
        float minAngle = 999f, maxAngle = -999f, near = 9999f, far = 0f;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));

            Vector3 local = corner - eye;
            float depth = Vector3.Dot(local, forward);
            float side = Vector3.Dot(local, right);
            float angle = Mathf.Atan2(side, depth) * Mathf.Rad2Deg;

            minAngle = Mathf.Min(minAngle, angle);
            maxAngle = Mathf.Max(maxAngle, angle);
            near = Mathf.Min(near, depth);
            far = Mathf.Max(far, depth);
        }

        return string.Format("{0} | {1,7:F1} | {2,7:F1} a {3,7:F1} | {4:F2}-{5:F2} m | y {6:F2} a {7:F2}{8}",
            go.name, (minAngle + maxAngle) * 0.5f, minAngle, maxAngle, near, far,
            bounds.min.y - eye.y, bounds.max.y - eye.y,
            go.activeSelf ? "" : "  (apagado)");
    }

    /// <summary>Caja que ocupa el objeto y todo lo que cuelga de él, encendido o no.</summary>
    private static Bounds? WorldBounds(GameObject go)
    {
        Bounds result = default;
        bool any = false;

        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds local = mf.sharedMesh.bounds;
            var box = new Bounds(mf.transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                box.Encapsulate(mf.transform.TransformPoint(corner));
            }
            if (!any) { result = box; any = true; } else result.Encapsulate(box);
        }

        foreach (var rt in go.GetComponentsInChildren<RectTransform>(true))
        {
            if (rt.GetComponent<Canvas>() == null) continue;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            var box = new Bounds(corners[0], Vector3.zero);
            foreach (var c in corners) box.Encapsulate(c);
            if (!any) { result = box; any = true; } else result.Encapsulate(box);
        }

        return any ? result : (Bounds?)null;
    }
}
