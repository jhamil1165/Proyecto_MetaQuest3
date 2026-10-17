using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Revisa qué se vería al arrancar la app: la vista inicial del menú, qué objetos lleva
/// cada vista, y qué cosas visibles de la escena no pertenecen a ninguna vista ni al menú
/// (esas se quedan a la vista desde el primer momento). No cambia nada.
/// </summary>
public static class MedicalStartupAudit
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step101 - Revisar que se ve al arrancar")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions == null) { Debug.LogError("[Step101] No hay MedicalMenuActions"); return; }

        var so = new SerializedObject(actions);
        sb.AppendLine("MedicalMenuActions en " + Branch(actions.transform) + (actions.isActiveAndEnabled ? "" : " (APAGADO)"));
        sb.AppendLine("vista inicial: " + so.FindProperty("initialView").enumNames[so.FindProperty("initialView").enumValueIndex]);

        var menuRoot = so.FindProperty("menuRoot").objectReferenceValue as GameObject;
        sb.AppendLine("menu: " + (menuRoot != null ? Branch(menuRoot.transform) : "(sin asignar)"));

        var covered = new HashSet<Transform>();
        if (menuRoot != null) covered.Add(menuRoot.transform);

        foreach (string group in new[] { "dicomObjects", "segmentationObjects", "model3DObjects" })
        {
            var list = so.FindProperty(group);
            sb.AppendLine(group + ":");
            for (int i = 0; i < list.arraySize; i++)
            {
                var go = list.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (go == null) { sb.AppendLine("   (vacio)"); continue; }
                covered.Add(go.transform);
                sb.AppendLine("   " + Branch(go.transform) + (go.activeSelf ? "" : "  [apagado en escena]"));
            }
        }

        // Lo que se ve y no cuelga de nada de lo anterior.
        sb.AppendLine("VISIBLE AL ARRANCAR Y FUERA DE TODA VISTA:");
        var reported = new HashSet<Transform>();
        foreach (var r in Object.FindObjectsOfType<Renderer>())
            Check(r.transform, r.enabled, covered, reported, sb, r.GetType().Name);
        foreach (var c in Object.FindObjectsOfType<Canvas>())
            if (c.isRootCanvas) Check(c.transform, c.enabled, covered, reported, sb, "Canvas");

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step101_output.txt", sb.ToString());
        Debug.Log("STEP101_DONE");
    }

    private static void Check(Transform t, bool enabled, HashSet<Transform> covered, HashSet<Transform> reported,
                              StringBuilder sb, string kind)
    {
        if (!enabled || !t.gameObject.activeInHierarchy) return;
        for (Transform p = t; p != null; p = p.parent)
            if (covered.Contains(p)) return;

        // Se informa por el objeto de primer nivel bajo la raíz, para no listar mil hijos.
        Transform top = t;
        while (top.parent != null && top.parent.parent != null) top = top.parent;
        if (!reported.Add(top)) return;
        sb.AppendLine("   " + Branch(top) + "   (p.ej. " + kind + " en " + t.name + ")");
    }

    private static string Branch(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
