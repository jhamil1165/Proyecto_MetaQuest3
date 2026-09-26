using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controles del plano de corte: interruptor de encendido, centrado y giro.
///
/// Los mismos métodos los usan el panel del menú (botones que se pulsan con la mano o
/// con el rayo) y el botón B/Y del mando, para que el usuario pueda cortar cuando quiera
/// en vez de encontrarse el órgano ya cortado.
/// </summary>
[RequireComponent(typeof(ClippingPlaneController))]
public class ClippingPlaneInput : MonoBehaviour
{
    [SerializeField] private ClippingPlaneController controller;
    [SerializeField] private ClippingPlaneAnchor anchor;

    [Tooltip("Lo que se ve del plano: se oculta cuando el corte está apagado.")]
    [SerializeField] private Renderer planeRenderer;

    [Tooltip("Colliders del plano: sin corte no debe estorbar al agarrar los órganos.")]
    [SerializeField] private Collider planeCollider;

    [Header("Botón del menú")]
    [Tooltip("Texto del botón: cambia entre activar y desactivar.")]
    [SerializeField] private TMP_Text stateLabel;

    [Tooltip("Fondo del botón: se pinta de color cuando el corte está activo.")]
    [SerializeField] private Image stateBackground;

    [SerializeField] private Color onColor = new Color(0.09f, 0.42f, 0.88f, 1f);
    [SerializeField] private Color offColor = new Color(0.955f, 0.960f, 0.968f, 1f);
    [SerializeField] private Color onText = Color.white;
    [SerializeField] private Color offText = new Color(0.13f, 0.15f, 0.18f, 1f);

    [Header("Mando")]
    [Tooltip("Botón B (mano derecha) o Y (izquierda) para activar y desactivar el corte.")]
    [SerializeField] private OVRInput.Button toggleButton = OVRInput.Button.Two;

    [SerializeField] private bool cutting;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<ClippingPlaneController>();
        if (anchor == null) anchor = GetComponent<ClippingPlaneAnchor>();
        if (planeRenderer == null) planeRenderer = GetComponent<Renderer>();
        if (planeCollider == null) planeCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        // El mando es un atajo: el panel del menú hace lo mismo.
        if (OVRInput.GetDown(toggleButton) || OVRInput.GetDown(toggleButton, OVRInput.Controller.LTouch))
        {
            Toggle();
        }
    }

    /// <summary>Enciende o apaga el corte. Enlazable a un interruptor del menú.</summary>
    public void SetCutting(bool isCutting)
    {
        cutting = isCutting;
        Apply();

        // Al encenderlo, el plano aparece donde están los órganos, no donde se quedó.
        if (cutting && anchor != null) anchor.Place();
    }

    public void Toggle()
    {
        SetCutting(!cutting);
    }

    /// <summary>Vuelve a poner el plano sobre los órganos. Enlazable a un botón.</summary>
    public void Center()
    {
        if (anchor != null) anchor.Place();
    }

    /// <summary>Alterna entre corte horizontal y vertical.</summary>
    public void Rotate90()
    {
        transform.Rotate(0f, 0f, 90f, Space.Self);
    }

    private void Apply()
    {
        if (controller != null) controller.SetClippingEnabled(cutting);
        if (planeRenderer != null) planeRenderer.enabled = cutting;
        if (planeCollider != null) planeCollider.enabled = cutting;

        // El botón del menú dice en qué estado está, para no tener que adivinarlo.
        if (stateLabel != null)
        {
            stateLabel.text = cutting ? "Quitar corte" : "Activar corte";
            stateLabel.color = cutting ? onText : offText;
        }

        if (stateBackground != null) stateBackground.color = cutting ? onColor : offColor;
    }
}
