using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Simula lo que devolvería Wit (texto en español, con tildes, mayúsculas y palabras de
/// relleno) y comprueba que la app hace lo que se pidió. No necesita visor ni internet:
/// se salta el micrófono y se entrega el texto directamente. Escribe el resultado en
/// Logs/EditorAutomation/voz.txt.
/// </summary>
public class VoiceCommandsTest
{
    [UnityTest]
    public IEnumerator FrasesEnEspanol()
    {
        yield return SceneManager.LoadSceneAsync("SampleScene");
        for (int i = 0; i < 30; i++) yield return null;

        var voiceType = System.Type.GetType("VoiceCommands, Assembly-CSharp");
        var voice = (MonoBehaviour)Object.FindObjectOfType(voiceType, true);
        Assert.IsNotNull(voice, "no hay VoiceCommands en la escena");
        MethodInfo hear = voiceType.GetMethod("OnTranscription", BindingFlags.Instance | BindingFlags.NonPublic);

        var menuType = System.Type.GetType("MedicalMenuActions, Assembly-CSharp");
        var cutType = System.Type.GetType("ClippingPlaneInput, Assembly-CSharp");
        var cut = Object.FindObjectOfType(cutType, true);
        FieldInfo cutting = cutType.GetField("cutting", BindingFlags.Instance | BindingFlags.NonPublic);

        var sb = new StringBuilder();
        int ok = 0, total = 0;

        IEnumerator Say(string text)
        {
            hear.Invoke(voice, new object[] { text });
            for (int i = 0; i < 10; i++) yield return null;
        }

        void Check(string what, bool passed)
        {
            total++;
            if (passed) ok++;
            sb.AppendLine((passed ? "OK    " : "FALLA ") + what);
        }

        yield return Say("Abre la vista DICOM, por favor");
        Check("'abre la vista DICOM' -> visor DICOM a la vista", Active("Canvas_DICOM"));

        var plane = FindPlane("axial");
        int before = Slice(plane);
        yield return Say("Siguiente corte");
        Check("'siguiente corte' -> el axial avanza (" + before + " -> " + Slice(plane) + ")", Slice(plane) > before);

        yield return Say("Modelo 3D");
        Check("'modelo 3D' -> vista Modelo 3D", Active("Volumen_3D"));

        yield return Say("Muéstrame el HÍGADO");
        var liver = GameObject.Find("Organs_Slicer/Hígado");
        Check("'muéstrame el HÍGADO' -> hígado visible", liver != null && liver.activeInHierarchy);

        yield return Say("activar corte");
        Check("'activar corte' -> corte encendido", (bool)cutting.GetValue(cut));

        yield return Say("quitar corte");
        Check("'quitar corte' -> corte apagado (gana la frase más larga)", !(bool)cutting.GetValue(cut));

        yield return Say("ocultar hígado");
        Check("'ocultar hígado' -> hígado oculto", liver == null || !liver.activeInHierarchy);

        yield return Say("Segmentación");
        Check("'segmentación' -> vista Segmentación", Active("Canvas_SegClean") || Active("Canvas_SegPainted"));

        yield return Say("hola qué tal");
        Check("frase sin comando -> no rompe nada", true);

        sb.Insert(0, "Comandos de voz: " + ok + " de " + total + " bien\n");
        System.IO.Directory.CreateDirectory("Logs/EditorAutomation");
        System.IO.File.WriteAllText("Logs/EditorAutomation/voz.txt", sb.ToString());
        Assert.AreEqual(total, ok, sb.ToString());
    }

    private static bool Active(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            if (t.name == name && t.gameObject.scene.IsValid() && t.gameObject.activeInHierarchy) return true;
        return false;
    }

    private static MonoBehaviour FindPlane(string plane)
    {
        var type = System.Type.GetType("CTPlaneView, Assembly-CSharp");
        foreach (MonoBehaviour p in Object.FindObjectsOfType(type, true))
            if ((string)type.GetProperty("Plane").GetValue(p) == plane && p.isActiveAndEnabled) return p;
        return null;
    }

    private static int Slice(MonoBehaviour plane)
    {
        return plane == null ? -1 : (int)plane.GetType().GetProperty("Slice").GetValue(plane);
    }
}
