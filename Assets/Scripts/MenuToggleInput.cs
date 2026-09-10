using UnityEngine;

/// <summary>
/// Reabre (o cierra) el menú desde el control del Quest. Va en un objeto que SIEMPRE
/// esté activo - no en el menú mismo, porque cuando el menú se oculta su script
/// dejaría de ejecutarse y no habría forma de recuperarlo.
///
/// Se usa OVRInput (SDK de Meta) porque el rig de la escena es un OVRCameraRig.
/// Botones: Menu (control izquierdo) y B / Y.
/// </summary>
public class MenuToggleInput : MonoBehaviour
{
    [SerializeField] private GameObject menuRoot;

    private void Update()
    {
        if (menuRoot == null) return;

        bool pressed = OVRInput.GetDown(OVRInput.Button.Start) // botón Menu
                       || OVRInput.GetDown(OVRInput.Button.Two)       // B (derecho)
                       || OVRInput.GetDown(OVRInput.Button.Four);     // Y (izquierdo)

        if (!pressed) return;

        if (menuRoot.activeSelf)
        {
            if (menuRoot.TryGetComponent(out MedicalMenuIntro intro))
            {
                intro.PlayHide(); // se desactiva solo al terminar el fade
            }
            else
            {
                menuRoot.SetActive(false);
            }
        }
        else
        {
            menuRoot.SetActive(true); // OnEnable dispara la animación de entrada
        }
    }
}
