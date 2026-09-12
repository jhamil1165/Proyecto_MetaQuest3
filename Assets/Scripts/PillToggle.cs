using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Da al Toggle de Unity el aspecto de interruptor tipo píldora de iOS: la perilla
/// se desliza de un extremo a otro y el fondo cambia de color.
///
/// El Toggle nativo solo sabe mostrar/ocultar un checkmark, así que la animación
/// y el color se manejan aquí.
/// </summary>
[RequireComponent(typeof(Toggle))]
public class PillToggle : MonoBehaviour
{
    [SerializeField] private RectTransform knob;
    [SerializeField] private Image background;
    [SerializeField] private Color onColor = new Color(0.16f, 0.18f, 0.21f);
    [SerializeField] private Color offColor = new Color(0.82f, 0.83f, 0.85f);
    [SerializeField] private float travel = 18f;
    [SerializeField] private float speed = 12f;

    private Toggle _toggle;
    private float _target;
    private float _current;

    private void Awake()
    {
        _toggle = GetComponent<Toggle>();
        _target = _toggle.isOn ? 1f : 0f;
        _current = _target;
        Apply(_current);
    }

    private void OnEnable()
    {
        _toggle.onValueChanged.AddListener(OnChanged);
        _target = _toggle.isOn ? 1f : 0f;
    }

    private void OnDisable()
    {
        _toggle.onValueChanged.RemoveListener(OnChanged);
    }

    private void OnChanged(bool isOn)
    {
        _target = isOn ? 1f : 0f;
    }

    private void Update()
    {
        if (Mathf.Approximately(_current, _target)) return;

        _current = Mathf.MoveTowards(_current, _target, Time.deltaTime * speed);
        Apply(_current);
    }

    private void Apply(float t)
    {
        if (knob != null)
        {
            Vector2 p = knob.anchoredPosition;
            knob.anchoredPosition = new Vector2(Mathf.Lerp(-travel * 0.5f, travel * 0.5f, t), p.y);
        }

        if (background != null)
        {
            background.color = Color.Lerp(offColor, onColor, t);
        }
    }
}
