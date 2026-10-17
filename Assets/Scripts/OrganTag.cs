using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Etiqueta flotante encima de un órgano: su nombre y su volumen real ("Hígado · 1665 mL"),
/// como las de las moléculas del vídeo del hidrógeno. Va siempre encima del órgano, mirando
/// a quien lo ve, y le acompaña cuando se agarra y se mueve.
///
/// La etiqueta no cuelga del órgano en la jerarquía: los órganos están escalados entre 5 y
/// 476 veces, y con eso de padre el texto saldría deformado. Se crea suelta y en cada
/// fotograma se coloca sobre la parte de arriba del órgano, con una patita que lo señala.
///
/// Apagada a medias cuando nadie la mira, para que cinco etiquetas no tapen el modelo; se
/// enciende del todo al apuntar al órgano o al agarrarlo.
/// </summary>
[DisallowMultipleComponent]
public class OrganTag : MonoBehaviour
{
    [SerializeField] private string title = "Órgano";

    [Tooltip("Volumen real medido en la segmentación del TAC. 0 = sin dato, no se muestra.")]
    [SerializeField] private float millilitres;

    [Tooltip("Separación entre la parte de arriba del órgano y la punta de la patita, en metros.")]
    [SerializeField] private float gap = 0.01f;

    [Tooltip("Opacidad cuando no se está apuntando al órgano.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleAlpha = 0.6f;

    [SerializeField] private float fadeSpeed = 6f;

    private static readonly Color PillColor = new Color(0.07f, 0.09f, 0.12f, 0.78f);
    private static readonly Color StemColor = new Color(1f, 1f, 1f, 0.7f);
    private const string AccentHex = "#7FB2FF";

    // Tamaño de la etiqueta: a 0,8 m mide unos 14 cm de ancho, y se agranda con la distancia
    // para que se pueda leer igual de lejos (sin pasarse).
    private const float PixelsToMetres = 0.00046f;
    private const float ReferenceDistance = 0.8f;

    private Transform _tag;
    private CanvasGroup _group;
    private TMP_Text _text;
    private Renderer[] _renderers;
    private XRBaseInteractable _interactable;
    private Transform _camera;
    private float _alpha;
    private bool _hovered;

    public string Title { get => title; set { title = value; Refresh(); } }
    public float Millilitres { get => millilitres; set { millilitres = value; Refresh(); } }

    /// <summary>Texto que lleva la etiqueta.</summary>
    public string Text => millilitres > 0f
        ? string.Format("<b>{0}</b>  <color={1}>·  {2:F0} mL</color>", title, AccentHex, millilitres)
        : "<b>" + title + "</b>";

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _interactable = ResolveInteractable();
        _alpha = idleAlpha;
        Build();
    }

    private void OnEnable()
    {
        if (_tag != null) _tag.gameObject.SetActive(true);
        if (_interactable == null) return;
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        if (_tag != null) _tag.gameObject.SetActive(false);
        if (_interactable == null) return;
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnDestroy()
    {
        if (_tag == null) return;
        if (Application.isPlaying) Destroy(_tag.gameObject);
        else DestroyImmediate(_tag.gameObject);
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => _hovered = true;
    private void OnHoverExited(HoverExitEventArgs args) => _hovered = _interactable.isHovered;

    /// <summary>El mismo criterio que OrganSelectionInfo: el de agarre si está activo.</summary>
    private XRBaseInteractable ResolveInteractable()
    {
        var all = GetComponents<XRBaseInteractable>();
        foreach (var i in all) if (i.enabled && i is XRGrabInteractable) return i;
        foreach (var i in all) if (i.enabled) return i;
        return all.Length > 0 ? all[0] : null;
    }

    private void LateUpdate()
    {
        if (_tag == null) return;

        bool lit = _hovered || (_interactable != null && _interactable.isSelected);
        _alpha = Mathf.MoveTowards(_alpha, lit ? 1f : idleAlpha, Time.deltaTime * fadeSpeed);
        _group.alpha = _alpha;

        Place();
    }

    /// <summary>Coloca la etiqueta sobre el órgano, mirando a la cámara.</summary>
    public void Place()
    {
        if (_tag == null || !TryGetBounds(out Bounds bounds)) return;

        Vector3 anchor = new Vector3(bounds.center.x, bounds.max.y + gap, bounds.center.z);
        _tag.position = anchor;

        Transform cam = ViewCamera();
        if (cam == null) return;

        // Misma orientación que la cabeza, en vez de mirar al punto: así todas las etiquetas
        // quedan paralelas y el texto no se tuerce en los bordes del campo de visión.
        _tag.rotation = Quaternion.LookRotation(cam.forward, Vector3.up);

        float distance = Vector3.Distance(cam.position, anchor);
        float grow = Mathf.Clamp(distance / ReferenceDistance, 0.7f, 2f);
        _tag.localScale = Vector3.one * (PixelsToMetres * grow);
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool any = false;

        foreach (var r in _renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is LineRenderer) continue;   // las medidas cuelgan del órgano: no cuentan
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return any;
    }

    private Transform ViewCamera()
    {
        if (_camera != null) return _camera;
        var cam = UnityEngine.Camera.main;
        if (cam == null) cam = FindObjectOfType<UnityEngine.Camera>();
        _camera = cam != null ? cam.transform : null;
        return _camera;
    }

    private void Refresh()
    {
        if (_text != null) _text.text = Text;
    }

    /// <summary>
    /// Arma la etiqueta: un lienzo en el mundo con el pivote abajo del todo, para que la punta
    /// de la patita caiga justo sobre el órgano y la pastilla quede por encima.
    /// </summary>
    private void Build()
    {
        var root = new GameObject("Etiqueta_" + name, typeof(RectTransform));
        _tag = root.transform;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 5;
        // Sin GraphicRaycaster: la etiqueta no debe quitarle el rayo al órgano.

        _group = root.AddComponent<CanvasGroup>();
        _group.interactable = false;
        _group.blocksRaycasts = false;

        var rect = (RectTransform)_tag;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(320f, 110f);

        const float stem = 34f;

        // Patita: una raya fina desde el órgano hasta la pastilla.
        var stemGo = new GameObject("Patita", typeof(RectTransform));
        stemGo.transform.SetParent(_tag, false);
        var stemRect = (RectTransform)stemGo.transform;
        stemRect.anchorMin = stemRect.anchorMax = new Vector2(0.5f, 0f);
        stemRect.pivot = new Vector2(0.5f, 0f);
        stemRect.sizeDelta = new Vector2(2.5f, stem);
        var stemImage = stemGo.AddComponent<Image>();
        stemImage.color = StemColor;
        stemImage.raycastTarget = false;

        // Puntito en el extremo que toca el órgano.
        var dotGo = new GameObject("Punto", typeof(RectTransform));
        dotGo.transform.SetParent(_tag, false);
        var dotRect = (RectTransform)dotGo.transform;
        dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0f);
        dotRect.sizeDelta = new Vector2(10f, 10f);
        var dot = dotGo.AddComponent<Image>();
        dot.color = StemColor;
        dot.raycastTarget = false;
        var dotRound = dotGo.AddComponent<UIRoundedRect>();
        dotRound.Radius = 5f;

        // Pastilla de vidrio oscuro con el texto, que se ajusta a lo largo del nombre.
        var pillGo = new GameObject("Pastilla", typeof(RectTransform));
        pillGo.transform.SetParent(_tag, false);
        var pillRect = (RectTransform)pillGo.transform;
        pillRect.anchorMin = pillRect.anchorMax = new Vector2(0.5f, 0f);
        pillRect.pivot = new Vector2(0.5f, 0f);
        pillRect.anchoredPosition = new Vector2(0f, stem);
        var pill = pillGo.AddComponent<Image>();
        pill.color = PillColor;
        pill.raycastTarget = false;
        var pillRound = pillGo.AddComponent<UIRoundedRect>();
        pillRound.Radius = 26f;

        var fitter = pillGo.AddComponent<HorizontalLayoutGroup>();
        fitter.padding = new RectOffset(26, 26, 12, 12);
        fitter.childAlignment = TextAnchor.MiddleCenter;
        fitter.childControlWidth = fitter.childControlHeight = true;
        fitter.childForceExpandWidth = fitter.childForceExpandHeight = false;
        var size = pillGo.AddComponent<ContentSizeFitter>();
        size.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        size.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var textGo = new GameObject("Texto", typeof(RectTransform));
        textGo.transform.SetParent(pillGo.transform, false);
        _text = textGo.AddComponent<TextMeshProUGUI>();
        _text.fontSize = 30f;
        _text.color = new Color(0.96f, 0.97f, 0.98f, 1f);
        _text.alignment = TextAlignmentOptions.Center;
        _text.enableWordWrapping = false;
        _text.richText = true;
        _text.raycastTarget = false;
        _text.text = Text;

        Place();
    }
}
