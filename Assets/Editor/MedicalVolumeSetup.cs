using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Monta el render de volumen: convierte los dos ficheros en bruto (tomografía y órganos)
/// en texturas 3D, crea el material con el shader MedicalViewer/VolumeRender y coloca el
/// cubo del volumen delante del usuario.
///
/// El cubo mide lo que mide el paciente: 50 x 50 cm en el plano de los cortes y 87 cm de
/// alto, que es lo que abarca la serie. El mismo plano de corte que corta los modelos
/// corta también el volumen, porque el shader lee las mismas propiedades.
/// </summary>
public static class MedicalVolumeSetup
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string VolumeDir = "Assets/Volumes";
    private const string MatDir = "Assets/Materials/Volume";
    private const string ObjectName = "Volumen_3D";

    // Medidas reales de la serie de cuerpo completo.
    private const float PixelMm = 0.976562f;
    private const float SliceMm = 3.27002f;

    [MenuItem("MedicalViewer/Step70 - Render de volumen 3D")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Shader shader = Shader.Find("MedicalViewer/VolumeRender");
        if (shader == null)
        {
            Debug.LogError("[Step70] No encuentro el shader MedicalViewer/VolumeRender.");
            return;
        }

        Texture3D density = BuildTexture("ct_densidad", FilterMode.Bilinear, sb, out Vector3Int size);
        Texture3D organs = BuildTexture("ct_organos", FilterMode.Point, sb, out _);
        if (density == null || organs == null)
        {
            Debug.LogError("[Step70] Faltan los ficheros de volumen en " + VolumeDir);
            return;
        }

        // ---- material ----
        System.IO.Directory.CreateDirectory(MatDir);
        string matPath = MatDir + "/Mat_Volumen.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, matPath);
        }
        material.shader = shader;
        material.SetTexture("_Density", density);
        material.SetTexture("_Organs", organs);
        material.SetFloat("_PlaneDistance", -100000f);
        material.SetVector("_PlaneNormal", new Vector4(0f, 1f, 0f, 0f));
        EditorUtility.SetDirty(material);
        sb.AppendLine("material: " + matPath);

        // ---- cubo del volumen ----
        GameObject volume = GameObject.Find(ObjectName);
        if (volume == null)
        {
            volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = ObjectName;
            sb.AppendLine(ObjectName + ": creado");
        }
        else
        {
            sb.AppendLine(ObjectName + ": ya existia, se reconfigura");
        }

        volume.GetComponent<Renderer>().sharedMaterial = material;

        // Tamaño real del paciente: ancho y fondo por el tamaño de píxel, alto por los cortes.
        float width = size.x * 2f * PixelMm / 1000f;   // el volumen esta a mitad de resolucion
        float height = size.z * 2f * SliceMm / 1000f;
        volume.transform.localScale = new Vector3(width, height, width);

        // Colgado del menú, que es lo que se recoloca delante del usuario al arrancar.
        GameObject root = GameObject.Find("Medical_Menu_UI");
        if (root != null)
        {
            volume.transform.SetParent(root.transform, false);

            // A la izquierda y algo por debajo: en el centro tapaba el menú y se encimaba
            // con los órganos, que están en fila delante del usuario.
            volume.transform.localPosition = new Vector3(-0.75f, -0.15f, 1.25f);
            volume.transform.localRotation = Quaternion.identity;
        }

        var body = GetOrAdd<Rigidbody>(volume);
        body.isKinematic = true;
        body.useGravity = false;

        var grab = GetOrAdd<XRGrabInteractable>(volume);
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.useDynamicAttach = true;
        grab.throwOnDetach = false;

        EditorUtility.SetDirty(volume);
        sb.AppendLine(string.Format("volumen: {0} x {1} x {2} voxeles, {3:F2} x {4:F2} x {5:F2} m",
            size.x, size.y, size.z, width, height, width));

        // ---- el mismo plano de corte también corta el volumen ----
        var controller = Object.FindObjectOfType<ClippingPlaneController>(true);
        if (controller != null)
        {
            var cso = new SerializedObject(controller);
            var list = cso.FindProperty("targets");
            bool already = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == volume.GetComponent<Renderer>()) already = true;
            }
            if (!already)
            {
                list.arraySize += 1;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = volume.GetComponent<Renderer>();
            }
            cso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            sb.AppendLine("plano de corte: tambien corta el volumen" + (already ? " (ya estaba)" : ""));
        }

        // ---- el menú lo enciende en la vista Modelo 3D ----
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var aso = new SerializedObject(actions);
            var group = aso.FindProperty("model3DObjects");
            bool already = false;
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue == volume) already = true;
            }
            if (!already)
            {
                group.arraySize += 1;
                group.GetArrayElementAtIndex(group.arraySize - 1).objectReferenceValue = volume;
            }
            aso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actions);
            sb.AppendLine("vista Modelo 3D: " + group.arraySize + " objetos" + (already ? " (el volumen ya estaba)" : ""));
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        Render(volume, sb);

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step70_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP70_DONE");
    }

    /// <summary>Convierte un fichero en bruto en una textura 3D de un byte por voxel.</summary>
    private static Texture3D BuildTexture(string prefix, FilterMode filter, StringBuilder sb, out Vector3Int size)
    {
        size = Vector3Int.zero;

        string[] found = Directory.GetFiles(VolumeDir, prefix + "_*.bytes");
        if (found.Length == 0)
        {
            sb.AppendLine("[FALLO] no encuentro " + prefix + "_*.bytes");
            return null;
        }

        string path = found[0].Replace('\\', '/');
        string dims = Path.GetFileNameWithoutExtension(path).Substring(prefix.Length + 1);
        string[] parts = dims.Split('x');
        size = new Vector3Int(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]));

        byte[] data = File.ReadAllBytes(path);
        int expected = size.x * size.y * size.z;
        if (data.Length != expected)
        {
            sb.AppendLine("[FALLO] " + path + ": " + data.Length + " bytes, esperados " + expected);
            return null;
        }

        string assetPath = VolumeDir + "/Tex_" + prefix + ".asset";
        var texture = new Texture3D(size.x, size.y, size.z, TextureFormat.R8, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = filter,
        };
        texture.SetPixelData(data, 0);
        texture.Apply(false, true); // sin lectura desde CPU: ahorra la mitad de memoria

        AssetDatabase.DeleteAsset(assetPath);
        AssetDatabase.CreateAsset(texture, assetPath);
        sb.AppendLine(string.Format("textura {0}: {1}x{2}x{3} ({4:F1} MB)", prefix, size.x, size.y, size.z, data.Length / 1048576f));
        return AssetDatabase.LoadAssetAtPath<Texture3D>(assetPath);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void Render(GameObject volume, StringBuilder sb)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
        {
            sb.AppendLine("render: sin GPU, no se genera imagen");
            return;
        }

        // Desde los ojos del usuario, con la vista Modelo 3D encendida: asi se ve como
        // queda el volumen junto al menu y a los organos, sin encimarse.
        GameObject root = GameObject.Find("Medical_Menu_UI");
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        var restore = new System.Collections.Generic.List<(GameObject go, bool active)>();
        if (actions != null)
        {
            var group = new SerializedObject(actions).FindProperty("model3DObjects");
            for (int i = 0; i < group.arraySize; i++)
            {
                if (group.GetArrayElementAtIndex(i).objectReferenceValue is GameObject go)
                {
                    restore.Add((go, go.activeSelf));
                    go.SetActive(true);
                }
            }
        }

        var camGo = new GameObject("TmpVolumeCamera");
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 30f;
        cam.fieldOfView = 70f;

        if (root != null)
        {
            cam.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);
        }
        else
        {
            Bounds bounds = volume.GetComponent<Renderer>().bounds;
            cam.transform.position = bounds.center + Vector3.back * 2f;
            cam.transform.LookAt(bounds.center);
        }

        const int w = 2000;
        const int h = 1100;
        cam.aspect = (float)w / h;

        var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = target;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(w, h, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        image.Apply();
        RenderTexture.active = previous;

        string path = OutDir + "step70_volumen.png";
        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllBytes(path, image.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(target);
        Object.DestroyImmediate(image);
        Object.DestroyImmediate(camGo);

        for (int i = restore.Count - 1; i >= 0; i--) restore[i].go.SetActive(restore[i].active);

        sb.AppendLine("render: " + path);
    }
}
