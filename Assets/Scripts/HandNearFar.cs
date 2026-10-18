using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// De cerca se agarra con la mano; de lejos, con el rayo.
///
/// Cada mano tiene dos formas de coger un órgano: el rayo (apuntar y pinzar a distancia) y el
/// agarre directo (una esfera pequeña en la pinza que coge lo que toca). Las dos estaban
/// activas a la vez: con la mano metida en el corazón, el rayo también lo apuntaba, pintaba
/// su círculo encima y a menudo era él quien se llevaba el agarre, así que el órgano no se
/// movía como si lo tuvieras en la mano.
///
/// Mientras el agarre directo toca algo (o lo tiene cogido), el rayo se aparta: no apunta, no
/// agarra y su círculo se esconde. En cuanto la mano se separa del órgano, vuelve el rayo.
/// </summary>
public class HandNearFar : MonoBehaviour
{
    [SerializeField] private XRRayInteractor ray;
    [SerializeField] private XRDirectInteractor direct;

    private void Update()
    {
        if (ray == null || direct == null) return;

        // Solo de cerca si de verdad tiene algo en la mano, o si toca un órgano sin estar
        // apuntando a un panel. Antes bastaba con tener algo agarrable a 9 cm: con la mano
        // descansando junto al plano de corte o a un órgano de la mesa, su rayo se apagaba
        // sin que se notara y esa mano no podía pulsar ningún botón.
        bool pointingAtPanel = ray.TryGetCurrentUIRaycastResult(out var ui) && ui.gameObject != null;
        bool near = direct.isActiveAndEnabled &&
                    (direct.hasSelection || (direct.hasHover && !pointingAtPanel));

        // Si el rayo ya tiene algo cogido, se le deja terminar: quitárselo a medias lo soltaría.
        if (ray.hasSelection) near = false;

        ray.allowHover = !near;
        ray.allowSelect = !near;
    }

    private void OnDisable()
    {
        if (ray == null) return;
        ray.allowHover = true;
        ray.allowSelect = true;
    }
}
