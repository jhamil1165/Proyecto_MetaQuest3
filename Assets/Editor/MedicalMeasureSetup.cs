using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Monta la regla: los dos puntos, la línea que los une y el número que aparece en medio.
///
/// Lo llama el constructor del panel (Step73), que es quien crea los botones "Medir" y
/// "Borrar" y los engancha aquí. Se deja aparte porque son piezas de escena, no de interfaz.
/// </summary>
public static class MedicalMeasureSetup
{
    private const string ObjectName = "Herramienta_Medicion";

    /// <summary>Crea la regla si no existe y deja sus piezas enganchadas. Devuelve el componente.</summary>
    public static MeasureTool Ensure(GameObject root, StringBuilder sb)
    {
        GameObject go = GameObject.Find(ObjectName);
        if (go == null)
        {
            go = new GameObject(ObjectName);
            sb.AppendLine("regla: creada");
        }

        if (root != null) go.transform.SetParent(root.transform, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;

        var tool = go.GetComponent<MeasureTool>();
        if (tool == null) tool = go.AddComponent<MeasureTool>();

        Transform markerA = Marker(go.transform, "Punto_A", new Color(0.09f, 0.42f, 0.88f));
        Transform markerB = Marker(go.transform, "Punto_B", new Color(0.94f, 0.35f, 0.24f));
        LineRenderer line = Line(go.transform);
        Transform labelRoot = Label(go.transform, out TMPro.TMP_Text label);

        // Los rayos de los mandos: se buscan en la escena porque el rig lo monta otro paso.
        // Se recogen todos los rayos del rig y la regla usa el que apunte a algo. Se
        // descartan las ramas huerfanas que dejo una version anterior del rig, que nunca
        // apuntan a nada pero ensucian la lista.
        var rays = new System.Collections.Generic.List<XRRayInteractor>();
        foreach (var ray in Object.FindObjectsOfType<XRRayInteractor>(true))
        {
            string branch = Branch(ray.transform);
            if (branch.ToUpperInvariant().Contains("ORPHAN"))
            {
                sb.AppendLine("  rayo descartado (rama huerfana): " + branch);
                continue;
            }

            rays.Add(ray);
            sb.AppendLine("  rayo: " + branch);
        }
        sb.AppendLine("rayos utiles: " + rays.Count);

        // La caja del volumen no se mide: el rayo sólo toca su superficie, no el hueso.
        var ignored = new System.Collections.Generic.List<Collider>();
        GameObject volume = GameObject.Find("Volumen_3D");
        if (volume != null)
        {
            var collider = volume.GetComponent<Collider>();
            if (collider != null) ignored.Add(collider);
        }

        var so = new SerializedObject(tool);
        var rayList = so.FindProperty("rays");
        rayList.arraySize = rays.Count;
        for (int i = 0; i < rays.Count; i++) rayList.GetArrayElementAtIndex(i).objectReferenceValue = rays[i];
        so.FindProperty("markerA").objectReferenceValue = markerA;
        so.FindProperty("markerB").objectReferenceValue = markerB;
        so.FindProperty("line").objectReferenceValue = line;
        so.FindProperty("label").objectReferenceValue = label;
        so.FindProperty("labelRoot").objectReferenceValue = labelRoot;
        so.FindProperty("measuring").boolValue = false;

        var list = so.FindProperty("ignored");
        list.arraySize = ignored.Count;
        for (int i = 0; i < ignored.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = ignored[i];

        FillOrganScales(so, sb);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tool);

        markerA.gameObject.SetActive(false);
        markerB.gameObject.SetActive(false);
        line.enabled = false;
        labelRoot.gameObject.SetActive(false);

        sb.AppendLine("regla: dos puntos, linea y numero listos (se mide con el boton A del mando)");
        return tool;
    }

    /// <summary>Rama completa de la jerarquia, para saber de que mano es cada rayo.</summary>
    private static string Branch(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null) { path = p.name + "/" + path; p = p.parent; }
        return path;
    }

    /// <summary>
    /// Volumen real de cada organo, en mililitros, medido sobre la segmentacion del TAC
    /// (Segmentacion.nrrd). Son los mismos numeros que salen de contar voxeles y de calcular
    /// el volumen de la malla, que coincidian: higado 1665/1664, estomago 436/435,
    /// pancreas 67/67, vesicula 12/11.
    ///
    /// El corazon no esta en esa segmentacion (viene de otro modelo), asi que no lleva
    /// numero: la regla dira "escala sin comprobar" antes que inventarse una medida.
    /// </summary>
    private static readonly (string name, float millilitres)[] RealVolumes =
    {
        ("Hígado", 1665f),
        ("Estómago", 436f),
        ("Páncreas", 67f),
        ("Vesícula", 12f),
    };

    /// <summary>
    /// Calcula, para cada organo, cuantas veces se muestra mas grande de lo que es. Se
    /// compara el volumen que ocupa su malla en la escena con el que ocupa en la tomografia;
    /// la raiz cubica de esa proporcion es el factor que hay que deshacer al medir.
    /// </summary>
    private static void FillOrganScales(SerializedObject so, StringBuilder sb)
    {
        var list = so.FindProperty("organs");
        list.arraySize = RealVolumes.Length;

        for (int i = 0; i < RealVolumes.Length; i++)
        {
            var element = list.GetArrayElementAtIndex(i);
            GameObject go = GameObject.Find(RealVolumes[i].name);

            element.FindPropertyRelative("label").stringValue = RealVolumes[i].name;
            element.FindPropertyRelative("root").objectReferenceValue = go != null ? go.transform : null;

            if (go == null)
            {
                element.FindPropertyRelative("displayScale").floatValue = 0f;
                sb.AppendLine("  [AVISO] no encuentro el organo " + RealVolumes[i].name);
                continue;
            }

            float scene = SceneVolume(go);     // metros cubicos tal y como se ve
            float real = RealVolumes[i].millilitres * 1e-6f;   // 1 mL = 1 cm3 = 1e-6 m3
            float factor = (scene > 0f && real > 0f) ? Mathf.Pow(scene / real, 1f / 3f) : 0f;

            element.FindPropertyRelative("displayScale").floatValue = factor;
            sb.AppendLine(string.Format("  {0}: se ve {1:F1} veces mas grande ({2:F0} mL reales, " +
                                        "{3:F0} mL en la escena)",
                RealVolumes[i].name, factor, RealVolumes[i].millilitres, scene * 1e6f));
        }
    }

    /// <summary>Volumen que encierra la malla tal y como esta puesta en la escena, en m3.</summary>
    private static float SceneVolume(GameObject go)
    {
        float total = 0f;

        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = mf.sharedMesh;
            // No se mira isReadable: eso solo limita la lectura en el juego ya compilado.
            // Aqui estamos en el editor, donde la malla se lee siempre.
            if (mesh == null || mesh.vertexCount == 0) continue;

            Vector3[] v = mesh.vertices;
            int[] tris = mesh.triangles;

            // Suma de tetraedros contra el origen: es el volumen con signo de una malla cerrada.
            double sum = 0.0;
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 a = v[tris[t]], b = v[tris[t + 1]], c = v[tris[t + 2]];
                sum += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
            }

            Vector3 scale = mf.transform.lossyScale;
            total += Mathf.Abs((float)sum) * Mathf.Abs(scale.x * scale.y * scale.z);
        }

        return total;
    }

    private static Transform Marker(Transform parent, string name, Color color)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localScale = Vector3.one * 0.014f;

        // Sin collider: si lo tuviera, el rayo mediria contra la propia bolita.
        var collider = go.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = SolidMaterial(name, color);
        return go.transform;
    }

    private static LineRenderer Line(Transform parent)
    {
        Transform existing = parent.Find("Linea");
        GameObject go = existing != null ? existing.gameObject : new GameObject("Linea");
        go.transform.SetParent(parent, false);

        var line = go.GetComponent<LineRenderer>();
        if (line == null) line = go.AddComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = 0.004f;
        line.numCapVertices = 4;
        line.sharedMaterial = SolidMaterial("Linea", new Color(0.09f, 0.42f, 0.88f));
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private static Transform Label(Transform parent, out TMPro.TMP_Text text)
    {
        Transform existing = parent.Find("Numero");
        GameObject go = existing != null ? existing.gameObject : new GameObject("Numero", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var canvas = go.GetComponent<Canvas>();
        if (canvas == null) canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(320f, 90f);
        rect.localScale = Vector3.one * 0.0012f;

        Transform child = go.transform.Find("Texto");
        GameObject textGo = child != null ? child.gameObject : new GameObject("Texto", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);

        text = textGo.GetComponent<TMPro.TextMeshProUGUI>();
        if (text == null) text = textGo.AddComponent<TMPro.TextMeshProUGUI>();

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        text.alignment = TMPro.TextAlignmentOptions.Center;
        text.fontSize = 54f;
        text.color = Color.white;
        text.enableWordWrapping = false;
        return go.transform;
    }

    /// <summary>Material plano de un color, para que la bolita y la línea se vean sin luz.</summary>
    private static Material SolidMaterial(string name, Color color)
    {
        string path = "Assets/Materials/Medicion/Mat_Medicion_" + name + ".mat";
        System.IO.Directory.CreateDirectory("Assets/Materials/Medicion");

        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetColor("_Color", color);
        EditorUtility.SetDirty(material);
        return material;
    }
}
