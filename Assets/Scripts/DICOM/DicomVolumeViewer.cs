using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FellowOakDicom;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Alias para evitar conflictos entre fo-dicom y Unity.
using DicomPixelData = FellowOakDicom.Imaging.DicomPixelData;

/// <summary>
/// Visor de una serie DICOM leída con fo-dicom: carga el volumen y muestra los cortes
/// axial, coronal y sagital con sus deslizadores.
///
/// Cambios respecto a la primera versión, por los tres problemas que tenía para el Quest:
///
///  1. MEMORIA. El volumen se guardaba en float[,,]: con 267 cortes de 512 x 512 son unos
///     280 MB solo de datos. Ahora se guarda en un short[] plano con los valores Hounsfield
///     tal cual: la mitad de memoria y sin el coste de un array de tres dimensiones.
///
///  2. TEXTURAS. Cada movimiento del deslizador creaba un Texture2D nuevo que nunca se
///     liberaba, así que la memoria crecía sin parar. Ahora hay una textura por plano y se
///     reutiliza.
///
///  3. DÓNDE LEE. Leía de una carpeta del PC (C:\DICOM_Test), que en el visor no existe.
///     Ahora puede leer de StreamingAssets, es decir, de dentro de la propia app, usando
///     UnityWebRequest, que es la única forma de leer ahí en Android.
///
/// Además, los cortes se ordenan por su posición real en el paciente y no por
/// InstanceNumber: en esta serie el corte 1 es el de los pies, y ordenar por número dejaba
/// el coronal y el sagital boca abajo.
/// </summary>
public class DicomVolumeViewer : MonoBehaviour
{
    public enum Origen
    {
        [Tooltip("Carpeta del PC. Solo funciona en el editor.")]
        CarpetaDelPC,

        [Tooltip("Dentro de la app (StreamingAssets). Funciona también en el Meta Quest.")]
        DentroDeLaApp,
    }

    [Header("De dónde se leen los DICOM")]
    [SerializeField] private Origen origen = Origen.DentroDeLaApp;

    [Tooltip("Carpeta del PC con los archivos DICOM (solo en el editor).")]
    [SerializeField] private string dicomFolderPath = @"C:\DICOM_Test";

    [Tooltip("Carpeta dentro de StreamingAssets. Debe incluir un index.txt con la lista de archivos.")]
    [SerializeField] private string streamingFolder = "DICOM/serie_toraxabdomen";

    [Header("Paneles donde se mostrarán los cortes")]
    [SerializeField] private RawImage axialImage;
    [SerializeField] private RawImage coronalImage;
    [SerializeField] private RawImage sagittalImage;

    [Header("Sliders para recorrer los cortes")]
    [SerializeField] private Slider axialSlider;
    [SerializeField] private Slider coronalSlider;
    [SerializeField] private Slider sagittalSlider;

    [Header("Ventana de visualización (Hounsfield)")]
    [Tooltip("Centro de ventana. 40 y 400 son los valores habituales para tejidos blandos.")]
    [SerializeField] private float windowCenter = 40f;
    [SerializeField] private float windowWidth = 400f;

    // Volumen plano: indice = (z * height + y) * width + x, en unidades Hounsfield.
    private short[] _volume;
    private int _depth;
    private int _height;
    private int _width;

    private Texture2D _axialTex;
    private Texture2D _coronalTex;
    private Texture2D _sagittalTex;
    private Color32[] _axialBuf;
    private Color32[] _coronalBuf;
    private Color32[] _sagittalBuf;

    private sealed class Slice
    {
        public DicomDataset Dataset;
        public double Position;   // coordenada z del paciente
        public int InstanceNumber;
    }

    private void Start()
    {
        StartCoroutine(LoadVolume());
    }

    private void OnDestroy()
    {
        if (_axialTex != null) Destroy(_axialTex);
        if (_coronalTex != null) Destroy(_coronalTex);
        if (_sagittalTex != null) Destroy(_sagittalTex);
    }

    private IEnumerator LoadVolume()
    {
        var datasets = new List<Slice>();

        if (origen == Origen.DentroDeLaApp)
        {
            yield return LoadFromStreamingAssets(datasets);
        }
        else
        {
            LoadFromFolder(datasets);
        }

        if (datasets.Count == 0)
        {
            Debug.LogError("[DICOM] No se encontraron imagenes DICOM validas.");
            yield break;
        }

        // Por posición real del paciente (de pies a cabeza) y, si no está, por número de
        // instancia. Ordenar solo por número dejaba el coronal y el sagital boca abajo.
        datasets = datasets
            .OrderBy(s => double.IsNaN(s.Position) ? s.InstanceNumber : s.Position)
            .ToList();

        DicomDataset first = datasets[0].Dataset;
        _height = first.GetSingleValue<ushort>(DicomTag.Rows);
        _width = first.GetSingleValue<ushort>(DicomTag.Columns);
        _depth = datasets.Count;

        long voxels = (long)_width * _height * _depth;
        Debug.Log($"[DICOM] Volumen {_width} x {_height} x {_depth} ({voxels * 2 / 1048576} MB en memoria)");

        _volume = new short[voxels];
        for (int z = 0; z < _depth; z++)
        {
            ReadSlicePixels(datasets[z].Dataset, z);

            // Un corte por frame en el visor: cargar 267 seguidos bloquea la imagen y el
            // sistema puede dar la app por colgada.
            if (z % 16 == 15) yield return null;
        }

        if (axialImage == null || coronalImage == null || sagittalImage == null)
        {
            Debug.LogError("[DICOM] Faltan por conectar AxialImage, CoronalImage o SagittalImage.");
            yield break;
        }

        _axialTex = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
        _coronalTex = new Texture2D(_width, _depth, TextureFormat.RGBA32, false);
        _sagittalTex = new Texture2D(_height, _depth, TextureFormat.RGBA32, false);
        _axialBuf = new Color32[_width * _height];
        _coronalBuf = new Color32[_width * _depth];
        _sagittalBuf = new Color32[_height * _depth];

        axialImage.texture = _axialTex;
        coronalImage.texture = _coronalTex;
        sagittalImage.texture = _sagittalTex;

        ConfigureSliders();
        ShowAxial(_depth / 2);
        ShowCoronal(_height / 2);
        ShowSagittal(_width / 2);

        Debug.Log("[DICOM] Volumen listo.");
    }

    private void LoadFromFolder(List<Slice> slices)
    {
        if (!Directory.Exists(dicomFolderPath))
        {
            Debug.LogError("[DICOM] No existe la carpeta: " + dicomFolderPath);
            return;
        }

        foreach (string path in Directory.GetFiles(dicomFolderPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                Add(slices, DicomFile.Open(path).Dataset);
            }
            catch (Exception)
            {
                // Archivos que no son DICOM: se ignoran.
            }
        }
    }

    private IEnumerator LoadFromStreamingAssets(List<Slice> slices)
    {
        string root = Path.Combine(Application.streamingAssetsPath, streamingFolder);

        // En Android no se puede listar una carpeta dentro del APK: la lista de archivos
        // viene en un index.txt que se genera al preparar el proyecto.
        string indexUrl = Url(Path.Combine(root, "index.txt"));
        string[] names;

        using (UnityWebRequest request = UnityWebRequest.Get(indexUrl))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[DICOM] No se pudo leer {indexUrl}: {request.error}");
                yield break;
            }

            names = request.downloadHandler.text
                .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .ToArray();
        }

        foreach (string name in names)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(Url(Path.Combine(root, name))))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[DICOM] No se pudo leer {name}: {request.error}");
                    continue;
                }

                try
                {
                    using (var stream = new MemoryStream(request.downloadHandler.data))
                    {
                        Add(slices, DicomFile.Open(stream).Dataset);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DICOM] {name} no es un DICOM valido: {e.Message}");
                }
            }
        }
    }

    private static string Url(string path)
    {
        path = path.Replace('\\', '/');
        return path.Contains("://") ? path : "file://" + path;
    }

    private static void Add(List<Slice> slices, DicomDataset dataset)
    {
        if (!dataset.Contains(DicomTag.PixelData)) return;

        double position = double.NaN;
        if (dataset.TryGetValues(DicomTag.ImagePositionPatient, out double[] ipp) && ipp.Length == 3)
        {
            position = ipp[2];
        }

        slices.Add(new Slice
        {
            Dataset = dataset,
            Position = position,
            InstanceNumber = dataset.GetSingleValueOrDefault(DicomTag.InstanceNumber, slices.Count),
        });
    }

    private void ReadSlicePixels(DicomDataset dataset, int z)
    {
        byte[] bytes = DicomPixelData.Create(dataset).GetFrame(0).Data;

        ushort bitsAllocated = dataset.GetSingleValue<ushort>(DicomTag.BitsAllocated);
        ushort pixelRepresentation = dataset.GetSingleValueOrDefault<ushort>(DicomTag.PixelRepresentation, 0);
        double slope = dataset.GetSingleValueOrDefault(DicomTag.RescaleSlope, 1.0);
        double intercept = dataset.GetSingleValueOrDefault(DicomTag.RescaleIntercept, 0.0);

        if (bitsAllocated != 16)
        {
            Debug.LogError($"[DICOM] El corte {z} no usa pixeles de 16 bits.");
            return;
        }

        if (bytes.Length < _width * _height * 2)
        {
            Debug.LogError($"[DICOM] El corte {z} parece comprimido: harian falta codecs.");
            return;
        }

        int offset = z * _height * _width;
        for (int i = 0; i < _width * _height; i++)
        {
            ushort raw = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            double stored = pixelRepresentation == 1 ? (short)raw : raw;
            _volume[offset + i] = (short)Mathf.Clamp((float)(stored * slope + intercept), -32768f, 32767f);
        }
    }

    private void ConfigureSliders()
    {
        if (axialSlider == null || coronalSlider == null || sagittalSlider == null)
        {
            Debug.LogError("[DICOM] Faltan por conectar los tres sliders.");
            return;
        }

        Setup(axialSlider, _depth, v => ShowAxial(Mathf.RoundToInt(v)));
        Setup(coronalSlider, _height, v => ShowCoronal(Mathf.RoundToInt(v)));
        Setup(sagittalSlider, _width, v => ShowSagittal(Mathf.RoundToInt(v)));
    }

    private static void Setup(Slider slider, int count, UnityEngine.Events.UnityAction<float> onChanged)
    {
        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = Mathf.Max(0, count - 1);
        slider.SetValueWithoutNotify(count / 2);
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(onChanged);
    }

    private byte Gray(short hounsfield)
    {
        float minimum = windowCenter - windowWidth * 0.5f;
        float maximum = windowCenter + windowWidth * 0.5f;
        return (byte)Mathf.RoundToInt(Mathf.InverseLerp(minimum, maximum, hounsfield) * 255f);
    }

    public void ShowAxial(int selectedZ)
    {
        if (_volume == null) return;

        selectedZ = Mathf.Clamp(selectedZ, 0, _depth - 1);
        int slice = selectedZ * _height * _width;

        for (int y = 0; y < _height; y++)
        {
            // La fila 0 de una textura de Unity es la de abajo, y la fila 0 del DICOM es la
            // de arriba (anterior): por eso se invierte.
            int src = slice + (_height - 1 - y) * _width;
            int dst = y * _width;
            for (int x = 0; x < _width; x++)
            {
                byte g = Gray(_volume[src + x]);
                _axialBuf[dst + x] = new Color32(g, g, g, 255);
            }
        }

        _axialTex.SetPixels32(_axialBuf);
        _axialTex.Apply(false);
    }

    public void ShowCoronal(int selectedY)
    {
        if (_volume == null) return;

        selectedY = Mathf.Clamp(selectedY, 0, _height - 1);

        for (int z = 0; z < _depth; z++)
        {
            // Sin invertir z: el volumen está ordenado de pies a cabeza y la fila 0 de la
            // textura es la de abajo, así que la cabeza queda arriba.
            int src = (z * _height + selectedY) * _width;
            int dst = z * _width;
            for (int x = 0; x < _width; x++)
            {
                byte g = Gray(_volume[src + x]);
                _coronalBuf[dst + x] = new Color32(g, g, g, 255);
            }
        }

        _coronalTex.SetPixels32(_coronalBuf);
        _coronalTex.Apply(false);
    }

    public void ShowSagittal(int selectedX)
    {
        if (_volume == null) return;

        selectedX = Mathf.Clamp(selectedX, 0, _width - 1);

        for (int z = 0; z < _depth; z++)
        {
            int dst = z * _height;
            for (int y = 0; y < _height; y++)
            {
                byte g = Gray(_volume[(z * _height + y) * _width + selectedX]);
                _sagittalBuf[dst + y] = new Color32(g, g, g, 255);
            }
        }

        _sagittalTex.SetPixels32(_sagittalBuf);
        _sagittalTex.Apply(false);
    }
}
