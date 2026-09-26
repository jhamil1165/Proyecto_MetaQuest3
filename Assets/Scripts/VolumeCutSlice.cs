using UnityEngine;

/// <summary>
/// Corta el volumen 3D y enseña el corte del TAC sobre la rebanada.
///
/// Hace falta porque el plano de corte y el volumen no están a la misma altura: el plano
/// vive entre los órganos sueltos, a la altura del pecho, y el volumen se subió por encima
/// para que no se atravesaran. Si se le pasara al volumen el plano tal cual, el volumen
/// quedaría entero por encima del plano y desaparecería al activar el corte.
///
/// Lo que se hace: se mira cuánto se ha movido el plano desde su sitio de reposo y ese mismo
/// desplazamiento se aplica al centro del volumen. Así el mando mueve las dos cosas a la vez
/// y el usuario ve dónde está cortando, porque la rebanada se dibuja justo ahí.
///
/// Se ejecuta después de ClippingPlaneController (por eso el orden 100): ese escribe el plano
/// en todos sus objetos y aquí se corrige el del volumen.
/// </summary>
[DefaultExecutionOrder(100)]
public class VolumeCutSlice : MonoBehaviour
{
    [SerializeField] private ClippingPlaneController controller;
    [SerializeField] private Transform planeHandle;
    [SerializeField] private Renderer volume;

    [Tooltip("Cuadrado donde se dibuja el corte del TAC.")]
    [SerializeField] private Renderer slice;

    [Tooltip("Lo que mide el volumen en diagonal: la rebanada se hace así de grande para " +
             "cubrirlo gire como gire. Lo que sobra el shader lo descarta.")]
    [SerializeField] private float sliceSize = 1.2f;

    private static readonly int PlaneNormalId = Shader.PropertyToID("_PlaneNormal");
    private static readonly int PlaneDistanceId = Shader.PropertyToID("_PlaneDistance");
    private static readonly int WorldToObjectId = Shader.PropertyToID("_VolumeWorldToObject");

    private const float DisabledDistance = -100000f;

    private MaterialPropertyBlock _volumeMpb;
    private MaterialPropertyBlock _sliceMpb;
    private Vector3 _restPosition;
    private bool _restCaptured;

    private void Awake()
    {
        _volumeMpb = new MaterialPropertyBlock();
        _sliceMpb = new MaterialPropertyBlock();
        CaptureRest();
    }

    private void OnEnable()
    {
        CaptureRest();
    }

    /// <summary>Guarda dónde está el plano cuando no se ha tocado, que es el corte por el centro.</summary>
    private void CaptureRest()
    {
        if (planeHandle == null || _restCaptured) return;

        _restPosition = planeHandle.position;
        _restCaptured = true;
    }

    private void LateUpdate()
    {
        if (volume == null || planeHandle == null) return;

        bool cutting = controller != null && controller.isActiveAndEnabled && IsCutting();

        if (!cutting)
        {
            volume.GetPropertyBlock(_volumeMpb);
            _volumeMpb.SetFloat(PlaneDistanceId, DisabledDistance);
            volume.SetPropertyBlock(_volumeMpb);

            if (slice != null && slice.enabled) slice.enabled = false;
            return;
        }

        CaptureRest();

        // El mismo desplazamiento que lleva el plano, pero aplicado al centro del volumen.
        Vector3 normal = planeHandle.up.normalized;
        Vector3 moved = planeHandle.position - _restPosition;
        Vector3 cutPoint = volume.bounds.center + moved;

        volume.GetPropertyBlock(_volumeMpb);
        _volumeMpb.SetVector(PlaneNormalId, new Vector4(normal.x, normal.y, normal.z, 0f));
        _volumeMpb.SetFloat(PlaneDistanceId, -Vector3.Dot(normal, cutPoint));
        volume.SetPropertyBlock(_volumeMpb);

        if (slice == null) return;

        // La rebanada se pone justo en el corte, mirando hacia donde mira el plano.
        slice.enabled = true;
        slice.transform.position = cutPoint;
        slice.transform.rotation = Quaternion.LookRotation(normal);
        slice.transform.localScale = new Vector3(sliceSize, sliceSize, 1f);

        // El shader necesita saber pasar de coordenadas del mundo a las de la caja del volumen.
        slice.GetPropertyBlock(_sliceMpb);
        _sliceMpb.SetMatrix(WorldToObjectId, volume.transform.worldToLocalMatrix);
        slice.SetPropertyBlock(_sliceMpb);
    }

    /// <summary>
    /// El controlador guarda si el corte está encendido en un campo privado, así que se
    /// deduce de lo que acaba de escribir en el material: con el corte apagado pone una
    /// distancia enorme y negativa.
    /// </summary>
    private bool IsCutting()
    {
        volume.GetPropertyBlock(_volumeMpb);
        return _volumeMpb.GetFloat(PlaneDistanceId) > DisabledDistance * 0.5f;
    }
}
