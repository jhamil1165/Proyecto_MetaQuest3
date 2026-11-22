using System.Collections.Generic;
using System.Text;
using Meta.WitAi.Data.Configuration;
using Oculus.Voice;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dos encargos del profesor (reunión del 15/11/2026):
///
///  - Interacción con las manos: ya va con el Interaction SDK de Meta (bloque
///    OVRInteractionComprehensive). Ese bloque trae locomoción encendida: con la mano, un
///    microgesto del pulgar teletransporta; con los mandos, el joystick izquierdo desliza y el
///    derecho gira. En un visor médico eso mueve al usuario sin querer y se pelea con los
///    joysticks del plano de corte. Aquí se apaga (no se borra: se puede volver a encender).
///
///  - Comandos de voz en español con Wit (Voice SDK de Meta): un objeto Comandos_Voz con
///    AppVoiceExperience, VoiceCommands y un letrero bajo la vista con lo que se entendió.
///    El Client Access Token de la app de Wit se pasa con -witToken o, si no, se pega luego en
///    Assets/Voice/WitConfig_INMERSIA.asset.
/// </summary>
public static class MedicalVoiceAndHands
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ConfigPath = "Assets/Voice/WitConfig_INMERSIA.asset";
    private const string OutDir = "Logs/EditorAutomation/";

    private static readonly string[] Locomotion =
    {
        "Locomotor",
        "MicroGesturesLocomotionHandInteractorGroup",
        "LocomotionControllerInteractorGroup",
    };

    [MenuItem("MedicalViewer/Step108 - Voz en espanol y manos sin locomocion")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        DisableLocomotion(sb);
        SetUpVoice(sb, TokenFromCommandLine());

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step108_output.txt", sb.ToString());
        Debug.Log("STEP108_DONE");
    }

    // ---------------------------------------------------------------- manos

    private static void DisableLocomotion(StringBuilder sb)
    {
        var rig = GameObject.Find("[BuildingBlock] Camera Rig");
        Transform comprehensive = rig != null ? rig.transform.Find("OVRInteractionComprehensive") : null;
        if (comprehensive == null)
        {
            sb.AppendLine("[AVISO] no encuentro OVRInteractionComprehensive");
            return;
        }

        foreach (var t in comprehensive.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in Locomotion)
            {
                if (t.name != name || !t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(false);
                EditorUtility.SetDirty(t.gameObject);
                sb.AppendLine("locomocion apagada: " + Branch(t));
            }
        }
    }

    // ---------------------------------------------------------------- voz

    private static string TokenFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-witToken") return args[i + 1].Trim();
        return null;
    }

    private static void SetUpVoice(StringBuilder sb, string token)
    {
        // Configuración de Wit: un asset con el token de la app (idioma español en wit.ai).
        System.IO.Directory.CreateDirectory("Assets/Voice");
        var config = AssetDatabase.LoadAssetAtPath<WitConfiguration>(ConfigPath);
        if (config == null)
        {
            config = ScriptableObject.CreateInstance<WitConfiguration>();
            config.ResetData();
            AssetDatabase.CreateAsset(config, ConfigPath);
            sb.AppendLine("configuracion de Wit creada: " + ConfigPath);
        }
        if (!string.IsNullOrEmpty(token))
        {
            config.SetClientAccessToken(token);
            sb.AppendLine("token de Wit puesto (" + token.Length + " caracteres)");
        }
        else if (string.IsNullOrEmpty(config.GetClientAccessToken()))
        {
            sb.AppendLine("[AVISO] falta el Client Access Token de Wit: la voz no funcionara hasta ponerlo");
        }
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        GameObject go = GameObject.Find("Comandos_Voz");
        if (go == null) go = new GameObject("Comandos_Voz");
        var voice = go.GetComponent<AppVoiceExperience>();
        if (voice == null) voice = go.AddComponent<AppVoiceExperience>();
        var runtime = voice.RuntimeConfiguration ?? new Meta.WitAi.Configuration.WitRuntimeConfiguration();
        runtime.witConfiguration = config;
        runtime.transcribeOnly = true;        // solo voz a texto: las frases se reconocen en VoiceCommands
        runtime.maxRecordingTime = 10f;
        voice.RuntimeConfiguration = runtime;
        EditorUtility.SetDirty(voice);

        var commands = go.GetComponent<VoiceCommands>();
        if (commands == null) commands = go.AddComponent<VoiceCommands>();
        var so = new SerializedObject(commands);
        so.FindProperty("voice").objectReferenceValue = voice;
        so.FindProperty("feedback").objectReferenceValue = Feedback(sb);
        so.FindProperty("menu").objectReferenceValue = Object.FindObjectOfType<MedicalMenuActions>(true);
        so.FindProperty("cut").objectReferenceValue = Object.FindObjectOfType<ClippingPlaneInput>(true);
        so.FindProperty("ruler3D").objectReferenceValue = Object.FindObjectOfType<MeasureTool>(true);
        so.FindProperty("ruler2D").objectReferenceValue = Object.FindObjectOfType<CTMeasure2D>(true);
        so.FindProperty("volume").objectReferenceValue = Object.FindObjectOfType<VolumeDisplay>(true);
        so.FindProperty("systems").objectReferenceValue = Object.FindObjectOfType<OrganSystemsPanel>(true);
        so.FindProperty("segmentation").objectReferenceValue = Object.FindObjectOfType<OrganSegmentationPanel>(true);

        var planes = Object.FindObjectsOfType<CTPlaneView>(true);
        var list = so.FindProperty("planes");
        list.arraySize = planes.Length;
        for (int i = 0; i < planes.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = planes[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (string field in new[] { "menu", "cut", "ruler3D", "ruler2D", "volume", "systems", "segmentation" })
            sb.AppendLine("voz -> " + field + ": " + (so.FindProperty(field).objectReferenceValue != null ? "ok" : "FALTA"));
        sb.AppendLine("voz -> planos de corte: " + planes.Length);

        // Los modos del volumen, para que "tejido/esqueleto/órganos" caigan en el índice bueno.
        var volume = Object.FindObjectOfType<VolumeDisplay>(true);
        if (volume != null)
        {
            var modes = new SerializedObject(volume).FindProperty("modes");
            var names = new List<string>();
            for (int i = 0; i < modes.arraySize; i++) names.Add(i + "=" + modes.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue);
            sb.AppendLine("modos del volumen: " + string.Join(", ", names));
        }
    }

    /// <summary>
    /// Letrero de vidrio oscuro colgado de la cámara, un poco por debajo de la vista: se lee
    /// sin girar la cabeza y no tapa lo que se está mirando.
    /// </summary>
    private static TMP_Text Feedback(StringBuilder sb)
    {
        Transform eye = Camera.main != null ? Camera.main.transform : null;
        if (eye == null) { sb.AppendLine("[AVISO] sin camara principal para el letrero de voz"); return null; }

        Transform old = eye.Find("Letrero_Voz");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("Letrero_Voz", typeof(RectTransform));
        root.transform.SetParent(eye, false);
        root.transform.localPosition = new Vector3(0f, -0.26f, 0.9f);
        root.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
        root.transform.localScale = Vector3.one * 0.0006f;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;
        ((RectTransform)root.transform).sizeDelta = new Vector2(900f, 110f);

        var pill = new GameObject("Fondo", typeof(RectTransform));
        pill.transform.SetParent(root.transform, false);
        var pillRect = (RectTransform)pill.transform;
        pillRect.anchorMin = Vector2.zero;
        pillRect.anchorMax = Vector2.one;
        pillRect.offsetMin = pillRect.offsetMax = Vector2.zero;
        var image = pill.AddComponent<Image>();
        image.color = new Color(0.07f, 0.09f, 0.12f, 0.8f);
        image.raycastTarget = false;
        pill.AddComponent<UIRoundedRect>().Radius = 40f;

        var textGo = new GameObject("Texto", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(30f, 10f);
        textRect.offsetMax = new Vector2(-30f, -10f);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.fontSize = 40f;
        text.color = new Color(0.96f, 0.97f, 0.98f, 1f);
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        text.text = "«mostrar hígado» → Hígado";

        sb.AppendLine("letrero de voz bajo la vista");
        return text;
    }

    private static string Branch(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path.Replace("[BuildingBlock] Camera Rig/OVRInteractionComprehensive/", "");
    }
}
