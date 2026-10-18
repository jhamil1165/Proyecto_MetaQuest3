using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Regla para medir sobre los órganos 3D dentro del visor.
///
/// Cómo se usa: se enciende con el botón "Medir" del panel. Se apunta con el mando a un
/// órgano y se pulsa <b>A</b> (o X en el mando izquierdo) para clavar el primer punto, y
/// otra vez para el segundo. Entre los dos aparece una línea con la distancia. Se pueden
/// hacer varias medidas seguidas; "Borrar" las quita todas.
///
/// Por qué el botón A y no el gatillo: el gatillo ya sirve para agarrar los órganos y para
/// pulsar los botones del menú. Midiendo con el gatillo, cada medida movería el órgano.
///
/// <para><b>Cada medida es hija de su órgano.</b> Lo pidió el profesor: al trasladar o girar
/// el modelo, las medidas tienen que acompañarlo y mantener su sitio sobre él. Antes los
/// puntos se guardaban en coordenadas del mundo y, al mover el órgano, la medida se quedaba
/// flotando donde estaba. Ahora cada medida es un objeto colgado del órgano, con sus dos
/// puntos, su línea y su número dentro.</para>
///
/// <para>Colgarla tal cual no basta: los órganos están agrandados para verse bien en su
/// peana, y un hijo hereda esa escala. Una bolita de 1,4 cm colgada del hígado saldría de
/// metro y medio. Por eso cada medida lleva dentro un contenedor que deshace la escala del
/// órgano, y todo lo que va dentro mide lo que tiene que medir.</para>
///
/// <para><b>La escala también importa para el número.</b> Cada órgano trae su factor (el
/// hígado se ve 1,1 veces más grande, la vesícula 4,7), sacado de comparar el volumen de su
/// malla con el que ocupa en la tomografía, y la regla divide por él antes de enseñar los
/// milímetros. Por eso los dos puntos tienen que estar en el mismo órgano: entre dos órganos
/// distintos la distancia no significa nada, porque cada uno va a su escala y están puestos
/// en un arco delante del usuario, no en su sitio dentro del cuerpo.</para>
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

        [Tooltip("La escala sale de un tamaño de referencia, no de la tomografía del paciente: " +
                 "las medidas se marcan con ≈.")]
        public bool approximate;
    }

    /// <summary>Una medida, terminada o a medias, colgada de su órgano.</summary>
    private class Measurement
    {
        public OrganScale organ;
        public Transform root;
        public Transform a;
        public Transform b;
        public Transform label;
    }

    [Header("Mandos")]
    [Tooltip("Rayos de los mandos y de las manos. Se usa el primero que apunte a algo.")]
    [SerializeField] private XRRayInteractor[] rays = new XRRayInteractor[0];

    [Tooltip("Botón que clava un punto. A en el mando derecho, X en el izquierdo.")]
    [SerializeField] private OVRInput.Button placeButton = OVRInput.Button.One;

    [Header("Piezas (se usan de molde)")]
    [SerializeField] private Transform markerA;
    [SerializeField] private Transform markerB;
    [SerializeField] private LineRenderer line;

    [Tooltip("El texto de ayuda que acompaña al mando mientras se mide.")]
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

    [Header("Medir dentro del volumen")]
    [Tooltip("El volumen 3D. Las medidas sobre su corte cuelgan de él.")]
    [SerializeField] private Transform volumeRoot;

    [Tooltip("Superficies que están dentro del volumen: el plano de corte. Sobre la caja del " +
             "volumen no se mide (es una caja invisible), pero sobre el corte sí: es la " +
             "imagen del TAC del paciente.")]
    [SerializeField] private Collider[] volumeSurfaces = new Collider[0];

    [SerializeField] private bool measuring;

    private readonly List<Measurement> _done = new List<Measurement>();
    private Measurement _pending;
    private Transform _camera;
    private Transform _hand;
    private string _message;

    // La ayuda solo se enseña hasta el primer punto: después ya se sabe usar y el texto
    // estorbaba en medio de la vista. Los avisos de error salen un momento y se van.
    private bool _learned;
    private float _messageUntil;
    private const float MessageSeconds = 1.8f;

    private void OnEnable()
    {
        HideMoulds();
        Refresh();
    }

    private void Update()
    {
        if (!measuring) return;

        bool pressed = OVRInput.GetDown(placeButton, OVRInput.Controller.RTouch)
                       || OVRInput.GetDown(placeButton, OVRInput.Controller.LTouch)
                       || OVRInput.GetDown(placeButton);

        if (pressed) TryPlace();
    }

    private void LateUpdate()
    {
        if (_learned && !string.IsNullOrEmpty(_message) && Time.time > _messageUntil)
        {
            _message = null;
            Refresh();
        }

        PlaceHint();
        FaceUser();
    }

    /// <summary>Enciende o apaga la regla. Enlazable al botón "Medir".</summary>
    public void Toggle()
    {
        measuring = !measuring;

        // Apagar la regla deja de clavar puntos, pero no borra lo medido: las medidas son
        // parte del modelo hasta que alguien pulse "Borrar".
        if (!measuring) CancelPending();

        _message = null;
        Refresh();
    }

    /// <summary>Deja de medir sin borrar lo medido. Lo usa el menú al cambiar de vista.</summary>
    public void Stop()
    {
        if (measuring) Toggle();
    }

    /// <summary>Quita todas las medidas. Enlazable al botón "Borrar".</summary>
    public void Clear()
    {
        CancelPending();

        foreach (var m in _done)
        {
            if (m.root != null) Destroy(m.root.gameObject);
        }
        _done.Clear();

        _message = null;
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
        XRRayInteractor used = null;

        foreach (var ray in rays)
        {
            if (ray == null || !ray.isActiveAndEnabled) continue;

            // Apuntando a un panel no se clava nada en 3D: es la regla 2D la que mide sobre
            // las imágenes, y un punto 3D detrás del panel sería un error del usuario.
            if (ray.TryGetCurrentUIRaycastResult(out var ui) && ui.gameObject != null) return;

            if (!ray.TryGetCurrent3DRaycastHit(out hit)) continue;
            used = ray;
            break;
        }

        if (used == null) { Say("Apunta a un órgano"); return; }
        _hand = used.transform;

        OrganScale organ;
        if (IsIgnored(hit.collider) || IsVolumeSurface(hit.collider))
        {
            // El rayo choca primero con la caja que envuelve el volumen. Se mira si, siguiendo
            // el mismo rayo, más adentro está el plano de corte.
            if (!TryHitCut(used, out hit)) { Say("Activa el corte para medir el volumen"); return; }
            organ = VolumeOrgan();
            if (organ == null) { Say("Activa el corte para medir el volumen"); return; }
        }
        else
        {
            organ = Owner(hit.collider.transform);
        }
        if (organ == null) { Say("Eso no es un órgano medible"); return; }

        if (_pending == null)
        {
            _pending = Begin(organ, hit.point);
            _learned = true;
            _message = null;
            Refresh();
            return;
        }

        if (organ != _pending.organ)
        {
            Say("Los dos puntos, en el mismo órgano");
            return;
        }

        Finish(_pending, hit.point);
        _done.Add(_pending);
        _pending = null;
        _message = null;
        Refresh();
    }

    /// <summary>Empieza una medida colgada del órgano, con su primer punto.</summary>
    private Measurement Begin(OrganScale organ, Vector3 point)
    {
        var root = new GameObject("Medida_" + (_done.Count + 1)).transform;
        root.SetParent(organ.root, false);

        // Deshacer la escala del órgano: todo lo que cuelgue de aquí mide en el mundo lo
        // mismo que mide en local.
        Vector3 s = organ.root.lossyScale;
        root.localScale = new Vector3(SafeInverse(s.x), SafeInverse(s.y), SafeInverse(s.z));
        root.position = point;
        root.rotation = Quaternion.identity;

        var m = new Measurement { organ = organ, root = root };
        m.a = Spawn(markerA, root, point);
        return m;
    }

    /// <summary>Pone el segundo punto, la línea y el número.</summary>
    private void Finish(Measurement m, Vector3 point)
    {
        m.b = Spawn(markerB, m.root, point);

        if (line != null)
        {
            var go = Instantiate(line.gameObject, m.root, false);
            go.name = "Linea";
            go.SetActive(true);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // En coordenadas de la medida: así la línea se mueve con el órgano sin que nadie
            // tenga que recalcularla.
            var lr = go.GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 2;
            lr.SetPosition(0, m.root.InverseTransformPoint(m.a.position));
            lr.SetPosition(1, m.root.InverseTransformPoint(m.b.position));
            lr.enabled = true;
        }

        if (labelRoot != null)
        {
            var go = Instantiate(labelRoot.gameObject, m.root, false);
            go.name = "Numero";
            go.SetActive(true);
            go.transform.position = (m.a.position + m.b.position) * 0.5f + Vector3.up * 0.05f;
            go.transform.localScale = labelRoot.lossyScale;   // la medida mide 1 en el mundo

            var text = go.GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.text = Format(Vector3.Distance(m.a.position, m.b.position), m.organ);

            m.label = go.transform;
        }
    }

    /// <summary>Copia un molde (bolita) dentro de la medida, en su sitio y a su tamaño.</summary>
    private static Transform Spawn(Transform mould, Transform parent, Vector3 point)
    {
        if (mould == null) return null;

        var go = Instantiate(mould.gameObject, parent, false);
        go.SetActive(true);
        go.transform.position = point;
        go.transform.localScale = mould.lossyScale;   // la medida mide 1 en el mundo

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = true;
        return go.transform;
    }

    private void CancelPending()
    {
        if (_pending == null) return;

        if (_pending.root != null) Destroy(_pending.root.gameObject);
        _pending = null;
    }

    /// <summary>Los moldes no se ven nunca: solo sirven para sacar copias.</summary>
    private void HideMoulds()
    {
        if (markerA != null) markerA.gameObject.SetActive(false);
        if (markerB != null) markerB.gameObject.SetActive(false);
        if (line != null) line.enabled = false;
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

    private bool IsVolumeSurface(Collider collider)
    {
        foreach (var other in volumeSurfaces)
        {
            if (other != null && other == collider) return true;
        }
        return false;
    }

    /// <summary>Busca el plano de corte a lo largo del rayo, aunque haya algo delante.</summary>
    private bool TryHitCut(XRRayInteractor ray, out RaycastHit cut)
    {
        cut = default;
        Transform origin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform;
        float best = float.MaxValue;
        bool found = false;

        foreach (var h in Physics.RaycastAll(origin.position, origin.forward, ray.maxRaycastDistance,
                                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            if (!IsVolumeSurface(h.collider) || !h.collider.enabled) continue;
            if (h.distance < best) { best = h.distance; cut = h; found = true; }
        }
        return found;
    }

    private OrganScale VolumeOrgan()
    {
        foreach (var organ in organs)
        {
            if (organ != null && organ.root != null && organ.root == volumeRoot) return organ;
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

    private void Say(string message)
    {
        _message = message;
        _messageUntil = Time.time + MessageSeconds;
        Refresh();
    }

    private void Refresh()
    {
        // Después del primer punto la ayuda solo aparece para un aviso.
        bool show = measuring && (!_learned || !string.IsNullOrEmpty(_message));
        if (labelRoot != null) labelRoot.gameObject.SetActive(show);

        if (label != null)
        {
            label.text = !measuring ? string.Empty
                       : !string.IsNullOrEmpty(_message) ? _message
                       : _pending != null ? "Ahora el segundo punto"
                       : "Pulsa A sobre un órgano";
        }

        Highlight();
    }

    /// <summary>
    /// La ayuda va con el usuario, no clavada en su cara: sobre el primer punto si hay una
    /// medida a medias, y si no, encima del mando con el que se apunta. Meta desaconseja los
    /// textos pegados a la cabeza porque cansan; pegados a la mano se leen cuando se miran.
    /// </summary>
    private void PlaceHint()
    {
        if (!measuring || labelRoot == null) return;

        if (_pending != null && _pending.a != null)
        {
            labelRoot.position = _pending.a.position + Vector3.up * 0.06f;
            return;
        }

        if (_hand == null)
        {
            foreach (var ray in rays)
            {
                if (ray != null && ray.isActiveAndEnabled) { _hand = ray.transform; break; }
            }
        }

        if (_hand != null) labelRoot.position = _hand.position + Vector3.up * 0.08f + _hand.forward * 0.06f;
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
        string prefix = organ.approximate ? "≈ " : "";
        return mm < 100f
            ? string.Format("{0}{1:F0} mm", prefix, mm)
            : string.Format("{0}{1:F1} cm", prefix, mm / 10f);
    }

    private static float SafeInverse(float v)
    {
        return Mathf.Abs(v) < 1e-6f ? 1f : 1f / v;
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
    /// Los números miran siempre al usuario. Con una rotación fija se leen del revés en
    /// cuanto uno se mueve alrededor del órgano.
    /// </summary>
    private void FaceUser()
    {
        if (_camera == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _camera = cam.transform;
        }

        Quaternion facing = Quaternion.LookRotation(_camera.forward, Vector3.up);

        if (labelRoot != null && labelRoot.gameObject.activeSelf) labelRoot.rotation = facing;

        foreach (var m in _done)
        {
            if (m.label != null) m.label.rotation = facing;
        }
    }
}
