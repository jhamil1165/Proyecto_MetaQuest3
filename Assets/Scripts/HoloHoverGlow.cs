using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Drives the "_HoverGlow" property of a MedicalViewer/HoloGlassPanel material
/// instance (via MaterialPropertyBlock, so it never creates a material clone)
/// based on hover/select state reported by the XR Simple Interactable already
/// on this object. Smoothly eases in/out so the glow doesn't pop.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class HoloHoverGlow : MonoBehaviour
{
    [SerializeField] private float glowTarget = 1.5f;
    [SerializeField] private float lerpSpeed = 8f;

    private static readonly int HoverGlowId = Shader.PropertyToID("_HoverGlow");

    private Renderer _renderer;
    private XRSimpleInteractable _interactable;
    private MaterialPropertyBlock _mpb;
    private float _current;
    private float _target;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _interactable = GetComponent<XRSimpleInteractable>();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
        _interactable.selectEntered.AddListener(OnSelectEntered);
        _interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        _interactable.selectExited.RemoveListener(OnSelectExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args) => _target = glowTarget;
    private void OnHoverExited(HoverExitEventArgs args) => _target = _interactable.isSelected ? glowTarget : 0f;
    private void OnSelectEntered(SelectEnterEventArgs args) => _target = glowTarget;
    private void OnSelectExited(SelectExitEventArgs args) => _target = _interactable.isHovered ? glowTarget : 0f;

    private void Update()
    {
        if (Mathf.Approximately(_current, _target)) return;

        _current = Mathf.Lerp(_current, _target, Time.deltaTime * lerpSpeed);
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HoverGlowId, _current);
        _renderer.SetPropertyBlock(_mpb);
    }
}
