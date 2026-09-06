using UnityEngine;

public class MedicalMenuActions : MonoBehaviour
{
    public void OpenDICOM()
    {
        Debug.Log("Abrir visor DICOM");
    }

    public void OpenSegmentation()
    {
        Debug.Log("Abrir segmentación");
    }

    public void Open3DModel()
    {
        Debug.Log("Abrir modelo 3D");
    }

    public void ExitMenu()
    {
        Debug.Log("Salir del menú");
    }
}