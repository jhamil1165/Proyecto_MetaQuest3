using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class JpgSliceViewer : MonoBehaviour
{
    [Header("Paneles existentes")]
    [SerializeField] private RawImage axialImage;
    [SerializeField] private RawImage coronalImage;
    [SerializeField] private RawImage sagittalImage;

    [Header("Sliders existentes")]
    [SerializeField] private Slider axialSlider;
    [SerializeField] private Slider coronalSlider;
    [SerializeField] private Slider sagittalSlider;

    private string[] axialFiles = Array.Empty<string>();
    private string[] coronalFiles = Array.Empty<string>();
    private string[] sagittalFiles = Array.Empty<string>();

    private Texture2D axialTexture;
    private Texture2D coronalTexture;
    private Texture2D sagittalTexture;

    private void Start()
    {
        axialSlider.onValueChanged.AddListener(ShowAxial);
        coronalSlider.onValueChanged.AddListener(ShowCoronal);
        sagittalSlider.onValueChanged.AddListener(ShowSagittal);

    }

    // Este método se conectará al botón "Elegir carpeta".
    public void ChooseOrganFolder()
    {
#if UNITY_EDITOR
        string selectedFolder =
            UnityEditor.EditorUtility.OpenFolderPanel(
                "Elige la carpeta del órgano",
                "",
                ""
            );

        if (!string.IsNullOrEmpty(selectedFolder))
        {
            LoadOrganFolder(selectedFolder);
        }
#else
        Debug.LogWarning(
            "Este selector de carpetas funciona en el Editor de Unity. " +
            "La selección de archivos en Quest se implementará aparte."
        );
#endif
    }

    private void LoadOrganFolder(string organFolder)
    {
        string axialFolder =
            Path.Combine(organFolder, "axial");

        string coronalFolder =
            Path.Combine(organFolder, "coronal");

        string sagittalFolder =
            Path.Combine(organFolder, "sagital");

        if (!Directory.Exists(sagittalFolder))
        {
            // Por si otro órgano utiliza el nombre en inglés.
            sagittalFolder =
                Path.Combine(organFolder, "sagittal");
        }

        if (!Directory.Exists(axialFolder) ||
            !Directory.Exists(coronalFolder) ||
            !Directory.Exists(sagittalFolder))
        {
            Debug.LogError(
                "La carpeta elegida debe contener axial, " +
                "coronal y sagital (o sagittal). Elegiste: " +
                organFolder
            );
            return;
        }

        string[] newAxialFiles = GetOrderedJpgFiles(axialFolder);
        string[] newCoronalFiles = GetOrderedJpgFiles(coronalFolder);
        string[] newSagittalFiles = GetOrderedJpgFiles(sagittalFolder);

        if (newAxialFiles.Length == 0 ||
            newCoronalFiles.Length == 0 ||
            newSagittalFiles.Length == 0)
        {
            Debug.LogError(
                "Una de las tres carpetas no contiene archivos JPG."
            );
            return;
        }

        axialFiles = newAxialFiles;
        coronalFiles = newCoronalFiles;
        sagittalFiles = newSagittalFiles;

        ConfigureSlider(axialSlider, axialFiles.Length);
        ConfigureSlider(coronalSlider, coronalFiles.Length);
        ConfigureSlider(sagittalSlider, sagittalFiles.Length);

        int axialMiddle = axialFiles.Length / 2;
        int coronalMiddle = coronalFiles.Length / 2;
        int sagittalMiddle = sagittalFiles.Length / 2;

        axialSlider.SetValueWithoutNotify(axialMiddle);
        coronalSlider.SetValueWithoutNotify(coronalMiddle);
        sagittalSlider.SetValueWithoutNotify(sagittalMiddle);

        ShowAxial(axialMiddle);
        ShowCoronal(coronalMiddle);
        ShowSagittal(sagittalMiddle);

        Debug.Log(
            "Órgano cargado: " + Path.GetFileName(organFolder) +
            " | Axial: " + axialFiles.Length +
            " | Coronal: " + coronalFiles.Length +
            " | Sagital: " + sagittalFiles.Length
        );
    }

    private string[] GetOrderedJpgFiles(string folder)
    {
        return Directory.GetFiles(folder)
            .Where(path =>
                string.Equals(
                    Path.GetExtension(path),
                    ".jpg",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                string.Equals(
                    Path.GetExtension(path),
                    ".jpeg",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .OrderBy(path => GetFinalNumber(path))
            .ThenBy(path => Path.GetFileName(path))
            .ToArray();
    }

    private int GetFinalNumber(string path)
    {
        string name =
            Path.GetFileNameWithoutExtension(path);

        int lastUnderscore =
            name.LastIndexOf('_');

        if (lastUnderscore >= 0 &&
            int.TryParse(
                name.Substring(lastUnderscore + 1),
                out int number
            ))
        {
            return number;
        }

        return int.MaxValue;
    }

    private void ConfigureSlider(
        Slider slider,
        int numberOfImages
    )
    {
        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = numberOfImages - 1;
    }

    private void ShowAxial(float value)
    {
        ShowImage(
            axialFiles,
            Mathf.RoundToInt(value),
            axialImage,
            ref axialTexture
        );
    }

    private void ShowCoronal(float value)
    {
        ShowImage(
            coronalFiles,
            Mathf.RoundToInt(value),
            coronalImage,
            ref coronalTexture
        );
    }

    private void ShowSagittal(float value)
    {
        ShowImage(
            sagittalFiles,
            Mathf.RoundToInt(value),
            sagittalImage,
            ref sagittalTexture
        );
    }

    private void ShowImage(
        string[] files,
        int index,
        RawImage panel,
        ref Texture2D previousTexture
    )
    {
        if (files.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(
            index,
            0,
            files.Length - 1
        );

        try
        {
            byte[] jpgBytes =
                File.ReadAllBytes(files[index]);

            Texture2D newTexture =
                new Texture2D(2, 2);

            if (!newTexture.LoadImage(jpgBytes))
            {
                Destroy(newTexture);

                Debug.LogError(
                    "No se pudo abrir: " + files[index]
                );

                return;
            }

            panel.texture = newTexture;

            if (previousTexture != null)
            {
                Destroy(previousTexture);
            }

            previousTexture = newTexture;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Error al leer el JPG: " +
                files[index] +
                "\n" +
                exception.Message
            );
        }
    }

    private void OnDestroy()
    {
        if (axialTexture != null)
            Destroy(axialTexture);

        if (coronalTexture != null)
            Destroy(coronalTexture);

        if (sagittalTexture != null)
            Destroy(sagittalTexture);
    }
}