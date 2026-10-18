using UnityEngine;

/// <summary>
/// Ajustes de rendimiento del visor que solo se pueden poner al arrancar.
///
/// El volumen 3D lanza un rayo por cada píxel que tapa: cuanto más cerca está o más ocupa
/// en la vista, más trabajo. Al girar la cabeza o acercarse al modelo era cuando la vista
/// Modelo 3D se trababa. El renderizado foveado dibuja los bordes de la imagen, que el ojo
/// apenas ve nítidos con las lentes del Quest, con menos píxeles, y el dinámico solo lo sube
/// cuando la GPU va justa. Así el centro de la vista conserva toda la calidad.
/// </summary>
public class QuestPerformance : MonoBehaviour
{
    [SerializeField] private OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;
    [SerializeField] private bool dynamicFoveation = true;

    private void Start()
    {
        if (!OVRManager.fixedFoveatedRenderingSupported) return;

        OVRManager.foveatedRenderingLevel = foveation;
        OVRManager.useDynamicFoveatedRendering = dynamicFoveation;
        Debug.Log("[Rendimiento] foveado " + foveation + (dynamicFoveation ? " (dinamico)" : ""));
    }
}
