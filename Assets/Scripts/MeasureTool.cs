using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Regla para medir sobre los órganos dentro del visor.
///
/// Cómo se usa: se enciende con el botón "Medir" del panel. Se apunta con el mando a un
/// órgano y se pulsa <b>A</b> (o X en el mando izquierdo) para clavar el primer punto, y
/// otra vez para el segundo. Entre los dos aparece una línea con la distancia. La tercera
/// pulsación empieza una medida nueva.
///
/// Por qué el botón A y no el gatillo: el gatillo ya sirve para agarrar los órganos y para
/// pulsar los botones del menú. Midiendo con el gatillo, cada medida movería el órgano.
///
/// <para><b>Lo importante: la escala.</b> Los órganos no están en la escena a tamaño real.
/// Cada uno se agrandó por separado para que se viera bien en su peana, y por eso llevan
/// escalas muy distintas (el hígado unas 111 veces, la vesícula unas 476). Medir la
/// distancia en la escena y llamarla milímetros sería mentir, y además mentiría distinto en
/// cada órgano. Por eso cada órgano trae su factor, sacado de comparar el volumen de su
/// malla con el volumen que ocupa de verdad en la tomografía, y la regla divide por él
/// antes de enseñar el número.</para>
///
/// De ahí sale también la otra regla: los dos puntos tienen que estar en el mismo órgano.
/// Entre dos órganos distintos la distancia no significa nada, porque están colocados en un
/// arco delante del usuario, no en su sitio dentro del cuerpo, y cada uno a su escala.
/// </summary>
public class MeasureTool : MonoBehaviour
{
    /// <summary>Un órgano y cuántas veces se muestra más grande de lo que es.</summary>
    [Serializable]
    public class OrganScale
    {
        public string label;
        public Transform root;

        [Tooltip("Veces que se muestra más grande que en la tomografía. 0 = sin comprobar.")]
        public float displayScale;
    }

    [Header("Mandos")]
    [Tooltip("Rayos de los mandos y de las manos. Se usa el primero que apunte a algo.")]
    [SerializeField] private XRRayInteractor[] rays = new XRRayInteractor[0];

    [Tooltip("Botón que clava un punto. A en el mando derecho, X en el izquierdo.")]
    [SerializeField] private OVRInput.Button placeButton = OVRInput.Button.One;

    [Header("Piezas")]
    [SerializeField] private Transform markerA;
    [SerializeField] private Transform markerB;
    [SerializeField] private LineRenderer line;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Transform labelRoot;

    [Header("Órganos y su escala")]
    [SerializeField] private OrganScale[] organs = new OrganScale[0];

    [Header("Botón del menú")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Color onColor = new Color(0.09f, 0.42f, 0.88f, 1f);
    [SerializeField] private Color offColor = new Color(0.955f, 0.960f, 0.968f, 1f);
    [SerializeField] private Color onText = Color.white;
    [SerializeField] private Color offText = new Color(0.13f, 0.15f, 0.18f, 1f);

    [Tooltip("Objetos que el rayo no debe medir, como la caja del volumen 3D.")]
    [SerializeField] private Collider[] ignored = new Collider[0];

    [SerializeField] private bool measuring;

    private Vector3 _a, _b;
    private int _placed;
    private OrganScale _organA, _organB;
    private Transform _camera;

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        FaceUser();

        if (!measuring) return;

        bool pressed = OVRInput.GetDown(placeButton, OVRInput.Controller.RTouch)
                       || OVRInput.GetDown(placeButton, OVRInput.Controller.LTouch)
                       || OVRInput.GetDown(placeButton);

        if (pressed) TryPlace();
    }

    /// <summary>Enciende o apaga la regla. Enlazable al botón "Medir".</summary>
    public void Toggle()
    {
        measuring = !measuring;
        if (!measuring) Clear();
        else Refresh();
    }

    /// <summary>Borra la medida que haya en pantalla. Enlazable al botón "Borrar".</summary>
    public void Clear()
    {
        _placed = 0;
        _organA = _organB = null;
        Refresh();
    }

    /// <summary>
    /// Clava un punto donde apunte el usuario. Se prueban todos los rayos y vale el primero
    /// que tenga algo delante: así da igual con qué mano se apunte, y da igual que el rig
    /// tenga rayos de mando y de mano a la vez.
    /// </summary>
    private void TryPlace()
    {
        RaycastHit hit = default;
        bool found = false;

        foreach (var ray in rays)
        {
            if (ray == null || !ray.isActiveAndEnabled) continue;
            if (!ray.TryGetCurrent3DRaycastHit(out hit)) continue;
            if (IsIgnored(hit.collider)) { Say("Sobre el volumen no se puede medir"); return; }
            found = true;
            break;
        }

        if (!found)
        {
            Say("Apunta a un órgano");
            return;
        }

        OrganScale organ = Owner(hit.collider.transform);
        if (organ == null)
        {
            Say("Eso no es un órgano medible");
            return;
        }

        // La tercera pulsación empieza de cero, para no tener que borrar a mano.
        if (_placed >= 2) { _placed = 0; _organA = _organB = null; }

        if (_placed == 0) { _a = hit.point; _organA = organ; }
        else
        {
            if (organ != _organA)
            {
                Say("Los dos puntos, en el mismo órgano");
                return;
            }
            _b = hit.point;
            _organB = organ;
        }

        _placed++;
        Refresh();
    }

    /// <summary>De qué órgano es el objeto que tocó el rayo, subiendo por la jerarquía.</summary>
    private OrganScale Owner(Transform hit)
    {
        for (Transform t = hit; t != null; t = t.parent)
        {
            foreach (var organ in organs)
            {
                if (organ != null && organ.root == t) return organ;
            }
        }
        return null;
    }

    private bool IsIgnored(Collider collider)
    {
        foreach (var other in ignored)
        {
            if (other != null && other == collider) return true;
        }
        return false;
    }

    private void Refresh()
    {
        bool one = measuring && _placed >= 1;
        bool two = measuring && _placed >= 2;

        if (markerA != null)
        {
            markerA.gameObject.SetActive(one);
            if (one) markerA.position = _a;
        }

        if (markerB != null)
        {
            markerB.gameObject.SetActive(two);
            if (two) markerB.position = _b;
        }

        if (line != null)
        {
            line.enabled = two;
            if (two)
            {
                line.positionCount = 2;
                line.SetPosition(0, _a);
                line.SetPosition(1, _b);
            }
        }

        if (labelRoot != null)
        {
            labelRoot.gameObject.SetActive(measuring);
            if (two) labelRoot.position = (_a + _b) * 0.5f + Vector3.up * 0.06f;
        }

        if (label != null)
        {
            if (!measuring) label.text = string.Empty;
            else if (_placed == 0) label.text = "Pulsa A sobre un órgano";
            else if (_placed == 1) label.text = "Ahora el segundo punto";
            else label.text = Format(Vector3.Distance(_a, _b), _organB);
        }

        Highlight();
    }

    /// <summary>
    /// Pasa la distancia de la escena a medida real dividiendo por lo agrandado que esté el
    /// órgano. Sin factor comprobado se avisa, en vez de dar un número que parezca bueno.
    /// </summary>
    private static string Format(float sceneMetres, OrganScale organ)
    {
        if (organ == null || organ.displayScale <= 0f)
            return "escala sin comprobar";

        float mm = sceneMetres * 1000f / organ.displayScale;
        return mm < 100f
            ? string.Format("{0:F0} mm", mm)
            : string.Format("{0:F1} cm", mm / 10f);
    }

    private void Say(string message)
    {
        if (label != null) label.text = message;
    }

    private void Highlight()
    {
        if (toggleButton == null) return;

        if (toggleButton.targetGraphic != null)
            toggleButton.targetGraphic.color = measuring ? onColor : offColor;

        var text = toggleButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.color = measuring ? onText : offText;
    }

    /// <summary>
    /// El número mira siempre al usuario. Con una rotación fija se lee del revés en cuanto
    /// uno se mueve alrededor del órgano.
    /// </summary>
    private void FaceUser()
    {
        if (labelRoot == null || !labelRoot.gameObject.activeSelf) return;

        if (_camera == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _camera = cam.transform;
        }

        labelRoot.rotation = Quaternion.LookRotation(_camera.forward, Vector3.up);
    }
}
