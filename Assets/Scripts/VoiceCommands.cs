using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Oculus.Voice;
using TMPro;
using UnityEngine;

/// <summary>
/// Comandos de voz en español: el visor escucha, Wit.ai (Voice SDK de Meta) pasa la voz a
/// texto y aquí se busca en ese texto una de las frases de la tabla.
///
/// Wit solo se usa para el reconocimiento de voz, como pidió el profesor: no hay intents ni
/// entrenamiento en la web de Wit. Las frases se reconocen aquí, sin importar mayúsculas,
/// tildes ni palabras de relleno ("por favor, muestra el hígado" vale igual que "hígado").
///
/// Escucha todo el rato: cuando Wit corta por silencio o por tiempo, se vuelve a activar.
/// Lo que entendió y lo que hizo aparece un momento en un letrero bajo la vista, para que
/// el médico sepa si se le entendió sin tener que mirar nada más.
/// </summary>
[RequireComponent(typeof(AppVoiceExperience))]
public class VoiceCommands : MonoBehaviour
{
    [SerializeField] private AppVoiceExperience voice;

    [Tooltip("Volver a escuchar en cuanto Wit termina una frase.")]
    [SerializeField] private bool continuous = true;

    [Header("Letrero de lo que se entendió")]
    [SerializeField] private TMP_Text feedback;
    [SerializeField] private float feedbackSeconds = 2.5f;

    [Header("Qué controla la voz")]
    [SerializeField] private MedicalMenuActions menu;
    [SerializeField] private ClippingPlaneInput cut;
    [SerializeField] private MeasureTool ruler3D;
    [SerializeField] private CTMeasure2D ruler2D;
    [SerializeField] private VolumeDisplay volume;
    [SerializeField] private OrganSystemsPanel systems;
    [SerializeField] private OrganSegmentationPanel segmentation;
    [SerializeField] private CTPlaneView[] planes = new CTPlaneView[0];

    [Tooltip("Cuántos cortes avanza 'siguiente corte'. De uno en uno no se nota el cambio.")]
    [SerializeField] private int sliceStep = 5;

    private struct Command
    {
        public string[] phrases;   // ya normalizadas
        public string label;       // lo que se enseña en el letrero
        public Action run;
    }

    private readonly List<Command> _commands = new List<Command>();
    private Coroutine _hide;
    private GameObject _panel;   // el letrero entero (fondo + texto)
    private bool _listening;

    private void Awake()
    {
        if (voice == null) voice = GetComponent<AppVoiceExperience>();
        Build();
        if (feedback != null)
        {
            _panel = feedback.transform.parent != null ? feedback.transform.parent.gameObject : feedback.gameObject;
            _panel.SetActive(false);
        }
    }

    private void OnEnable()
    {
        voice.VoiceEvents.OnFullTranscription.AddListener(OnTranscription);
        voice.VoiceEvents.OnStoppedListening.AddListener(OnStopped);
        voice.VoiceEvents.OnError.AddListener(OnError);
        StartCoroutine(Begin());
    }

    private void OnDisable()
    {
        voice.VoiceEvents.OnFullTranscription.RemoveListener(OnTranscription);
        voice.VoiceEvents.OnStoppedListening.RemoveListener(OnStopped);
        voice.VoiceEvents.OnError.RemoveListener(OnError);
        _listening = false;
        if (voice.Active) voice.Deactivate();
    }

    /// <summary>Pide el micrófono (en Android hay que pedirlo en ejecución) y empieza.</summary>
    private IEnumerator Begin()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        const string mic = UnityEngine.Android.Permission.Microphone;
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(mic))
        {
            UnityEngine.Android.Permission.RequestUserPermission(mic);
            float until = Time.realtimeSinceStartup + 30f;
            while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(mic) && Time.realtimeSinceStartup < until)
                yield return null;
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(mic))
            {
                Show("Sin permiso de micrófono: los comandos de voz están apagados.");
                yield break;
            }
        }
#endif
        yield return null;
        _listening = true;
        Listen();
    }

    private void Listen()
    {
        if (!_listening || !isActiveAndEnabled || voice.Active) return;
        voice.Activate();
    }

    private void OnStopped()
    {
        if (continuous) StartCoroutine(ListenAgain());
    }

    private IEnumerator ListenAgain()
    {
        yield return new WaitForSeconds(0.2f);
        Listen();
    }

    private void OnError(string error, string message)
    {
        Debug.LogWarning("[Voz] " + error + ": " + message);
        if (continuous) StartCoroutine(ListenAgainLater());
    }

    private IEnumerator ListenAgainLater()
    {
        // Sin internet o con el token mal, Wit falla al momento: no insistir cada fotograma.
        yield return new WaitForSeconds(3f);
        Listen();
    }

    private void OnTranscription(string text)
    {
        string said = Normalize(text);
        if (string.IsNullOrWhiteSpace(said)) return;
        Debug.Log("[Voz] escuché: " + text);

        // Gana la frase más larga que aparezca: "quitar corte" antes que "corte".
        Command best = default;
        int bestLength = 0;
        foreach (var c in _commands)
        {
            foreach (var p in c.phrases)
            {
                if (p.Length > bestLength && ContainsWords(said, p))
                {
                    best = c;
                    bestLength = p.Length;
                }
            }
        }

        if (bestLength == 0)
        {
            Show("«" + text.Trim() + "»");
            return;
        }

        try
        {
            best.run();
            Show("«" + text.Trim() + "» → " + best.label);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Voz] no se pudo '" + best.label + "': " + e.Message);
            Show("«" + text.Trim() + "» → no disponible ahora");
        }
    }

    /// <summary>La tabla de frases. Cada comando acepta varias formas de decirlo.</summary>
    private void Build()
    {
        Add("Vista DICOM", () => menu.OpenDICOM(), "dicom", "imagenes", "tomografia", "ver dicom");
        Add("Vista segmentación", () => menu.OpenSegmentation(), "segmentacion", "segmentado");
        Add("Vista modelo 3D", () => menu.Open3DModel(), "modelo 3d", "modelo tres de", "modelo", "tres de", "3d");

        Add("Corte activado", () => cut.SetCutting(true), "activar corte", "activa el corte", "cortar", "plano de corte");
        Add("Corte quitado", () => cut.SetCutting(false), "quitar corte", "quita el corte", "sin corte", "desactivar corte");
        Add("Corte girado", () => cut.Rotate90(), "girar corte", "gira el corte", "girar", "rotar");
        Add("Corte centrado", () => cut.Center(), "centrar", "centrar corte");

        Add("Siguiente corte", () => StepSlices(sliceStep), "siguiente corte", "siguiente", "avanzar", "baja", "bajar");
        Add("Corte anterior", () => StepSlices(-sliceStep), "corte anterior", "anterior", "retroceder", "sube", "subir");

        Add("Regla", ToggleRuler, "medir", "regla", "medicion", "mide");
        Add("Medidas borradas", ClearRulers, "borrar", "borrar medidas", "borra las medidas", "limpiar");

        Add("Volumen: tejido", () => volume.Show(0), "tejido", "tejidos", "piel");
        Add("Volumen: esqueleto", () => volume.Show(1), "esqueleto", "hueso", "huesos");
        Add("Volumen: órganos", () => volume.Show(2), "organos");
        Add("Volumen oculto/visible", () => volume.ToggleVisible(), "ocultar volumen", "mostrar volumen", "volumen");

        AddOrgan("Hígado", "higado");
        AddOrgan("Estómago", "estomago");
        AddOrgan("Páncreas", "pancreas");
        AddOrgan("Vesícula", "vesicula");
        AddOrgan("Corazón", "corazon");
        Add("Todos los órganos", () => systems.ToggleAll(), "todos los organos", "mostrar todo", "todos");
        Add("Órganos ocultos", () => systems.HideAll(), "ocultar todo", "ocultar organos", "esconder todo");
    }

    private void Add(string label, Action run, params string[] phrases)
    {
        var normalized = new string[phrases.Length];
        for (int i = 0; i < phrases.Length; i++) normalized[i] = Normalize(phrases[i]);
        _commands.Add(new Command { label = label, run = run, phrases = normalized });
    }

    /// <summary>
    /// Un órgano: en Segmentación lo elige en el panel; en Modelo 3D lo enseña. "Ocultar
    /// hígado" lo apaga.
    /// </summary>
    private void AddOrgan(string label, string word)
    {
        Add(label, () => ShowOrgan(label, true), word, "mostrar " + word, "ver " + word);
        Add(label + " oculto", () => ShowOrgan(label, false), "ocultar " + word, "quitar " + word, "esconder " + word);
    }

    private void ShowOrgan(string label, bool on)
    {
        bool done = false;
        if (segmentation != null && segmentation.isActiveAndEnabled && on) done = segmentation.SelectByLabel(label);
        if (!done && systems != null) done = systems.SetVisible(label, on);
        if (!done) throw new InvalidOperationException("ningún panel de órganos abierto");
    }

    private void StepSlices(int delta)
    {
        bool any = false;
        foreach (var p in planes)
        {
            if (p == null || !p.isActiveAndEnabled) continue;
            p.Step(delta);
            any = true;
        }
        if (!any) throw new InvalidOperationException("no hay cortes a la vista");
    }

    /// <summary>La regla de la vista que esté abierta: la 2D en DICOM, la 3D en el modelo.</summary>
    private void ToggleRuler()
    {
        if (ruler2D != null && ruler2D.isActiveAndEnabled) ruler2D.Toggle();
        else if (ruler3D != null) ruler3D.Toggle();
    }

    private void ClearRulers()
    {
        if (ruler2D != null) ruler2D.Clear();
        if (ruler3D != null) ruler3D.Clear();
    }

    private void Show(string message)
    {
        if (feedback == null) return;
        feedback.text = message;
        _panel.SetActive(true);
        if (_hide != null) StopCoroutine(_hide);
        _hide = StartCoroutine(HideLater());
    }

    private IEnumerator HideLater()
    {
        yield return new WaitForSeconds(feedbackSeconds);
        if (_panel != null) _panel.SetActive(false);
    }

    /// <summary>Minúsculas, sin tildes ni signos, con un espacio entre palabras.</summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        bool space = true;
        foreach (char ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); space = false; }
            else if (!space) { sb.Append(' '); space = true; }
        }
        return sb.ToString().Trim();
    }

    /// <summary>Si la frase aparece con palabras completas: "corte" no vale dentro de "cortesia".</summary>
    private static bool ContainsWords(string said, string phrase)
    {
        return (" " + said + " ").Contains(" " + phrase + " ");
    }
}
