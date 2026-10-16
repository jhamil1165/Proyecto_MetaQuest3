using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Analiza si el corte se ve distinto según su orientación, que es lo que pidió revisar el
/// profesor ("cierta deformación al desplazar el plano de corte").
///
/// Fotografía de frente, y de cerca, la imagen del TAC que se dibuja sobre el corte en tres
/// orientaciones: horizontal (axial), vertical (coronal) y diagonal. Para cada una apunta
/// también la resolución teórica que tiene el corte en su dirección más pobre.
///
/// La sospecha que se quiere comprobar: la tomografía tiene píxeles de 0,98 mm pero cortes
/// cada 3,27 mm. Un corte horizontal coincide con un corte original y tiene la resolución
/// buena en las dos direcciones. Uno vertical o diagonal atraviesa cortes y en una dirección
/// solo tiene la de 3,27 mm, y los bordes de los órganos (que no se interpolan, porque una
/// etiqueta de órgano no admite términos medios) salen en escalones de ese tamaño. Al mover
/// el plano, los escalones se desplazan por el borde.
/// </summary>
public static class MedicalCutAnalysis
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    // Medidas de la serie: píxel en el plano de la imagen y separación entre cortes.
    private const float PixelMm = 0.976562f;
    private const float SliceMm = 3.27002f;

    [MenuItem("MedicalViewer/Step96 - Analizar el corte segun su orientacion")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            Debug.LogError("[Step96] Hace falta GPU: ejecutar sin -nographics.");
            return;
        }

        GameObject volumeGo = GameObject.Find("Volumen_3D");
        GameObject sliceGo = null;
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
        {
            if (t.name == "Corte_TAC") { sliceGo = t.gameObject; break; }
        }

        if (volumeGo == null || sliceGo == null)
        {
            Debug.LogError("[Step96] Falta Volumen_3D o Corte_TAC.");
            return;
        }

        var volume = volumeGo.GetComponent<Renderer>();
        var slice = sliceGo.GetComponent<Renderer>();

        bool volumeWasOn = volume.enabled;
        bool sliceWasOn = slice.enabled;
        bool sliceWasActive = sliceGo.activeSelf;

        // Solo la imagen del corte: el volumen de detrás estorbaría para mirarla.
        volume.enabled = false;
        sliceGo.SetActive(true);
        slice.enabled = true;

        Vector3 center = volume.bounds.center;
        Quaternion body = volumeGo.transform.rotation;

        var cases = new (string name, float tilt)[]
        {
            ("horizontal", 0f),
            ("diagonal", 38f),
            ("vertical", 90f),
        };

        sb.AppendLine("orientacion | resolucion en su direccion mas pobre | escalon de los bordes");
        foreach (var c in cases)
        {
            // El plano de corte inclinado alrededor del eje lateral del cuerpo.
            Quaternion tilt = Quaternion.AngleAxis(c.tilt, volumeGo.transform.right);
            Vector3 normal = tilt * (body * Vector3.up);

            sliceGo.transform.SetPositionAndRotation(center, Quaternion.LookRotation(normal, tilt * (body * Vector3.forward)));
            sliceGo.transform.localScale = new Vector3(1.18f, 1.18f, 1f);

            var mpb = new MaterialPropertyBlock();
            slice.GetPropertyBlock(mpb);
            mpb.SetMatrix("_VolumeWorldToObject", volumeGo.transform.worldToLocalMatrix);
            slice.SetPropertyBlock(mpb);

            string path = OutDir + "step96_" + c.name + ".png";
            Shoot(center, normal, sliceGo.transform.up, path);

            // Resolución del corte en la dirección que más cortes atraviesa.
            float rad = c.tilt * Mathf.Deg2Rad;
            float worst = Mathf.Sqrt(Mathf.Pow(PixelMm * Mathf.Cos(rad), 2f) + Mathf.Pow(SliceMm * Mathf.Sin(rad), 2f));
            string steps = c.tilt < 1f
                ? "sin escalones (coincide con un corte original)"
                : (SliceMm / Mathf.Sin(rad)).ToString("F1") + " mm por escalon";

            sb.AppendLine(string.Format("{0,-11} | {1:F2} mm | {2}", c.name, worst, steps));
            sb.AppendLine("            foto: " + path);
        }

        volume.enabled = volumeWasOn;
        slice.enabled = sliceWasOn;
        sliceGo.SetActive(sliceWasActive);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step96_analisis.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP96_DONE");
    }

    /// <summary>Foto de frente a la imagen del corte, a 45 cm.</summary>
    private static void Shoot(Vector3 center, Vector3 normal, Vector3 up, string path)
    {
        var camGo = new GameObject("TmpAnalysisCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
        cam.fieldOfView = 50f;
        cam.nearClipPlane = 0.02f;
        cam.farClipPlane = 5f;

        cam.transform.position = center + normal * 0.62f;
        cam.transform.LookAt(center, up);

        const int size = 1000;
        cam.aspect = 1f;

        var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = target;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(size, size, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);
    }
}
