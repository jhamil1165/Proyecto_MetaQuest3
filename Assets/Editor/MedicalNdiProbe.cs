using System.Text;
using Klak.Ndi;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Pregunta a la red qué emisiones NDI se ven desde este ordenador.
///
/// No sustituye a probarlo en el visor, pero comprueba lo que sí se puede comprobar sin
/// laboratorio: que la librería de NDI carga, que la llamada que hace nuestro buscador
/// existe y responde, y qué está emitiendo ahora mismo. Si esto reventara, el buscador
/// tampoco funcionaría en el Quest.
/// </summary>
public static class MedicalNdiProbe
{
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step90 - Que emisiones NDI se ven ahora")]
    public static void Apply()
    {
        var sb = new StringBuilder();

        try
        {
            int count = 0;
            foreach (string name in NdiFinder.sourceNames)
            {
                sb.AppendLine("  emision: " + name);
                count++;
            }

            sb.AppendLine("la libreria NDI responde: si");
            sb.AppendLine("emisiones visibles desde este ordenador: " + count);

            if (count == 0)
            {
                sb.AppendLine("ninguna, que es lo esperado: aqui no hay nada emitiendo.");
                sb.AppendLine("queda comprobado que la llamada funciona y no revienta;");
                sb.AppendLine("lo que no se puede comprobar sin emisor es que la imagen llegue.");
            }
        }
        catch (System.Exception e)
        {
            sb.AppendLine("[FALLO] la libreria NDI no responde: " + e.GetType().Name + " " + e.Message);
            sb.AppendLine("esto si seria un problema: el buscador tampoco funcionaria en el visor.");
        }

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step90_ndi.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP90_DONE");
    }
}
