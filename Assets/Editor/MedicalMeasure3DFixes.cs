using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Arreglos de la regla 3D tras probarla en el visor (18/10/2026):
///
///  - El corazón no se podía medir ("Eso no es un órgano medible"): no está en la tomografía
///    y no había de dónde sacar su tamaño real. Se le da la escala de un corazón adulto de
///    referencia (12 cm de base a punta, el eje más largo) y sus medidas salen con "≈".
///  - Medir dentro del volumen: el rayo chocaba con la caja invisible que lo envuelve. Ahora
///    se mide sobre el plano de corte, que es la imagen del TAC. El volumen está a escala
///    real (50 x 87,3 x 50 cm, igual que el estudio), así que la medida es exacta.
///  - El número de cada medida y los avisos salían enormes delante de la cara: a menos de la
///    mitad de tamaño.
///  - El plano de corte se queda en los tres cortes anatómicos del volumen.
/// </summary>
public static class MedicalMeasure3DFixes
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    // Corazón adulto: unos 12 cm de base a punta, 8-9 de ancho, 6 de grosor.
    private const float HeartReferenceLength = 0.12f;
    // Altura real del estudio: 267 cortes de 3,27 mm.
    private const float StudyHeight = 267 * 0.00327f;
    private const float LabelScale = 0.0005f;

    [MenuItem("MedicalViewer/Step105 - Regla 3D: corazon, volumen y textos")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var tool = Object.FindObjectOfType<MeasureTool>(true);
        var so = new SerializedObject(tool);
        var organs = so.FindProperty("organs");

        GameObject heart = GameObject.Find("Heart");
        GameObject volume = GameObject.Find("UI_Manager/Medical_Menu_UI/Volumen_3D");

        float heartScale = LongestSide(heart) / HeartReferenceLength;
        float volumeScale = volume.transform.lossyScale.y / StudyHeight;

        Upsert(organs, "Corazón", heart.transform, heartScale, true, sb);
        Upsert(organs, "Volumen", volume.transform, volumeScale, false, sb);

        so.FindProperty("volumeRoot").objectReferenceValue = volume.transform;

        GameObject plane = GameObject.Find("Plano_de_corte");
        var surfaces = so.FindProperty("volumeSurfaces");
        surfaces.arraySize = 1;
        surfaces.GetArrayElementAtIndex(0).objectReferenceValue = plane.GetComponent<Collider>();
        sb.AppendLine("se mide sobre el plano de corte: " + plane.name);

        var labelRoot = so.FindProperty("labelRoot").objectReferenceValue as Transform;
        if (labelRoot != null)
        {
            sb.AppendLine(string.Format("textos de la regla: {0} -> {1}", labelRoot.localScale.x, LabelScale));
            labelRoot.localScale = Vector3.one * LabelScale;
            EditorUtility.SetDirty(labelRoot);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tool);

        // El plano de corte, fijo a los ejes del volumen.
        var cut = plane.GetComponent<ClippingPlaneInput>();
        var cso = new SerializedObject(cut);
        cso.FindProperty("volume").objectReferenceValue = volume.transform;
        cso.FindProperty("lockToAnatomical").boolValue = true;
        cso.ApplyModifiedPropertiesWithoutUndo();
        sb.AppendLine("plano de corte: solo axial, coronal y sagital");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step105_output.txt", sb.ToString());
        Debug.Log("STEP105_DONE");
    }

    private static void Upsert(SerializedProperty organs, string label, Transform root, float scale,
                               bool approximate, StringBuilder sb)
    {
        int index = -1;
        for (int i = 0; i < organs.arraySize; i++)
        {
            if (organs.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue == label) index = i;
        }
        if (index < 0)
        {
            index = organs.arraySize;
            organs.arraySize++;
        }

        var e = organs.GetArrayElementAtIndex(index);
        e.FindPropertyRelative("label").stringValue = label;
        e.FindPropertyRelative("root").objectReferenceValue = root;
        e.FindPropertyRelative("displayScale").floatValue = scale;
        e.FindPropertyRelative("approximate").boolValue = approximate;
        sb.AppendLine(string.Format("{0}: se ve {1:F2} veces su tamaño{2}", label, scale,
            approximate ? " (referencia, medidas con ≈)" : ""));
    }

    /// <summary>Lado más largo del modelo tal y como se ve, sin la peana.</summary>
    private static float LongestSide(GameObject go)
    {
        float longest = 0f;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null || mf.sharedMesh.name == "Quad") continue;
            Vector3 size = Vector3.Scale(mf.sharedMesh.bounds.size, mf.transform.lossyScale);
            longest = Mathf.Max(longest, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
        }
        return longest;
    }
}
