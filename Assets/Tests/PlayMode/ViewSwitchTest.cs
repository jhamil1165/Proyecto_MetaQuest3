using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Arranca la escena de verdad y cambia de vista como lo haría el usuario, para ver qué queda
/// encendido de las otras vistas. Escribe la lista en Logs/EditorAutomation/vistas.txt.
/// </summary>
public class ViewSwitchTest
{
    [UnityTest]
    public IEnumerator CambiarDeVista()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        for (int i = 0; i < 30; i++) yield return null;

        var type = System.Type.GetType("MedicalMenuActions, Assembly-CSharp");
        var actions = Object.FindObjectOfType(type, true);
        var sb = new StringBuilder();
        sb.AppendLine(Snapshot("ARRANQUE"));

        type.GetMethod("Open3DModel").Invoke(actions, null);
        for (int i = 0; i < 30; i++) yield return null;
        foreach (var b in Object.FindObjectsOfType<UnityEngine.UI.Button>())
        {
            string branch = Top(b.transform);
            if (!branch.Contains("Canvas_Controles") && !branch.Contains("Canvas_Systems")) continue;
            sb.AppendLine("pulso " + branch + " / " + b.name);
            b.onClick.Invoke();
            for (int i = 0; i < 5; i++) yield return null;
        }
        sb.AppendLine(Snapshot("3D TRAS PULSAR TODO"));

        foreach (string step in new[] { "OpenDICOM", "OpenSegmentation", "OpenDICOM" })
        {
            type.GetMethod(step).Invoke(actions, null);
            for (int i = 0; i < 30; i++) yield return null;
            sb.AppendLine(Snapshot(step));
        }

        System.IO.Directory.CreateDirectory("Logs/EditorAutomation");
        System.IO.File.WriteAllText("Logs/EditorAutomation/vistas.txt", sb.ToString());
        Assert.Pass();
    }

    private static string Snapshot(string label)
    {
        var sb = new StringBuilder("== " + label + " ==\n");
        var seen = new HashSet<string>();
        foreach (var r in Object.FindObjectsOfType<Renderer>())
        {
            if (!r.enabled || !r.isVisible && false) continue;
            if (!r.enabled) continue;
            string p = Top(r.transform);
            if (p.Contains("Camera Rig")) continue;
            if (seen.Add(p)) sb.AppendLine("  " + p + "   (" + r.GetType().Name + " " + r.name + ")");
        }
        foreach (var c in Object.FindObjectsOfType<Canvas>())
        {
            if (!c.enabled || !c.isRootCanvas) continue;
            string p = Top(c.transform);
            if (p.Contains("Camera Rig")) continue;
            if (seen.Add(p)) sb.AppendLine("  " + p + "   (Canvas " + c.name + ")");
        }
        return sb.ToString();
    }

    // Rama hasta el hijo directo de Medical_Menu_UI, o la raiz.
    private static string Top(Transform t)
    {
        var chain = new List<string>();
        for (var p = t; p != null; p = p.parent) chain.Insert(0, p.name);
        int menu = chain.IndexOf("Medical_Menu_UI");
        if (menu >= 0 && menu + 1 < chain.Count) return "Menu/" + chain[menu + 1];
        return chain.Count > 1 ? chain[0] + "/" + chain[1] : chain[0];
    }
}
