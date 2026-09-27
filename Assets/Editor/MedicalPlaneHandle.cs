using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Hace el plano de corte agarrable sin puntería y visible sin estorbar.
///
/// Dos problemas que tenía: era una lámina de un centímetro, así que para agarrarla había
/// que acertarle con el rayo casi de milagro; y desde que es casi transparente (para no
/// teñir la imagen del TAC) apenas se ve dónde está.
///
/// La solución es separar lo que se ve de lo que se agarra: la lámina sigue siendo fina y
/// casi invisible, pero el volumen que responde al agarre se engorda a varios centímetros, y
/// se le dibuja un marco luminoso en el borde. El marco cumple dos funciones: dice dónde está
/// el plano y dice que es algo que se puede coger.
/// </summary>
public static class MedicalPlaneHandle
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string MatDir = "Assets/Materials/Volume";

    /// <summary>Grosor que se quiere para agarrarlo, en metros de verdad.</summary>
    private const float GrabThickness = 0.07f;

    [MenuItem("MedicalViewer/Step88 - Plano de corte mas facil de agarrar")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject plane = GameObject.Find("Plano_de_corte");
        if (plane == null)
        {
            Debug.LogError("[Step88] No encuentro Plano_de_corte.");
            return;
        }

        // ---- engordar lo que se agarra ----
        var box = plane.GetComponent<BoxCollider>();
        if (box != null)
        {
            float scaleY = Mathf.Abs(plane.transform.lossyScale.y);
            if (scaleY > 0.0001f)
            {
                Vector3 size = box.size;
                float before = size.y * scaleY;
                size.y = Mathf.Max(size.y, GrabThickness / scaleY);
                box.size = size;
                EditorUtility.SetDirty(box);

                sb.AppendLine(string.Format("zona de agarre: de {0:F0} mm a {1:F0} mm de grosor",
                    before * 1000f, size.y * scaleY * 1000f));
            }
        }
        else
        {
            sb.AppendLine("[AVISO] el plano no tiene BoxCollider; no se puede engordar el agarre");
        }

        // ---- marco luminoso en el borde ----
        Transform existing = plane.transform.Find("Borde");
        GameObject edge = existing != null ? existing.gameObject : new GameObject("Borde");
        edge.transform.SetParent(plane.transform, false);
        edge.transform.localPosition = Vector3.zero;
        edge.transform.localRotation = Quaternion.identity;
        edge.transform.localScale = Vector3.one;

        var line = edge.GetComponent<LineRenderer>();
        if (line == null) line = edge.AddComponent<LineRenderer>();

        // En coordenadas del propio plano: así gira y se mueve con él sin hacer nada.
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;
        line.SetPosition(0, new Vector3(-0.5f, 0f, -0.5f));
        line.SetPosition(1, new Vector3(0.5f, 0f, -0.5f));
        line.SetPosition(2, new Vector3(0.5f, 0f, 0.5f));
        line.SetPosition(3, new Vector3(-0.5f, 0f, 0.5f));

        // El ancho va en metros de mundo, y el objeto esta escalado: se compensa.
        float scale = Mathf.Max(0.0001f, Mathf.Abs(plane.transform.lossyScale.x));
        line.widthMultiplier = 0.006f / scale;

        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sharedMaterial = EdgeMaterial(sb);
        EditorUtility.SetDirty(line);

        sb.AppendLine("marco del borde: 6 mm de grosor, sigue al plano al girarlo");

        AssetDatabase.SaveAssets();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step88_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP88_DONE");
    }

    private static Material EdgeMaterial(StringBuilder sb)
    {
        System.IO.Directory.CreateDirectory(MatDir);
        string path = MatDir + "/Mat_Borde_Corte.mat";

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            sb.AppendLine("material del borde creado en " + path);
        }

        // Azul claro, el mismo acento que los botones: se reconoce como algo con lo que se
        // puede interactuar.
        var color = (Color)new Color32(0x4C, 0x8D, 0xFF, 255);
        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
