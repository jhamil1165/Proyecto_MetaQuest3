using System.Collections.Generic;
using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Sujeta el cuerpo y los órganos para que no se muevan mientras se corta.
///
/// Todo lo que se puede mirar se puede también agarrar y colocar, que está bien para
/// examinarlo pero estorba justo cuando se está cortando: al apuntar al plano es fácil rozar
/// el cuerpo que hay detrás, engancharlo sin querer y perder el encuadre que costó conseguir.
///
/// Con la fijación puesta, esos objetos siguen viéndose igual pero no responden al agarre.
/// El plano de corte no se toca: ese tiene que seguir moviéndose, que es de lo que se trata.
///
/// Empieza suelto, como ha sido siempre: quitarle al usuario algo que ya funcionaba, y sin
/// avisar, es peor que el problema que resuelve. Se fija cuando hace falta, antes de cortar.
/// </summary>
public class HoldStill : MonoBehaviour
{
    // Se siguen usando los XRGrabInteractable (apagados) solo como referencia a cada objeto:
    // así no hay que volver a asignar la lista en la escena. El agarre real es el de Meta.
    [Tooltip("Lo que se queda quieto: el volumen y los órganos. El plano de corte no.")]
    [SerializeField] private XRGrabInteractable[] targets = new XRGrabInteractable[0];

    private readonly List<GameObject> _metaGrab = new List<GameObject>();
    private bool _collected;

    [Header("Botón del menú")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Color onColor = new Color32(0x4C, 0x8D, 0xFF, 255);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color onText = Color.white;
    [SerializeField] private Color offText = new Color32(0xF0, 0xF2, 0xF5, 255);

    [SerializeField] private bool holding;

    private void OnEnable()
    {
        Apply();
    }

    /// <summary>Fija o suelta el cuerpo. Enlazable a un botón.</summary>
    public void Toggle()
    {
        holding = !holding;
        Apply();
    }

    public void SetHolding(bool value)
    {
        holding = value;
        Apply();
    }

    private void Apply()
    {
        if (!_collected) CollectMetaGrab();

        // Se apagan los hijos con el agarre de Meta (ISDK_HandGrabInteraction), no el objeto:
        // el modelo se sigue viendo. El XRGrabInteractable ya no se toca: antes se encendía
        // aquí y volvía a activar el agarre de XRI que se había quitado.
        foreach (var grab in _metaGrab)
        {
            if (grab != null && grab.activeSelf == holding) grab.SetActive(!holding);
        }

        if (toggleButton != null)
        {
            if (toggleButton.targetGraphic != null)
                toggleButton.targetGraphic.color = holding ? onColor : offColor;

            var label = toggleButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = holding ? "Fijado" : "Suelto";
                label.color = holding ? onText : offText;
            }
        }
    }

    /// <summary>
    /// Los hijos directos de cada objeto que llevan piezas de interacción de Meta. Se buscan
    /// una sola vez y con los inactivos incluidos, porque al fijar se apagan.
    /// </summary>
    private void CollectMetaGrab()
    {
        _collected = true;
        foreach (var target in targets)
        {
            if (target == null) continue;

            foreach (Transform child in target.transform)
            {
                foreach (var behaviour in child.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is IPointable)
                    {
                        _metaGrab.Add(child.gameObject);
                        break;
                    }
                }
            }
        }
    }
}
