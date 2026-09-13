using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Conecta el visor a las dos series DICOM reales en lugar de las capturas de Slicer.
///
/// Las capturas de Slicer traian la segmentacion pintada en naranja, el rotulo
/// "B: 1: Unn...Series" quemado en la imagen y, en coronal y sagital, el cuerpo entero
/// encogido dentro del recuadro. Las series convertidas desde los .dcm son la TC
/// limpia, con ventana de tejido blando 40/400, que es lo que se ve en el visor de
/// referencia. Las capturas no se borran: siguen disponibles si se vacia "studies".
///
/// Antes de tocar la escena se comprueba que existan el primer corte, el central y
/// el ultimo de cada plano, y que NO exista uno mas: asi un recuento equivocado se
/// detecta aqui y no como una columna en negro dentro del visor.
/// </summary>
public static class MedicalCtStudies
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string PreviewDir = "Assets/UI/CTPreview";

    private struct StudyDef
    {
        public string folder, label;
        public int axial, coronal, sagittal;

        public StudyDef(string folder, string label, int axial, int coronal, int sagittal)
        {
            this.folder = folder;
            this.label = label;
            this.axial = axial;
            this.coronal = coronal;
            this.sagittal = sagittal;
        }
    }

    // Recuentos sacados de las cabeceras DICOM: axial = archivos de la serie,
    // coronal = filas y sagital = columnas de cada corte (512 x 512 en las dos).
    private static readonly StudyDef[] Studies =
    {
        new StudyDef("serie_cuerpo", "Cuerpo completo", 267, 512, 512),
        new StudyDef("serie_toraxabdomen", "Tórax y abdomen", 139, 512, 512),
    };

    [MenuItem("MedicalViewer/Step58 - Visor con las series DICOM")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        bool ok = true;
        foreach (var st in Studies)
        {
            ok &= Check(st.folder, "axial", st.axial, sb);
            ok &= Check(st.folder, "coronal", st.coronal, sb);
            ok &= Check(st.folder, "sagital", st.sagittal, sb);
        }

        if (!ok)
        {
            sb.AppendLine("[FALLO] faltan cortes: no se toca la escena");
            Dump(sb);
            return;
        }

        var viewer = Object.FindObjectOfType<CTMultiPlaneViewer>(true);
        if (viewer == null)
        {
            sb.AppendLine("[FALLO] no hay CTMultiPlaneViewer");
            Dump(sb);
            return;
        }

        var so = new SerializedObject(viewer);
        var list = so.FindProperty("studies");
        list.arraySize = Studies.Length;
        for (int i = 0; i < Studies.Length; i++)
        {
            var e = list.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("folder").stringValue = Studies[i].folder;
            e.FindPropertyRelative("label").stringValue = Studies[i].label;
            e.FindPropertyRelative("axial").intValue = Studies[i].axial;
            e.FindPropertyRelative("coronal").intValue = Studies[i].coronal;
            e.FindPropertyRelative("sagittal").intValue = Studies[i].sagittal;
        }
        var organLabel = so.FindProperty("organLabel").objectReferenceValue as TMP_Text;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(viewer);
        sb.AppendLine("studies: " + Studies.Length + " series enlazadas");

        // Lo que se ve fuera de Play debe coincidir con lo que saldra al arrancar.
        StudyDef first = Studies[0];
        if (organLabel != null)
        {
            Undo.RecordObject(organLabel, "Serie TC");
            organLabel.text = first.label;
            EditorUtility.SetDirty(organLabel);
        }

        System.IO.Directory.CreateDirectory(PreviewDir);

        foreach (var view in Object.FindObjectsOfType<CTPlaneView>(true))
        {
            var vso = new SerializedObject(view);
            string plane = vso.FindProperty("plane").stringValue;
            int count = CountFor(first, plane);
            if (count <= 0)
            {
                sb.AppendLine("[AVISO] plano desconocido: " + plane);
                continue;
            }

            vso.FindProperty("sliceCount").intValue = count;
            var raw = vso.FindProperty("display").objectReferenceValue as RawImage;
            var slider = vso.FindProperty("slider").objectReferenceValue as Slider;
            var counter = vso.FindProperty("counter").objectReferenceValue as TMP_Text;
            vso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            int mid = count / 2;

            if (slider != null)
            {
                Undo.RecordObject(slider, "Serie TC");
                slider.wholeNumbers = true;
                slider.minValue = 0;
                slider.maxValue = count - 1;
                slider.SetValueWithoutNotify(mid);
                EditorUtility.SetDirty(slider);
            }

            // Antes ponia "0 / 0" hasta darle a Play, que parecia un visor vacio.
            if (counter != null)
            {
                Undo.RecordObject(counter, "Serie TC");
                counter.text = (mid + 1) + " / " + count;
                EditorUtility.SetDirty(counter);
            }

            if (raw != null)
            {
                string src = SlicePath(first.folder, plane, mid);
                string dst = PreviewDir + "/Preview_" + plane + ".jpg";
                System.IO.File.Copy(src, dst, true);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                var importer = AssetImporter.GetAtPath(dst) as TextureImporter;
                if (importer != null)
                {
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
                Undo.RecordObject(raw, "Serie TC");
                raw.texture = tex;
                EditorUtility.SetDirty(raw);
            }

            sb.AppendLine(view.name + ": " + plane + " " + count + " cortes, arranca en " + (mid + 1) +
                          ", vista previa " + first.folder + "_" + plane + "_" + mid.ToString("D4") + ".jpg");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));
        Dump(sb);
    }

    private static bool Check(string folder, string plane, int count, StringBuilder sb)
    {
        bool first = System.IO.File.Exists(SlicePath(folder, plane, 0));
        bool mid = System.IO.File.Exists(SlicePath(folder, plane, count / 2));
        bool last = System.IO.File.Exists(SlicePath(folder, plane, count - 1));
        bool extra = System.IO.File.Exists(SlicePath(folder, plane, count));
        bool ok = first && mid && last && !extra;

        sb.AppendLine((ok ? "[ok]    " : "[FALLO] ") + folder + "/" + plane + ": " + count + " cortes" +
                      (ok ? "" : "  (primero=" + first + " central=" + mid + " ultimo=" + last + " sobra_uno=" + extra + ")"));
        return ok;
    }

    private static string SlicePath(string folder, string plane, int index)
    {
        return Application.streamingAssetsPath + "/CT/" + folder + "/" + plane + "/" +
               folder + "_" + plane + "_" + index.ToString("D4") + ".jpg";
    }

    private static int CountFor(StudyDef st, string plane)
    {
        switch (plane)
        {
            case "axial": return st.axial;
            case "coronal": return st.coronal;
            case "sagital": return st.sagittal;
            default: return 0;
        }
    }

    private static void Dump(StringBuilder sb)
    {
        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step58_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP58_DONE");
    }
}
