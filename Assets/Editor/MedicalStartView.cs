using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Arranca la escena directamente en la vista DICOM para poder comprobar el visor de
/// tomografia sin tener que pulsar nada. Es un ajuste de prueba: para volver al
/// comportamiento normal, en el Inspector de MedicalMenuActions pon "Initial View"
/// otra vez en SoloMenu, o ejecuta Step55.
/// </summary>
public static class MedicalStartView
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("MedicalViewer/Step54 - Arrancar en DICOM")]
    public static void ShowDicom() { Set(2, "DICOM"); }

    [MenuItem("MedicalViewer/Step55 - Arrancar solo con el menu")]
    public static void ShowMenu() { Set(0, "SoloMenu"); }

    private static void Set(int value, string label)
    {
        EditorSceneManager.OpenScene(ScenePath);

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions == null) { Debug.LogError("[Step54] No hay MedicalMenuActions."); return; }

        var so = new SerializedObject(actions);
        var prop = so.FindProperty("initialView");

        // enumValueIndex es el ordinal dentro del desplegable, que aqui coincide con el
        // valor porque InitialView empieza en 0. Si alguna vez empieza en otro numero,
        // hay que usar intValue.
        int before = prop.enumValueIndex;
        prop.enumValueIndex = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);

        Debug.Log("vista inicial: " + before + " -> " + prop.enumValueIndex + " (" + label + ")  guardado=" + saved);
        Debug.Log("STEP54_DONE");
    }
}
