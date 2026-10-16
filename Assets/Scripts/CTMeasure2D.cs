using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Regla sobre la imagen médica 2D: mide encima del corte del TAC, en milímetros reales.
///
/// Lo pidió el profesor en "Proyecto XR en Cirugía": la medición se hace sobre la imagen
/// original, en el corte donde la estructura se ve mejor, y no hace falta repetirla en todos
/// los cortes. Por eso cada medida recuerda el plano y el corte donde se hizo, y solo se ve
/// cuando ese corte está en pantalla; al pasar a otro corte se esconde, y vuelve al volver.
///
/// Cómo se usa: se pulsa "Medir" en la cabecera del visor, se apunta a la imagen con el mando
/// y se pulsa <b>A</b> en cada extremo. Sale una línea con la distancia. "Borrar" quita todas.
///
/// Los milímetros salen del tamaño de píxel de cada plano, que pone el visor al cargar el
/// estudio (CTPlaneView.MillimetresPerPixel). En una imagen sin escala conocida, como las
/// capturas sueltas de Slicer, se avisa en lugar de dar un número que parezca bueno.
///
/// Las marcas se dibujan dentro de la propia imagen, ancladas en coordenadas relativas a ella:
/// si la imagen se mueve o cambia de tamaño, las marcas van con ella sin recalcular nada.
/// </summary>
public class CTMeasure2D : MonoBehaviour
{
    /// <summary>Una medida, terminada o a medias, sobre un corte concreto.</summary>
    private class Caliper
    {
        public CTPlaneView plane;
        public int slice;
        public Vector2 a;          // en píxeles de la imagen
        public Vector2 b;
        public RectTransform root;
        public RectTransform line;
        public Vector2 ua;         // los mismos puntos, de 0 a 1 sobre la imagen
        public Vector2 ub;
        public Vector2 drawnFor = new Vector2(-1f, -1f);   // tamaño de imagen de la última línea
    }

    [SerializeField] private CTPlaneView[] planes = new CTPlaneView[0];

    [Header("Mandos")]
    [Tooltip("Rayos de los mandos y de las manos. Se usa el primero que apunte a una imagen.")]
    [SerializeField] private XRRayInteractor[] rays = new XRRayInteractor[0];

    [Tooltip("Botón que clava un punto. A en el mando derecho, X en el izquierdo.")]
    [SerializeField] private OVRInput.Button placeButton = OVRInput.Button.One;

    [Header("Botones de la cabecera")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Color onColor = new Color32(0x4C, 0x8D, 0xFF, 255);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color onText = Color.white;
    [SerializeField] private Color offText = new Color32(0xF0, 0xF2, 0xF5, 255);

    [Header("Marcas")]
    [Tooltip("Amarillo, como en los visores radiológicos: se lee bien sobre los grises del TAC.")]
    [SerializeField] private Color markColor = new Color32(0xFF, 0xD5, 0x4A, 255);
    [SerializeField] private float lineWidth = 4f;
    [SerializeField] private float dotSize = 11f;
    [SerializeField] private float textSize = 22f;

    [SerializeField] private bool measuring;

    private readonly List<Caliper> _done = new List<Caliper>();
    private Caliper _pending;
    private string _status;

    private void OnEnable()
    {
        foreach (var plane in planes)
        {
            if (plane != null) plane.SliceChanged += OnSliceChanged;
        }
        Refresh();
    }

    private void OnDisable()
    {
        foreach (var plane in planes)
        {
            if (plane != null) plane.SliceChanged -= OnSliceChanged;
        }
    }

    private void LateUpdate()
    {
        // Escribir en los RectTransform ensucia el Canvas y UGUI lo reconstruye entero, así
        // que las líneas solo se rehacen si algo cambió: este objeto, o el tamaño de la imagen
        // bajo alguna medida (lo reparte el layout tarde y cambia al cargar otra imagen; sin
        // mirarlo, una medida nueva se quedaría con la línea de largo cero).
        if (transform.hasChanged || LinesOutdated())
        {
            RefreshLines();
            transform.hasChanged = false;
        }
    }

    private bool LinesOutdated()
    {
        foreach (var c in _done)
        {
            if (c.line == null || !c.root.gameObject.activeInHierarchy) continue;
            if (c.plane.Display.rectTransform.rect.size != c.drawnFor) return true;
        }
        return false;
    }

    /// <summary>
    /// Ajusta las líneas al tamaño que tiene la imagen ahora mismo.
    ///
    /// Los puntos y el número van anclados en proporción a la imagen y la siguen solos, pero
    /// una línea inclinada no se puede anclar así: su largo y su ángulo dependen del tamaño
    /// real de la imagen, que lo reparte el layout de Unity y puede cambiar después de
    /// dibujarla (al cargar otra imagen, por ejemplo). Calculándola una sola vez salía con
    /// largo cero.
    /// </summary>
    private void RefreshLines()
    {
        foreach (var c in _done)
        {
            if (c.line == null || !c.root.gameObject.activeInHierarchy) continue;

            Rect rect = c.plane.Display.rectTransform.rect;
            Vector2 delta = Vector2.Scale(c.ub - c.ua, rect.size);
            c.drawnFor = rect.size;

            c.line.sizeDelta = new Vector2(delta.magnitude, lineWidth);
            c.line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }

    private void Update()
    {
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
        if (!measuring) CancelPending();
        _status = null;
        Refresh();
    }

    /// <summary>Quita todas las medidas de este visor. Enlazable al botón "Borrar".</summary>
    public void Clear()
    {
        CancelPending();
        foreach (var c in _done)
        {
            if (c.root != null) Kill(c.root.gameObject);
        }
        _done.Clear();
        _status = null;
        Refresh();
    }

    private void TryPlace()
    {
        foreach (var ray in rays)
        {
            if (ray == null || !ray.isActiveAndEnabled) continue;
            if (!ray.TryGetCurrentUIRaycastResult(out RaycastResult hit) || hit.gameObject == null) continue;

            CTPlaneView plane = PlaneOf(hit.gameObject.transform);
            if (plane == null) continue;   // apunta a un botón o a otro panel: no es para nosotros

            if (!ToPixel(plane, hit.worldPosition, out Vector2 px))
            {
                Say("Fuera de imagen");
                return;
            }

            Place(plane, px);
            return;
        }
    }

    /// <summary>Clava un punto en la imagen de un plano, en píxeles de la imagen.</summary>
    private void Place(CTPlaneView plane, Vector2 px)
    {
        if (_pending == null)
        {
            _pending = new Caliper { plane = plane, slice = plane.Slice, a = px };
            Draw(_pending, false);
            Say("2º punto");
            return;
        }

        if (plane != _pending.plane || plane.Slice != _pending.slice)
        {
            Say("Misma imagen");
            return;
        }

        _pending.b = px;
        Draw(_pending, true);
        _done.Add(_pending);
        _pending = null;
        Say(null);
    }

    /// <summary>
    /// Pasa el punto donde chocó el rayo a píxeles de la imagen. Falso si cae fuera de ella
    /// (por ejemplo en el marco oscuro que la rodea).
    /// </summary>
    private static bool ToPixel(CTPlaneView plane, Vector3 world, out Vector2 px)
    {
        px = Vector2.zero;

        RawImage image = plane.Display;
        if (image == null || image.texture == null) return false;

        RectTransform rt = image.rectTransform;
        Vector3 local = rt.InverseTransformPoint(world);
        Rect rect = rt.rect;

        var uv = new Vector2((local.x - rect.xMin) / rect.width, (local.y - rect.yMin) / rect.height);
        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f) return false;

        Rect uvRect = image.uvRect;
        uv = new Vector2(uvRect.x + uv.x * uvRect.width, uvRect.y + uv.y * uvRect.height);

        px = new Vector2(uv.x * image.texture.width, uv.y * image.texture.height);
        return true;
    }

    /// <summary>De qué plano es el objeto que tocó el rayo, subiendo por la jerarquía.</summary>
    private CTPlaneView PlaneOf(Transform hit)
    {
        foreach (var plane in planes)
        {
            if (plane != null && hit.IsChildOf(plane.transform)) return plane;
        }
        return null;
    }

    /// <summary>
    /// Dibuja la medida dentro de la imagen: los dos puntos, la línea y el número. Todo va
    /// anclado en coordenadas relativas a la imagen (0 a 1), así que acompaña a la imagen
    /// pase lo que pase con su tamaño.
    /// </summary>
    private void Draw(Caliper c, bool complete)
    {
        RawImage image = c.plane.Display;
        if (image == null || image.texture == null) return;

        if (c.root == null)
        {
            var go = new GameObject("Medida_2D", typeof(RectTransform));
            c.root = go.GetComponent<RectTransform>();
            c.root.SetParent(image.rectTransform, false);
            c.root.anchorMin = Vector2.zero;
            c.root.anchorMax = Vector2.one;
            c.root.offsetMin = c.root.offsetMax = Vector2.zero;
        }

        // Rehacer lo de dentro: es poco y así no hay estados a medias.
        for (int i = c.root.childCount - 1; i >= 0; i--) Kill(c.root.GetChild(i).gameObject);

        Vector2 texSize = new Vector2(image.texture.width, image.texture.height);
        c.ua = new Vector2(c.a.x / texSize.x, c.a.y / texSize.y);
        Dot(c.root, c.ua);

        if (!complete) return;

        c.ub = new Vector2(c.b.x / texSize.x, c.b.y / texSize.y);
        Dot(c.root, c.ub);

        // La línea arranca en el primer punto; su largo y su ángulo los pone RefreshLines.
        var line = NewImage(c.root, "Linea", markColor);
        line.rectTransform.anchorMin = line.rectTransform.anchorMax = c.ua;
        line.rectTransform.pivot = new Vector2(0f, 0.5f);
        line.rectTransform.anchoredPosition = Vector2.zero;
        line.transform.SetAsFirstSibling();   // por debajo de los puntos
        c.line = line.rectTransform;

        Label(c.root, (c.ua + c.ub) * 0.5f, Format(c));
    }

    private string Format(Caliper c)
    {
        float mmPerPx = c.plane.MillimetresPerPixel;
        if (mmPerPx <= 0f) return "escala desconocida";

        float mm = (c.b - c.a).magnitude * mmPerPx;
        return mm < 100f ? string.Format("{0:F1} mm", mm) : string.Format("{0:F1} cm", mm / 10f);
    }

    private void Dot(RectTransform parent, Vector2 uv)
    {
        var dot = NewImage(parent, "Punto", markColor);
        dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = uv;
        dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        dot.rectTransform.anchoredPosition = Vector2.zero;
        dot.rectTransform.sizeDelta = new Vector2(dotSize, dotSize);
    }

    /// <summary>
    /// El número, sobre una pastilla oscura para que se lea encima de cualquier gris. Un poco
    /// por encima del centro de la línea para no taparla.
    /// </summary>
    private void Label(RectTransform parent, Vector2 uv, string text)
    {
        var pill = NewImage(parent, "Numero", new Color32(0x12, 0x16, 0x1C, 215));
        pill.rectTransform.anchorMin = pill.rectTransform.anchorMax = uv;
        pill.rectTransform.pivot = new Vector2(0.5f, 0f);
        pill.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        pill.rectTransform.sizeDelta = new Vector2(textSize * 5.2f, textSize * 1.5f);

        var go = new GameObject("Texto", typeof(RectTransform));
        go.transform.SetParent(pill.rectTransform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = textSize;
        tmp.color = markColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
    }

    /// <summary>Destroy en ejecución, DestroyImmediate en el editor (para poder probarlo).</summary>
    private static void Kill(GameObject go)
    {
        if (Application.isPlaying) Destroy(go);
        else DestroyImmediate(go);
    }

    private static Image NewImage(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent<Image>();
        image.color = color;

        // Las marcas no deben capturar el rayo: si no, el segundo punto chocaría con el
        // primero en vez de con la imagen.
        image.raycastTarget = false;
        return image;
    }

    /// <summary>Cada medida solo se ve en el corte donde se hizo.</summary>
    private void OnSliceChanged(CTPlaneView plane, int slice)
    {
        // Cambiar de corte a medias anula el punto suelto: no se puede medir entre cortes.
        if (_pending != null && _pending.plane == plane && _pending.slice != slice) CancelPending();

        Refresh();
    }

    private void CancelPending()
    {
        if (_pending == null) return;
        if (_pending.root != null) Kill(_pending.root.gameObject);
        _pending = null;
    }

    private void Say(string status)
    {
        _status = status;
        Refresh();
    }

    private void Refresh()
    {
        foreach (var c in _done)
        {
            if (c.root != null) c.root.gameObject.SetActive(c.plane != null && c.plane.Slice == c.slice);
        }

        if (toggleButton == null) return;

        if (toggleButton.targetGraphic != null)
            toggleButton.targetGraphic.color = measuring ? onColor : offColor;

        var label = toggleButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = measuring ? onText : offText;
            label.text = !measuring ? "Medir"
                       : !string.IsNullOrEmpty(_status) ? _status
                       : "Pulsa A";
        }
    }
}
