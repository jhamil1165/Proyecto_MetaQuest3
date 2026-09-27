using UnityEngine;

/// <summary>
/// Reabre (o cierra) el menú desde el control del Quest. Va en un objeto que SIEMPRE
/// esté activo - no en el menú mismo, porque cuando el menú se oculta su script
/// dejaría de ejecutarse y no habría forma de recuperarlo.
///
/// Se usa OVRInput (SDK de Meta) porque el rig de la escena es un OVRCameraRig.
///
/// Sólo responde al botón Menú del mando izquierdo. Antes respondía también a B y a Y, y
/// esos dos ya servían para activar el corte: al pulsar Y se encendía el corte y a la vez se
/// escondía el menú, así que desaparecía todo el puesto de trabajo y quedaba flotando el
/// plano de corte sin nada que cortar. Un botón, una acción.
/// </summary>
public class MenuToggleInput : MonoBehaviour
{
    [SerializeField] private GameObject menuRoot;

    [Tooltip("Al reabrir el menu, recolocar el espacio delante del usuario.")]
    [SerializeField] private WorkspaceRecenter recenter;

    private void Update()
    {
        if (menuRoot == null) return;

        // B e Y son del corte. Aquí sólo el botón Menú.
        bool pressed = OVRInput.GetDown(OVRInput.Button.Start);

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
            // Recentrar ANTES de mostrarlo: si no, el menu aparece donde quedo la
            // ultima vez, que puede ser detras del usuario.
            if (recenter != null) recenter.Recenter();

            menuRoot.SetActive(true); // OnEnable dispara la animación de entrada
        }
    }
}
