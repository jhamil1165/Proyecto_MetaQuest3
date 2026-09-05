using System;
using System.IO;
using FellowOakDicom;
using UnityEngine;

public class DicomVolumeViewer : MonoBehaviour
{
    [Header("Carpeta que contiene los archivos DICOM")]
    [SerializeField] private string dicomFolderPath = @"C:\DICOM_Test";

    private void Start()
    {
        ReadDicomFolder();
    }

    private void ReadDicomFolder()
    {
        // Verifica que la carpeta indicada realmente exista.
        if (!Directory.Exists(dicomFolderPath))
        {
            Debug.LogError(
                "No se encontró la carpeta: " + dicomFolderPath
            );

            return;
        }

        // Busca todos los archivos, incluso dentro de subcarpetas.
        string[] paths = Directory.GetFiles(
            dicomFolderPath,
            "*",
            SearchOption.AllDirectories
        );

        Debug.Log("Archivos encontrados en la carpeta: " + paths.Length);

        int validDicomFiles = 0;

        foreach (string path in paths)
        {
            try
            {
                DicomFile dicomFile = DicomFile.Open(path);
                DicomDataset dataset = dicomFile.Dataset;

                ushort rows =
                    dataset.GetSingleValue<ushort>(DicomTag.Rows);

                ushort columns =
                    dataset.GetSingleValue<ushort>(DicomTag.Columns);

                validDicomFiles++;

                Debug.Log(
                    "DICOM válido: " +
                    Path.GetFileName(path) +
                    " | Dimensiones: " +
                    columns +
                    " x " +
                    rows
                );
            }
            catch (Exception)
            {
                // El archivo no era una imagen DICOM válida.
            }
        }

        Debug.Log(
            "Total de imágenes DICOM válidas: " +
            validDicomFiles
        );
    }
}