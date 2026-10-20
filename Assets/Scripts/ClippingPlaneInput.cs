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


    [SerializeField] private bool cutting;

    [Header("Orientación fija")]
    [Tooltip("El plano solo se queda en los tres cortes anatómicos (axial, coronal, sagital) " +
             "del volumen. El TAC tiene píxeles de 0,98 mm pero cortes cada 3,27 mm: en " +
             "diagonal el corte cruza capas a medias y se ve escalonado y deformado. En los " +
             "tres planos anatómicos cada píxel cae limpio sobre los datos.")]
    [SerializeField] private bool lockToAnatomical = true;

    [Tooltip("El volumen cuyos ejes marcan los tres cortes.")]
    [SerializeField] private Transform volume;

    [Tooltip("Grados por segundo con los que se endereza al soltarlo.")]
    [SerializeField] private float snapSpeed = 360f;

    // Agarre del plano con el Interaction SDK de Meta (su Grabbable y el hijo
    // ISDK_HandGrabInteraction). Sirve para no pelearse con la mano mientras se agarra.
    private PointableTracker _grab;

    private bool Grabbed => _grab != null && _grab.IsSelected;

    private void Awake()
    {
        if (controller == null) controller = GetComponent<ClippingPlaneController>();
        if (anchor == null) anchor = GetComponent<ClippingPlaneAnchor>();
        if (planeRenderer == null) planeRenderer = GetComponent<Renderer>();
        if (planeCollider == null) planeCollider = GetComponent<Collider>();
        _grab = new PointableTracker(gameObject);
    }

    private void OnEnable()
    {
        _grab.Attach();
        Apply();
    }

    private void OnDisable()
    {
        _grab.Detach();
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
        if (Grabbed) return;

        Vector2 turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
        Vector2 shift = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

        // Con la orientación fija el joystick derecho no inclina: solo se cambia de corte
        // con "Girar 90°".
        if (turn.magnitude > deadZone && !lockToAnatomical)
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

    /// <summary>
    /// Pasa al siguiente corte: axial, coronal, sagital. Sin orientación fija, gira 90°.
    /// </summary>
    public void Rotate90()
    {
        if (!lockToAnatomical || Volume() == null)
        {
            transform.Rotate(0f, 0f, 90f, Space.Self);
            return;
        }

        Vector3[] axes = Axes();
        int next = (Nearest(axes, out _) + 1) % axes.Length;
        transform.rotation = Facing(axes[next], axes[(next + 1) % axes.Length]);
    }

    /// <summary>
    /// Mientras nadie lo agarra, el plano se endereza al corte anatómico más cercano. Agarrado
    /// se mueve libre, para que la mano no luche contra él, y al soltarlo se coloca.
    /// </summary>
    private void LateUpdate()
    {
        if (!lockToAnatomical || !cutting || Volume() == null) return;
        if (Grabbed) return;

        Vector3[] axes = Axes();
        int i = Nearest(axes, out float sign);
        Vector3 normal = axes[i] * sign;

        // El lado del plano se alinea también con un eje del volumen, para que la rebanada
        // no quede girada sobre sí misma.
        Vector3 along = Vector3.ProjectOnPlane(transform.forward, normal);
        if (along.sqrMagnitude < 1e-4f) along = Vector3.ProjectOnPlane(transform.right, normal);
        Vector3 best = along;
        float bestDot = -1f;
        foreach (var axis in axes)
        {
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 candidate = axis * s;
                if (Mathf.Abs(Vector3.Dot(candidate, normal)) > 0.5f) continue;
                float d = Vector3.Dot(candidate, along.normalized);
                if (d > bestDot) { bestDot = d; best = candidate; }
            }
        }

        Quaternion target = Quaternion.LookRotation(best, normal);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, snapSpeed * Time.deltaTime);
    }

    private Transform Volume()
    {
        if (volume == null)
        {
            var go = GameObject.Find("Volumen_3D");
            if (go != null) volume = go.transform;
        }
        return volume;
    }

    /// <summary>Normales de los tres cortes: axial (arriba), coronal (delante), sagital (lado).</summary>
    private Vector3[] Axes()
    {
        return new[] { volume.up, volume.forward, volume.right };
    }

    /// <summary>Cuál de los ejes se parece más a la normal actual del plano, y con qué signo.</summary>
    private int Nearest(Vector3[] axes, out float sign)
    {
        int best = 0;
        float bestDot = -1f;
        sign = 1f;
        for (int i = 0; i < axes.Length; i++)
        {
            float d = Vector3.Dot(transform.up, axes[i]);
            if (Mathf.Abs(d) > bestDot) { bestDot = Mathf.Abs(d); best = i; sign = d >= 0f ? 1f : -1f; }
        }
        return best;
    }

    private static Quaternion Facing(Vector3 normal, Vector3 along)
    {
        return Quaternion.LookRotation(along, normal);
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
