using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Put on a selectable organ (next to its XR Simple Interactable). Selecting it with
/// the VR controller ray fades in the HoloOutline hull and pops the shared info panel
/// next to the model; deselecting reverses both.
///
/// organName / organInfo are placeholders on purpose - fill them in with the real
/// medical data from the Inspector.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(XRBaseInteractable))]
public class OrganSelectionInfo : MonoBehaviour
{
    [SerializeField] private string organName = "ÓRGANO";

    [TextArea(2, 6)]
    [SerializeField] private string organInfo = "Información médica pendiente.";

    [SerializeField] private float fadeSpeed = 6f;

    private static readonly int OutlineAlphaId = Shader.PropertyToID("_OutlineAlpha");

    private XRBaseInteractable _interactable;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _current;
    private float _target;

    private void Awake()
    {
        _interactable = ResolveInteractable();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
    }

    /// <summary>
    /// Un organo puede arrastrar mas de un interactable (por ejemplo un
    /// XRSimpleInteractable antiguo junto al de agarre). GetComponent devolveria el
    /// primero en el orden del componente, que puede ser uno desactivado, y entonces
    /// nunca llegaria un solo evento de hover. Nos quedamos con el que de verdad esta
    /// activo, dando preferencia al de agarre.
    /// </summary>
    private XRBaseInteractable ResolveInteractable()
    {
        var all = GetComponents<XRBaseInteractable>();

        foreach (var i in all)
        {
            if (i.enabled && i is XRGrabInteractable) return i;
        }

        foreach (var i in all)
        {
            if (i.enabled) return i;
        }

        return all.Length > 0 ? all[0] : null;
    }

    private void OnEnable()
    {
        // Se usa hover, no select: con XRGrabInteractable "select" significa agarrar,
        // y la ficha debe aparecer con solo apuntar el organo.
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);

        if (OrganInfoPanelController.Instance != null)
        {
            OrganInfoPanelController.Instance.Hide(transform);
        }
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        _target = 1f;

        if (OrganInfoPanelController.Instance != null)
        {
            OrganInfoPanelController.Instance.Show(transform, organName, organInfo);
        }
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (_interactable.isSelected) return; // agarrado: mantener visible

        _target = 0f;

        if (OrganInfoPanelController.Instance != null)
        {
            OrganInfoPanelController.Instance.Hide(transform);
        }
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
