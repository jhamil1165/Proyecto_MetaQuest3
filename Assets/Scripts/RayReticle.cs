using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// El círculo que marca dónde apunta un rayo, siempre visible y con movimiento suave.
///
/// La retícula que trae XRI solo existe cuando el rayo toca algo: al bajar un poco la mano
/// por debajo de un panel desaparecía, y al volver a tocar algo reaparecía de golpe en otro
/// sitio. Además saltaba al instante a cada nuevo punto, que con el temblor de la mano se
/// veía nervioso.
///
/// Aquí el círculo:
///  - si el rayo toca algo, va sobre ese punto, pegado a la superficie;
///  - si no toca nada, se queda flotando en el rayo a la última distancia a la que tocó
///    (así no "salta" hacia delante ni hacia atrás), y más tenue para que se note que no
///    hay nada que pulsar;
///  - siempre se desliza hacia su sitio en vez de teletransportarse.
/// </summary>
[RequireComponent(typeof(XRRayInteractor))]
public class RayReticle : MonoBehaviour
{
    [SerializeField] private GameObject reticlePrefab;

    [Tooltip("Distancia a la que flota el círculo si todavía no ha tocado nada, en metros.")]
    [SerializeField] private float idleDistance = 1.2f;

    [Tooltip("Opacidad del círculo cuando el rayo no toca nada.")]
    [Range(0f, 1f)]
    [SerializeField] private float idleAlpha = 0.45f;

    [Tooltip("Rapidez con la que el círculo llega a su sitio. Más bajo = más suave.")]
    [SerializeField] private float followSpeed = 14f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private XRRayInteractor _ray;
    private Transform _reticle;
    private Renderer _renderer;
    private MaterialPropertyBlock _block;
    private Color _baseColor = Color.white;
    private Vector3 _position;
    private Quaternion _rotation;
    private float _alpha;
    private float _lastHitDistance = -1f;
    private bool _placed;

    private void Awake()
    {
        _ray = GetComponent<XRRayInteractor>();
        if (reticlePrefab == null) return;

        _reticle = Instantiate(reticlePrefab).transform;
        _reticle.name = "Reticula_" + name;
        _renderer = _reticle.GetComponentInChildren<Renderer>();
        _block = new MaterialPropertyBlock();
        if (_renderer != null && _renderer.sharedMaterial != null && _renderer.sharedMaterial.HasProperty(ColorId))
            _baseColor = _renderer.sharedMaterial.GetColor(ColorId);
        _reticle.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (_reticle != null) _reticle.gameObject.SetActive(false);
        _placed = false;
    }

    private void OnDestroy()
    {
        if (_reticle != null) Destroy(_reticle.gameObject);
    }

    private void LateUpdate()
    {
        if (_reticle == null) return;
        // Escondido si el rayo está apagado, si la mano agarra de cerca (HandNearFar le
        // quita el apuntar) o si ya tiene un órgano cogido: ahí el círculo solo estorba.
        if (!_ray.isActiveAndEnabled || !_ray.allowHover || _ray.hasSelection)
        {
            if (_reticle.gameObject.activeSelf) _reticle.gameObject.SetActive(false);
            _placed = false;
            return;
        }

        Transform origin = _ray.rayOriginTransform != null ? _ray.rayOriginTransform : _ray.transform;

        Vector3 target;
        Vector3 normal;
        bool hit = _ray.TryGetHitInfo(out target, out normal, out _, out _);

        if (hit)
        {
            _lastHitDistance = Vector3.Distance(origin.position, target);
        }
        else
        {
            float distance = _lastHitDistance > 0f ? _lastHitDistance : idleDistance;
            target = origin.position + origin.forward * distance;
            normal = -origin.forward;
        }

        // Mirando hacia quien apunta, apoyado en la superficie que toca.
        Quaternion targetRotation = Quaternion.LookRotation(-normal, Vector3.up);

        float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        if (!_placed)
        {
            _position = target;
            _rotation = targetRotation;
            _alpha = hit ? 1f : idleAlpha;
            _placed = true;
        }
        else
        {
            _position = Vector3.Lerp(_position, target, t);
            _rotation = Quaternion.Slerp(_rotation, targetRotation, t);
            _alpha = Mathf.Lerp(_alpha, hit ? 1f : idleAlpha, t);
        }

        _reticle.SetPositionAndRotation(_position, _rotation);
        if (!_reticle.gameObject.activeSelf) _reticle.gameObject.SetActive(true);

        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_block);
            Color c = _baseColor;
            c.a *= _alpha;
            _block.SetColor(ColorId, c);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
