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
[RequireComponent(typeof(XRSimpleInteractable))]
public class OrganSelectionInfo : MonoBehaviour
{
    [SerializeField] private string organName = "ÓRGANO";

    [TextArea(2, 6)]
    [SerializeField] private string organInfo = "Información médica pendiente.";

    [SerializeField] private float fadeSpeed = 6f;

    private static readonly int OutlineAlphaId = Shader.PropertyToID("_OutlineAlpha");

    private XRSimpleInteractable _interactable;
    private Renderer[] _renderers;
    private MaterialPropertyBlock _mpb;
    private float _current;
    private float _target;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        _renderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _interactable.selectEntered.AddListener(OnSelectEntered);
        _interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
        _interactable.selectExited.RemoveListener(OnSelectExited);

        if (OrganInfoPanelController.Instance != null)
        {
            OrganInfoPanelController.Instance.Hide(transform);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        _target = 1f;

        if (OrganInfoPanelController.Instance != null)
        {
            OrganInfoPanelController.Instance.Show(transform, organName, organInfo);
        }
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
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
