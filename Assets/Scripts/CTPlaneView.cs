using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Una columna del visor: un plano anatómico con su imagen, su deslizador y su
/// contador de corte. Tres de estas forman la vista axial / coronal / sagital
/// simultánea, que es como se revisa una tomografía en radiología.
///
/// Cada columna carga solo el corte visible desde StreamingAssets. En Android eso
/// queda dentro del APK comprimido, así que la lectura va por UnityWebRequest:
/// File.ReadAllBytes funciona en el editor y falla en el dispositivo.
/// </summary>
public class CTPlaneView : MonoBehaviour
{
    [Header("Plano")]
    [Tooltip("Nombre de la carpeta y del archivo: axial, coronal o sagital.")]
    [SerializeField] private string plane = "axial";

    [Tooltip("Número de cortes de este plano en el volumen de la TC.")]
    [SerializeField] private int sliceCount = 267;

    [Header("UI")]
    [SerializeField] private RawImage display;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text counter;

    [SerializeField] private string rootFolder = "CT";

    private string _organ;
    private int _slice;
    private Coroutine _loading;
    private Texture2D _current;

    public string Plane => plane;
    public int SliceCount => sliceCount;
    public int Slice => _slice;

    /// <summary>Se dispara cuando el usuario mueve el deslizador (no al cambiar de serie).</summary>
    public event System.Action<CTPlaneView, int> SliceChanged;

    private void Awake()
    {
        if (title != null) title.text = Capitalize(plane);

        if (slider != null)
        {
            slider.wholeNumbers = true;
            slider.minValue = 0;
            slider.maxValue = Mathf.Max(0, sliceCount - 1);
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void OnDestroy()
    {
        if (_current != null) Destroy(_current);
    }

    public void SetOrgan(string organ)
    {
        _organ = organ;

        // El corte central es el más informativo para empezar.
        _slice = sliceCount / 2;
        if (slider != null) slider.SetValueWithoutNotify(_slice);

        Refresh();
    }

    /// <summary>
    /// Cambia de serie y, con ella, el número de cortes de este plano: cada serie de
    /// TC trae su propio número de cortes axiales.
    /// </summary>
    public void SetOrgan(string organ, int count)
    {
        if (count > 0 && count != sliceCount)
        {
            sliceCount = count;

            if (slider != null)
            {
                // Bajar el valor antes de cambiar el máximo: si el máximo nuevo es
                // menor, el deslizador recortaría su valor y dispararía una carga del
                // corte equivocado.
                slider.SetValueWithoutNotify(0);
                slider.maxValue = Mathf.Max(0, sliceCount - 1);
            }
        }

        SetOrgan(organ);
    }

    /// <summary>
    /// Va a un corte sin avisar a los oyentes. Sirve para sincronizar dos visores: si
    /// avisara, cada uno movería al otro en un bucle sin fin.
    /// </summary>
    public void SetSliceWithoutNotify(int slice)
    {
        _slice = Mathf.Clamp(slice, 0, Mathf.Max(0, sliceCount - 1));
        if (slider != null) slider.SetValueWithoutNotify(_slice);
        Refresh();
    }

    private void OnSliderChanged(float value)
    {
        _slice = Mathf.RoundToInt(value);
        Refresh();
        SliceChanged?.Invoke(this, _slice);
    }

    private void Refresh()
    {
        if (string.IsNullOrEmpty(_organ)) return;

        _slice = Mathf.Clamp(_slice, 0, sliceCount - 1);
        if (counter != null) counter.text = $"{_slice + 1} / {sliceCount}";

        string file = $"{_organ}_{plane}_{_slice:D4}.jpg";
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, rootFolder, _organ, plane, file);

        if (_loading != null) StopCoroutine(_loading);
        _loading = StartCoroutine(LoadSlice(path));
    }

    private IEnumerator LoadSlice(string path)
    {
        string url = path.Contains("://") ? path : "file://" + path;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CTPlaneView] No se pudo cargar {url}: {request.error}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(request);
            if (display != null)
            {
                display.texture = tex;

                // Si la imagen lleva un AspectRatioFitter, respetar la proporción real del
                // corte: las capturas de Slicer no son cuadradas y se verían estiradas.
                if (tex.height > 0 && display.TryGetComponent(out AspectRatioFitter fitter))
                    fitter.aspectRatio = (float)tex.width / tex.height;
            }

            // Liberar el corte anterior: sin esto, cada arrastre del deslizador deja
            // una textura huérfana y la memoria se dispara.
            if (_current != null && _current != tex) Destroy(_current);
            _current = tex;
        }

        _loading = null;
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
