using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Visor de cortes de tomografía. Las imágenes viven en StreamingAssets y se cargan
/// una a una en tiempo de ejecución.
///
/// Por qué StreamingAssets y no Assets: son 5.164 JPG. Importados como texturas,
/// Unity los procesaría todos, el proyecto tardaría una eternidad en abrir y en el
/// Quest se comerían la memoria. Aquí viajan crudos y solo se carga el corte visible.
///
/// Detalle de Android: en el Quest, StreamingAssets queda DENTRO del APK comprimido,
/// así que File.ReadAllBytes no sirve. Hay que pasar por UnityWebRequest, que sí
/// sabe leer del jar. Por eso la carga es asíncrona incluso en el editor.
/// </summary>
public class CTSliceViewer : MonoBehaviour
{
    public enum Plane { Axial, Coronal, Sagital }

    [Header("Datos")]
    [Tooltip("Carpeta dentro de StreamingAssets que contiene <organo>/<plano>/")]
    [SerializeField] private string rootFolder = "CT";

    [Tooltip("Órganos disponibles, en el mismo orden que las pestañas.")]
    [SerializeField] private List<string> organs = new List<string> { "higado", "estomago", "pancreas", "vesicula" };

    [Header("UI")]
    [SerializeField] private RawImage display;
    [SerializeField] private Slider sliceSlider;
    [SerializeField] private TMP_Text sliceLabel;
    [SerializeField] private TMP_Text organLabel;
    [SerializeField] private TMP_Text planeLabel;

    // Número de cortes por plano. Vienen del volumen original de la TC.
    private static readonly Dictionary<Plane, int> SliceCount = new Dictionary<Plane, int>
    {
        { Plane.Axial, 267 },
        { Plane.Coronal, 512 },
        { Plane.Sagital, 512 },
    };

    private int _organIndex;
    private Plane _plane = Plane.Axial;
    private int _slice;
    private Coroutine _loading;
    private Texture2D _current;

    private void Start()
    {
        if (sliceSlider != null)
        {
            sliceSlider.wholeNumbers = true;
            sliceSlider.onValueChanged.AddListener(OnSliderChanged);
        }

        SetPlane(Plane.Axial);
    }

    private void OnDestroy()
    {
        if (_current != null) Destroy(_current);
    }

    // ---------- API pública, para enganchar a botones ----------

    public void NextOrgan()
    {
        if (organs.Count == 0) return;

        _organIndex = (_organIndex + 1) % organs.Count;
        Refresh();
    }

    public void PreviousOrgan()
    {
        if (organs.Count == 0) return;

        _organIndex = (_organIndex - 1 + organs.Count) % organs.Count;
        Refresh();
    }

    public void ShowAxial() => SetPlane(Plane.Axial);
    public void ShowCoronal() => SetPlane(Plane.Coronal);
    public void ShowSagital() => SetPlane(Plane.Sagital);

    public void SetPlane(Plane plane)
    {
        _plane = plane;

        int count = SliceCount[plane];
        if (sliceSlider != null)
        {
            sliceSlider.minValue = 0;
            sliceSlider.maxValue = count - 1;
            // El corte central es el más informativo para empezar.
            sliceSlider.SetValueWithoutNotify(count / 2);
        }
        _slice = count / 2;

        Refresh();
    }

    private void OnSliderChanged(float value)
    {
        _slice = Mathf.RoundToInt(value);
        Refresh();
    }

    private void Refresh()
    {
        if (organs.Count == 0) return;

        string organ = organs[_organIndex];
        string plane = _plane.ToString().ToLowerInvariant();
        int count = SliceCount[_plane];
        _slice = Mathf.Clamp(_slice, 0, count - 1);

        if (organLabel != null) organLabel.text = Capitalize(organ);
        if (planeLabel != null) planeLabel.text = Capitalize(plane);
        if (sliceLabel != null) sliceLabel.text = $"{_slice + 1} / {count}";

        string file = $"{organ}_{plane}_{_slice:D4}.jpg";
        string path = System.IO.Path.Combine(Application.streamingAssetsPath, rootFolder, organ, plane, file);

        if (_loading != null) StopCoroutine(_loading);
        _loading = StartCoroutine(LoadSlice(path));
    }

    private IEnumerator LoadSlice(string path)
    {
        // UnityWebRequest funciona igual con rutas de disco y con el jar de Android.
        string url = path.Contains("://") ? path : "file://" + path;

        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CTSliceViewer] No se pudo cargar {url}: {request.error}");
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(request);
            if (display != null) display.texture = tex;

            // Liberar el corte anterior: si no, cada arrastre del deslizador deja una
            // textura huérfana y la memoria se dispara.
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
