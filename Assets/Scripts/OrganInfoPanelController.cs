using TMPro;
using UnityEngine;

/// <summary>
/// Single floating glass panel shared by every selectable organ. The root object
/// stays active (so this component registers itself), while the "visual" child is
/// toggled on/off - that child carries MedicalMenuIntro, so the panel re-plays the
/// same fade + scale-in as the main menu every time it appears.
///
/// Placement: to the side of the selected object's bounds, billboarded to match the
/// camera's own rotation (the same convention Medical_Menu uses).
/// </summary>
public class OrganInfoPanelController : MonoBehaviour
{
    public static OrganInfoPanelController Instance { get; private set; }

    [SerializeField] private GameObject visual;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private float sideMargin = 0.15f;

    private Transform _currentTarget;
    private Transform _cameraTransform;

    private void Awake()
    {
        Instance = this;
        if (visual != null)
        {
            visual.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void Show(Transform target, string title, string body)
    {
        _currentTarget = target;

        if (titleText != null) titleText.text = title;
        if (bodyText != null) bodyText.text = body;

        PlaceNear(target);

        if (visual != null && !visual.activeSelf)
        {
            visual.SetActive(true); // OnEnable on the child replays the intro animation
        }
    }

    public void Hide(Transform target)
    {
        // Ignore a stale hide from an organ that is no longer the one being shown.
        if (target != null && _currentTarget != target) return;

        _currentTarget = null;
        if (visual != null)
        {
            visual.SetActive(false);
        }
    }

    private void LateUpdate()
    {
        if (_currentTarget == null) return;
        PlaceNear(_currentTarget);
    }

    private Transform GetCameraTransform()
    {
        if (_cameraTransform != null) return _cameraTransform;

        Camera cam = Camera.main;
        if (cam == null) cam = FindObjectOfType<Camera>();
        if (cam != null) _cameraTransform = cam.transform;

        return _cameraTransform;
    }

    private void PlaceNear(Transform target)
    {
        if (target == null) return;

        Transform cam = GetCameraTransform();
        Bounds bounds = GetWorldBounds(target);

        Vector3 right = cam != null ? cam.right : Vector3.right;
        transform.position = bounds.center + right * (bounds.extents.magnitude + sideMargin);

        if (cam != null)
        {
            // Match the camera's rotation, same as Medical_Menu does, so the panel front faces the viewer.
            transform.rotation = Quaternion.LookRotation(cam.forward, Vector3.up);
        }
    }

    private static Bounds GetWorldBounds(Transform target)
    {
        var renderers = target.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(target.position, Vector3.one * 0.1f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }
}
