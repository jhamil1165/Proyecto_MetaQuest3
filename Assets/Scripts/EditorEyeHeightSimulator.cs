using System.Collections;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// El tracking del proyecto es Floor Level: el CenterEyeAnchor representa los ojos
/// medidos DESDE EL SUELO, y es el runtime del Quest quien lo sube a la altura real
/// del usuario. En el editor sin headset nadie hace eso, así que la cámara se queda
/// a ras del piso y todo el contenido (menú a 1,5 m, órganos a 1,15 m) aparece muy
/// por encima de la vista.
///
/// Este componente sube el rig SOLO en esa situación, en tiempo de ejecución:
///   - No modifica la escena guardada (el cambio se revierte al salir de Play).
///   - No se ejecuta en el build (guardado con UNITY_EDITOR).
///   - No se ejecuta si hay un headset real (Quest Link incluido).
/// </summary>
[DefaultExecutionOrder(1000)]
public class EditorEyeHeightSimulator : MonoBehaviour
{
    [Tooltip("Altura de ojos que se simula en el editor cuando no hay headset.")]
    [SerializeField] private float simulatedEyeHeight = 1.6f;

    [Tooltip("Margen para que el subsistema XR alcance a reportar un headset antes de decidir.")]
    [SerializeField] private float detectionDelay = 0.5f;

    private IEnumerator Start()
    {
#if UNITY_EDITOR
        yield return new WaitForSeconds(detectionDelay);

        if (IsHeadsetPresent())
        {
            yield break; // hay headset real: el runtime ya coloca la altura correcta
        }

        transform.position += Vector3.up * simulatedEyeHeight;
        Debug.Log($"[MedicalViewer] Editor sin headset: rig elevado {simulatedEyeHeight} m " +
                  "solo para previsualizar. No afecta la escena guardada ni el build.");
#else
        yield break;
#endif
    }

    private static bool IsHeadsetPresent()
    {
        if (XRSettings.isDeviceActive) return true;

        InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        return head.isValid;
    }
}
