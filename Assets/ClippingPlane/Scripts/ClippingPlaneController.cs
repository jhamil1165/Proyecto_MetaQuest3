using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mueve un plano de corte (un Transform que el usuario agarra en VR) y actualiza,
/// cada frame, la propiedad de plano del shader "APOSE/URP_ClippingPlane" en los
/// renderers indicados. Usa MaterialPropertyBlock para no crear instancias nuevas
/// de material por cada organo.
///
/// El plano de corte del shader se define en espacio de mundo como
/// dot(posicionMundo, normal) + distancia = 0. La normal y la distancia se derivan
/// del eje "up" (verde, Y) y de la posicion de "planeHandle" en cada frame, asi que
/// mover o rotar ese Transform en el editor o con la mano mueve el corte.
/// </summary>
public class ClippingPlaneController : MonoBehaviour
{
    [Tooltip("Transform que define el plano de corte (su eje Y = normal del plano).")]
    [SerializeField] private Transform planeHandle;

    [Tooltip("Renderers a los que se les aplica el corte (el organo o los organos con Mat_ClippingPlane).")]
    [SerializeField] private List<Renderer> targets = new List<Renderer>();

    [Tooltip("Si esta apagado, el plano no corta nada (se manda muy lejos).")]
    [SerializeField] private bool clippingEnabled = true;

    private static readonly int PlaneNormalId = Shader.PropertyToID("_PlaneNormal");
    private static readonly int PlaneDistanceId = Shader.PropertyToID("_PlaneDistance");

    private const float DisabledDistance = 100000f;

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (planeHandle == null || targets.Count == 0) return;

        Vector3 normal = clippingEnabled ? planeHandle.up.normalized : Vector3.up;
        float distance = clippingEnabled
            ? -Vector3.Dot(normal, planeHandle.position)
            : DisabledDistance;

        foreach (Renderer target in targets)
        {
            if (target == null) continue;

            target.GetPropertyBlock(_mpb);
            _mpb.SetVector(PlaneNormalId, new Vector4(normal.x, normal.y, normal.z, 0f));
            _mpb.SetFloat(PlaneDistanceId, distance);
            target.SetPropertyBlock(_mpb);
        }
    }

    /// <summary>Enciende o apaga el efecto de corte sin desactivar el GameObject.</summary>
    public void SetClippingEnabled(bool isEnabled)
    {
        clippingEnabled = isEnabled;
    }

    /// <summary>Agrega un renderer a la lista de objetos afectados por el corte.</summary>
    public void AddTarget(Renderer target)
    {
        if (target != null && !targets.Contains(target))
        {
            targets.Add(target);
        }
    }
}
