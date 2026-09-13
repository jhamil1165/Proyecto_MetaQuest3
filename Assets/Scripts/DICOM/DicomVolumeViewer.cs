using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FellowOakDicom;
using UnityEngine;
using UnityEngine.UI;

// Alias para evitar conflictos entre fo-dicom y Unity.
using DicomPixelData = FellowOakDicom.Imaging.DicomPixelData;

public class DicomVolumeViewer : MonoBehaviour
{
    [Header("Carpeta que contiene los archivos DICOM")]
    [SerializeField]
    private string dicomFolderPath = @"C:\DICOM_Test";

    [Header("Paneles donde se mostrarán los cortes")]
    [SerializeField]
    private RawImage axialImage;

    [SerializeField]
    private RawImage coronalImage;

    [SerializeField]
    private RawImage sagittalImage;

    [Header("Sliders para recorrer los cortes")]
    [SerializeField]
    private Slider axialSlider;

    [SerializeField]
    private Slider coronalSlider;

    [SerializeField]
    private Slider sagittalSlider;

    // Volumen organizado como: volumen[z, y, x].
    private float[,,] volume;

    private int depth;
    private int height;
    private int width;

    private class DicomSlice
    {
        public DicomDataset Dataset;
        public int InstanceNumber;
    }

    private void Start()
    {
        LoadDicomVolume();
    }

    private void LoadDicomVolume()
    {
        if (!Directory.Exists(dicomFolderPath))
        {
            Debug.LogError(
                "No se encontró la carpeta: " + dicomFolderPath
            );

            return;
        }

        string[] paths = Directory.GetFiles(
            dicomFolderPath,
            "*",
            SearchOption.AllDirectories
        );

        List<DicomSlice> slices = new List<DicomSlice>();

        foreach (string path in paths)
        {
            try
            {
                DicomFile file = DicomFile.Open(path);
                DicomDataset dataset = file.Dataset;

                // Ignora archivos DICOM que no contengan una imagen.
                if (!dataset.Contains(DicomTag.PixelData))
                {
                    continue;
                }

                int instanceNumber =
                    dataset.GetSingleValueOrDefault<int>(
                        DicomTag.InstanceNumber,
                        slices.Count
                    );

                slices.Add(
                    new DicomSlice
                    {
                        Dataset = dataset,
                        InstanceNumber = instanceNumber
                    }
                );
            }
            catch (Exception)
            {
                // Ignora archivos que no sean DICOM válidos.
            }
        }

        if (slices.Count == 0)
        {
            Debug.LogError(
                "No se encontraron imágenes DICOM válidas."
            );

            return;
        }

        // Ordena los cortes según InstanceNumber.
        slices = slices
            .OrderBy(slice => slice.InstanceNumber)
            .ToList();

        DicomDataset firstDataset = slices[0].Dataset;

        height = firstDataset.GetSingleValue<ushort>(
            DicomTag.Rows
        );

        width = firstDataset.GetSingleValue<ushort>(
            DicomTag.Columns
        );

        depth = slices.Count;

        Debug.Log(
            $"Construyendo volumen: {width} x {height} x {depth}"
        );

        volume = new float[depth, height, width];

        for (int z = 0; z < depth; z++)
        {
            ReadSlicePixels(slices[z].Dataset, z);
        }

        Debug.Log("Volumen DICOM construido correctamente.");

        if (
            axialImage == null ||
            coronalImage == null ||
            sagittalImage == null
        )
        {
            Debug.LogError(
                "Debes conectar AxialImage, CoronalImage " +
                "y SagittalImage en el Inspector."
            );

            return;
        }

        // Configura los sliders y muestra los cortes centrales.
        ConfigureSliders();
        ShowAxial(depth / 2);
        ShowCoronal(height / 2);
        ShowSagittal(width / 2);
    }
    private void ConfigureSliders()
    {
        if (
            axialSlider == null ||
            coronalSlider == null ||
            sagittalSlider == null
        )
        {
            Debug.LogError(
                "Debes conectar los tres sliders en el Inspector."
            );

            return;
        }

        // Configuración del slider axial.
        axialSlider.wholeNumbers = true;
        axialSlider.minValue = 0;
        axialSlider.maxValue = depth - 1;
        axialSlider.value = depth / 2;

        // Configuración del slider coronal.
        coronalSlider.wholeNumbers = true;
        coronalSlider.minValue = 0;
        coronalSlider.maxValue = height - 1;
        coronalSlider.value = height / 2;

        // Configuración del slider sagital.
        sagittalSlider.wholeNumbers = true;
        sagittalSlider.minValue = 0;
        sagittalSlider.maxValue = width - 1;
        sagittalSlider.value = width / 2;

        // Elimina conexiones anteriores para evitar duplicados.
        axialSlider.onValueChanged.RemoveAllListeners();
        coronalSlider.onValueChanged.RemoveAllListeners();
        sagittalSlider.onValueChanged.RemoveAllListeners();

        // Actualiza las imágenes cuando se mueve un slider.
        axialSlider.onValueChanged.AddListener(
            value => ShowAxial(Mathf.RoundToInt(value))
        );

        coronalSlider.onValueChanged.AddListener(
            value => ShowCoronal(Mathf.RoundToInt(value))
        );

        sagittalSlider.onValueChanged.AddListener(
            value => ShowSagittal(Mathf.RoundToInt(value))
        );

        Debug.Log("Sliders configurados correctamente.");
    }

    private void ReadSlicePixels(
        DicomDataset dataset,
        int z
    )
    {
        DicomPixelData pixelData =
            DicomPixelData.Create(dataset);

        byte[] bytes = pixelData.GetFrame(0).Data;

        ushort bitsAllocated =
            dataset.GetSingleValue<ushort>(
                DicomTag.BitsAllocated
            );

        ushort pixelRepresentation =
            dataset.GetSingleValueOrDefault<ushort>(
                DicomTag.PixelRepresentation,
                0
            );

        double slope =
            dataset.GetSingleValueOrDefault<double>(
                DicomTag.RescaleSlope,
                1.0
            );

        double intercept =
            dataset.GetSingleValueOrDefault<double>(
                DicomTag.RescaleIntercept,
                0.0
            );

        if (bitsAllocated != 16)
        {
            Debug.LogError(
                $"El corte {z} no utiliza píxeles de 16 bits."
            );

            return;
        }

        int expectedBytes = width * height * 2;

        if (bytes.Length < expectedBytes)
        {
            Debug.LogError(
                $"El corte {z} parece estar comprimido. " +
                "Será necesario instalar codecs adicionales."
            );

            return;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = y * width + x;
                int byteIndex = pixelIndex * 2;

                ushort unsignedValue = (ushort)(
                    bytes[byteIndex] |
                    (bytes[byteIndex + 1] << 8)
                );

                float storedValue;

                // 0 = sin signo; 1 = con signo.
                if (pixelRepresentation == 1)
                {
                    storedValue = (short)unsignedValue;
                }
                else
                {
                    storedValue = unsignedValue;
                }

                volume[z, y, x] =
                    (float)(storedValue * slope + intercept);
            }
        }
    }

    private byte ConvertToGray(float value)
    {
        // Ventana inicial para tejidos blandos en una CT.
        float windowCenter = 40f;
        float windowWidth = 400f;

        float minimum =
            windowCenter - windowWidth / 2f;

        float maximum =
            windowCenter + windowWidth / 2f;

        float normalized = Mathf.InverseLerp(
            minimum,
            maximum,
            value
        );

        return (byte)Mathf.RoundToInt(
            normalized * 255f
        );
    }

    public void ShowAxial(int selectedZ)
    {
        selectedZ = Mathf.Clamp(
            selectedZ,
            0,
            depth - 1
        );

        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        );

        UnityEngine.Color32[] colors =
            new UnityEngine.Color32[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte gray = ConvertToGray(
                    volume[
                        selectedZ,
                        height - 1 - y,
                        x
                    ]
                );

                colors[y * width + x] =
                    new UnityEngine.Color32(
                        gray,
                        gray,
                        gray,
                        255
                    );
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();

        axialImage.texture = texture;
    }

    public void ShowCoronal(int selectedY)
    {
        selectedY = Mathf.Clamp(
            selectedY,
            0,
            height - 1
        );

        Texture2D texture = new Texture2D(
            width,
            depth,
            TextureFormat.RGBA32,
            false
        );

        UnityEngine.Color32[] colors =
            new UnityEngine.Color32[width * depth];

        for (int z = 0; z < depth; z++)
        {
            for (int x = 0; x < width; x++)
            {
                byte gray = ConvertToGray(
                    volume[
                        depth - 1 - z,
                        selectedY,
                        x
                    ]
                );

                colors[z * width + x] =
                    new UnityEngine.Color32(
                        gray,
                        gray,
                        gray,
                        255
                    );
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();

        coronalImage.texture = texture;
    }

    public void ShowSagittal(int selectedX)
    {
        selectedX = Mathf.Clamp(
            selectedX,
            0,
            width - 1
        );

        Texture2D texture = new Texture2D(
            height,
            depth,
            TextureFormat.RGBA32,
            false
        );

        UnityEngine.Color32[] colors =
            new UnityEngine.Color32[height * depth];

        for (int z = 0; z < depth; z++)
        {
            for (int y = 0; y < height; y++)
            {
                byte gray = ConvertToGray(
                    volume[
                        depth - 1 - z,
                        y,
                        selectedX
                    ]
                );

                colors[z * height + y] =
                    new UnityEngine.Color32(
                        gray,
                        gray,
                        gray,
                        255
                    );
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();

        sagittalImage.texture = texture;
    }
}