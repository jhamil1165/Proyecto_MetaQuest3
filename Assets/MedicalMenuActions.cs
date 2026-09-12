using UnityEngine;

/// <summary>
/// Acciones del menú principal, invocadas por los XR Simple Interactable de cada botón.
///
/// Cada "vista" es un grupo de objetos de la escena. Al abrir una vista se activa su
/// grupo y se apagan los demás, así solo hay una cosa visible a la vez. Los grupos se
/// asignan desde el Inspector: si quieres que un botón muestre otra cosa, cambias la
/// lista, no el código.
/// </summary>
public class MedicalMenuActions : MonoBehaviour
{
    [Header("Raíz del menú (para ExitMenu)")]
    [SerializeField] private GameObject menuRoot;

    public enum InitialView { SoloMenu, Modelo3D, DICOM }

    [Tooltip("Que se ve al arrancar la escena. SoloMenu deja todas las vistas ocultas.")]
    [SerializeField] private InitialView initialView = InitialView.SoloMenu;

    [Header("Vistas")]
    [Tooltip("Objetos que se muestran al pulsar VER DICOM.")]
    [SerializeField] private GameObject[] dicomObjects;

    [Tooltip("Objetos que se muestran al pulsar VER SEGMENTACIÓN.")]
    [SerializeField] private GameObject[] segmentationObjects;

    [Tooltip("Objetos que se muestran al pulsar VER MODELO 3D.")]
    [SerializeField] private GameObject[] model3DObjects;

    /// <summary>
    /// Sin esto la escena arranca con TODAS las vistas visibles a la vez (el menú, los
    /// órganos y la pantalla DICOM encimados), porque el cambio de vista solo ocurría
    /// al pulsar un botón.
    /// </summary>
    private void Start()
    {
        switch (initialView)
        {
            case InitialView.Modelo3D:
                ShowView(model3DObjects, "Modelo 3D (vista inicial)");
                break;

            case InitialView.DICOM:
                ShowView(dicomObjects, "DICOM (vista inicial)");
                break;

            default:
                // Sin esto la escena arranca con TODAS las vistas encimadas, porque el
                // cambio de vista solo ocurria al pulsar un boton.
                SetGroupActive(dicomObjects, false);
                SetGroupActive(segmentationObjects, false);
                SetGroupActive(model3DObjects, false);
                Debug.Log("[MedicalViewer] Vista inicial: solo el menu.");
                break;
        }
    }

    public void OpenDICOM()
    {
        ShowView(dicomObjects, "DICOM");
    }

    public void OpenSegmentation()
    {
        ShowView(segmentationObjects, "Segmentación");
    }

    public void Open3DModel()
    {
        ShowView(model3DObjects, "Modelo 3D");
    }

    /// <summary>
    /// Oculta el menú con la animación de salida. Se vuelve a abrir con el botón
    /// Menu o B/Y del control (ver MenuToggleInput), así que no deja al usuario
    /// atrapado sin interfaz.
    /// </summary>
    public void ExitMenu()
    {
        if (menuRoot == null)
        {
            Debug.LogWarning("[MedicalViewer] ExitMenu: falta asignar 'Menu Root' en el Inspector.");
            return;
        }

        if (menuRoot.TryGetComponent(out MedicalMenuIntro intro))
        {
            intro.PlayHide();
        }
        else
        {
            menuRoot.SetActive(false);
        }

        Debug.Log("[MedicalViewer] Menú cerrado (botón Menu o B/Y para reabrirlo).");
    }

    private void ShowView(GameObject[] target, string label)
    {
        if (target == null || target.Length == 0)
        {
            Debug.LogWarning($"[MedicalViewer] La vista '{label}' todavía no tiene objetos asignados. " +
                             "Arrástralos al array correspondiente en el Inspector de MedicalMenuActions.");
            return;
        }

        SetGroupActive(dicomObjects, ReferenceEquals(target, dicomObjects));
        SetGroupActive(segmentationObjects, ReferenceEquals(target, segmentationObjects));
        SetGroupActive(model3DObjects, ReferenceEquals(target, model3DObjects));

        Debug.Log($"[MedicalViewer] Vista activa: {label}");
    }

    private static void SetGroupActive(GameObject[] group, bool active)
    {
        if (group == null) return;

        foreach (var go in group)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
