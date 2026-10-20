using UnityEngine;

/// <summary>
/// Put on a selectable organ (next to its Meta Grabbable). Pointing at it or grabbing it
/// fades in the HoloOutline hull and pops the shared info panel next to the model;
/// letting go of both reverses it.
///
/// organName / organInfo are placeholders on purpose - fill them in with the real
/// medical data from the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class OrganSelectionInfo : MonoBehaviour
{
    [SerializeField] private string organName = "ÓRGANO";

    [TextArea(2, 6)]
    [SerializeField] private string organInfo = "Información médica pendiente.";

    [SerializeField] private float fadeSpeed = 6f;

    private static readonly int OutlineAlphaId = Shader.PropertyToID("_OutlineAlpha");

    private PointableTracker _tracker;
    private bool _shown;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _current;
    private float _target;

    private void Awake()
    {
        _tracker = new PointableTracker(gameObject);
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        // La ficha aparece con solo apuntar el organo, y se mantiene mientras este agarrado.
        _tracker.Changed += OnPointerChanged;
        _tracker.Attach();
    }

    private void OnDisable()
    {
        _tracker.Changed -= OnPointerChanged;
        _tracker.Detach();
        _shown = false;
        _target = 0f;

        if (OrganInfoPanelController.Instance != null)
        {
            OrganInfoPanelController.Instance.Hide(transform);
        }
    }

    private void OnPointerChanged()
    {
        bool show = _tracker.IsHovered || _tracker.IsSelected;
        if (show == _shown) return;

        _shown = show;
        _target = show ? 1f : 0f;

        if (OrganInfoPanelController.Instance == null) return;
        if (show) OrganInfoPanelController.Instance.Show(transform, organName, organInfo);
        else OrganInfoPanelController.Instance.Hide(transform);
    }

    private void Update()
    {
        if (Mathf.Approximately(_current, _target)) return;

        _current = Mathf.MoveTowards(_current, _target, Time.deltaTime * fadeSpeed);

        foreach (var r in _renderers)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OutlineAlphaId, _current);
            r.SetPropertyBlock(_mpb);
        }
    }
}
