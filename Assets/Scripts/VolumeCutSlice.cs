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
/// El plano que corta el volumen lo publica ClippingPlaneController como propiedad global
/// de shader, igual que para los órganos; aquí ya no se escribe en el volumen (antes iba por
/// MaterialPropertyBlock, que deja al renderer fuera del SRP Batcher). Este componente solo
/// coloca la rebanada del TAC.
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

    private static readonly int WorldToObjectId = Shader.PropertyToID("_VolumeWorldToObject");

    private MaterialPropertyBlock _sliceMpb;

    private void Awake()
    {
        _sliceMpb = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (volume == null || planeHandle == null) return;

        bool cutting = controller != null && controller.IsClipping;

        if (!cutting)
        {
            if (slice != null && slice.enabled) slice.enabled = false;
            return;
        }

        // El plano corta donde esta, sin trasladar nada. Antes se le sumaba al centro del
        // volumen el desplazamiento del plano desde su reposo, para que el corte funcionase
        // aunque el plano se hubiese quedado por debajo del volumen. El efecto era que el
        // corte no ocurria donde el usuario veia el plano, sino a una distancia fija de el:
        // se acercaba el plano y el corte iba por otro lado. Eso confunde mas de lo que
        // resuelve, asi que ahora corta exactamente por donde pasa.
        Vector3 normal = planeHandle.up.normalized;
        Vector3 cutPoint = planeHandle.position;

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
}
