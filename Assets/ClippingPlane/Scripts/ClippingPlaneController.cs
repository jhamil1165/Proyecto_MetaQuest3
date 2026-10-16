using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mueve un plano de corte (un Transform que el usuario agarra en VR) y actualiza,
/// cada frame, el plano de los shaders "APOSE/URP_ClippingPlane" y "MedicalViewer/VolumeRender".
///
/// El plano se publica como propiedad GLOBAL de shader (Shader.SetGlobalVector/Float) y no
/// con MaterialPropertyBlock: un renderer con MaterialPropertyBlock queda fuera del SRP
/// Batcher. Como el plano es el mismo para todos, una global basta y cuesta una sola
/// llamada por frame. Consecuencia: corta todo material con esos shaders, este o no en
/// "targets"; la lista se conserva para que "targets.Count == 0" siga desactivando el corte.
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

    [Tooltip("Renderers con el corte. El plano es global: con la lista vacía no se corta nada.")]
    [SerializeField] private List<Renderer> targets = new List<Renderer>();

    [Tooltip("Si esta apagado, el plano no corta nada (se manda muy lejos).")]
    [SerializeField] private bool clippingEnabled = true;

    private static readonly int PlaneNormalId = Shader.PropertyToID("_PlaneNormal");
    private static readonly int PlaneDistanceId = Shader.PropertyToID("_PlaneDistance");

    // Negativo a proposito: el shader descarta lo que queda del lado positivo del plano.
    // Con +100000 todo quedaba del lado descartado y los organos desaparecian al apagar
    // el corte, que es justo lo contrario de lo que se busca.
    private const float DisabledDistance = -100000f;

    /// <summary>Si el corte está aplicándose ahora mismo. Lo consulta VolumeCutSlice.</summary>
    public bool IsClipping => isActiveAndEnabled && clippingEnabled && planeHandle != null && targets.Count > 0;

    private void LateUpdate()
    {
        if (planeHandle == null || targets.Count == 0) return;

        Vector3 normal = clippingEnabled ? planeHandle.up.normalized : Vector3.up;
        float distance = clippingEnabled
            ? -Vector3.Dot(normal, planeHandle.position)
            : DisabledDistance;

        Shader.SetGlobalVector(PlaneNormalId, new Vector4(normal.x, normal.y, normal.z, 0f));
        Shader.SetGlobalFloat(PlaneDistanceId, distance);
    }

    /// <summary>
    /// Al apagarse (p. ej. al cambiar de vista en el menú) se deja el corte desactivado: la
    /// global sobrevive al componente y, si no, los órganos y el volumen seguirían cortados.
    /// </summary>
    private void OnDisable()
    {
        Shader.SetGlobalVector(PlaneNormalId, new Vector4(0f, 1f, 0f, 0f));
        Shader.SetGlobalFloat(PlaneDistanceId, DisabledDistance);
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
