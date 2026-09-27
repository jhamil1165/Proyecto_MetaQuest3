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

    [Header("Joysticks: control fino")]
    [Tooltip("Grados por segundo con el joystick derecho a tope.")]
    [SerializeField] private float rotateSpeed = 45f;

    [Tooltip("Metros por segundo con el joystick izquierdo a tope.")]
    [SerializeField] private float moveSpeed = 0.20f;

    [Tooltip("Por debajo de esto no se mueve: los joysticks nunca descansan del todo en cero.")]
    [SerializeField] private float deadZone = 0.2f;

    [Tooltip("Para no pelearse con la mano mientras se agarra el plano.")]
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable grab;

    [SerializeField] private bool cutting;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<ClippingPlaneController>();
        if (anchor == null) anchor = GetComponent<ClippingPlaneAnchor>();
        if (planeRenderer == null) planeRenderer = GetComponent<Renderer>();
        if (planeCollider == null) planeCollider = GetComponent<Collider>();
        if (grab == null) grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.XRGrabInteractable>();
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

        FineControl();
    }

    /// <summary>
    /// Control fino con los joysticks: el derecho inclina el plano y el izquierdo lo sube y
    /// lo baja por su propia perpendicular.
    ///
    /// Hace falta porque a mano sólo se puede girar con el pulso que uno tenga, y el botón
    /// salta de 90 en 90. Para enseñar un corte concreto eso no vale: hace falta poder ir
    /// grado a grado y milímetro a milímetro.
    ///
    /// Los joysticks están libres: la escena no tiene locomoción, así que no le quitan el
    /// movimiento a nadie. Sólo responden con el corte encendido, para que no se mueva solo
    /// mientras se hace otra cosa, y callan mientras el plano está agarrado con la mano, para
    /// no pelearse con ella.
    /// </summary>
    private void FineControl()
    {
        if (!cutting) return;
        if (grab != null && grab.isSelected) return;

        Vector2 turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        Vector2 shift = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

        if (turn.magnitude > deadZone)
        {
            // Adelante y atrás inclina de frente; izquierda y derecha inclina de lado.
            float pitch = -turn.y * rotateSpeed * Time.deltaTime;
            float roll = -turn.x * rotateSpeed * Time.deltaTime;
            transform.Rotate(pitch, 0f, roll, Space.Self);
        }

        if (Mathf.Abs(shift.y) > deadZone)
        {
            // Por su perpendicular: así sube y baja respecto del corte, no del suelo, y
            // sigue sirviendo con el plano girado.
            transform.position += transform.up * (shift.y * moveSpeed * Time.deltaTime);
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
        if (planeCollider != null) planeCollider.enabled = cutting;

        // Todo lo que se vea del plano, incluido el marco azul del borde, que es un objeto
        // hijo aparte. Antes solo se apagaba la lamina: el marco se quedaba encendido en
        // medio de la escena aunque el corte estuviera apagado.
        foreach (var renderer in GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = cutting;
        }

        if (planeRenderer != null) planeRenderer.enabled = cutting;

        // El botón del menú dice en qué estado está, para no tener que adivinarlo.
        if (stateLabel != null)
        {
            stateLabel.text = cutting ? "Quitar corte" : "Activar corte";
            stateLabel.color = cutting ? onText : offText;
        }

        if (stateBackground != null) stateBackground.color = cutting ? onColor : offColor;
    }
}
