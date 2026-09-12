using System.Collections;
using UnityEngine;

/// <summary>
/// El tracking del proyecto es Floor Level: el CenterEyeAnchor representa los ojos
/// medidos DESDE EL SUELO, y es el runtime del Quest quien lo sube a la altura real
/// del usuario. En el editor sin headset nadie hace eso, así que la cámara se queda
/// a ras del piso y todo el contenido (menú a 1,5 m, órganos a 1,15 m) aparece muy
/// por encima de la vista.
///
/// La detección NO usa XRSettings.isDeviceActive: en el editor esa API reporta un
/// dispositivo activo aunque no haya headset físico, y el simulador se saltaba solo.
/// En su lugar se mide el síntoma real: si el CenterEyeAnchor sigue a ras del suelo
/// pasado un momento, es que nadie está alimentando una pose de cabeza.
///
///   - No modifica la escena guardada (se revierte al salir de Play).
///   - No se ejecuta en el build (guardado con UNITY_EDITOR).
///   - No se ejecuta si hay pose real (headset o Quest Link), porque entonces
///     el anchor ya está a la altura de los ojos.
/// </summary>
[DefaultExecutionOrder(1000)]
public class EditorEyeHeightSimulator : MonoBehaviour
{
    [Tooltip("Altura de ojos que se simula en el editor cuando no hay headset.")]
    [SerializeField] private float simulatedEyeHeight = 1.6f;

    [Tooltip("Margen para que el runtime alcance a reportar una pose antes de decidir.")]
    [SerializeField] private float detectionDelay = 0.75f;

    [Tooltip("Por debajo de esta altura local se considera que no hay pose real de cabeza.")]
    [SerializeField] private float floorLevelThreshold = 0.2f;

    private IEnumerator Start()
    {
#if UNITY_EDITOR
        yield return new WaitForSeconds(detectionDelay);

        Transform eye = ResolveCenterEyeAnchor();
        if (eye == null)
        {
            Debug.LogWarning("[MedicalViewer] EditorEyeHeightSimulator: no encuentro el CenterEyeAnchor.");
            yield break;
        }

        float eyeHeight = eye.localPosition.y;
        if (eyeHeight > floorLevelThreshold)
        {
            Debug.Log($"[MedicalViewer] Pose de cabeza real detectada (ojos a {eyeHeight:F2} m). " +
                      "El simulador de altura no se aplica.");
            yield break;
        }

        transform.position += Vector3.up * simulatedEyeHeight;
        Debug.Log($"[MedicalViewer] Editor sin headset (ojos a {eyeHeight:F2} m): rig elevado " +
                  $"{simulatedEyeHeight} m solo para previsualizar. No afecta la escena guardada ni el build.");
#else
        yield break;
#endif
    }

    private Transform ResolveCenterEyeAnchor()
    {
        var rig = GetComponent<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null)
        {
            return rig.centerEyeAnchor;
        }

        Transform trackingSpace = transform.Find("TrackingSpace");
        return trackingSpace != null ? trackingSpace.Find("CenterEyeAnchor") : null;
    }
}
