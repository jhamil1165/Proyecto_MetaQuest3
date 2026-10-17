using System.Collections;
using UnityEngine;

/// <summary>
/// Se asegura de que la habitación real se vea de fondo, y si no se ve, dice por qué.
///
/// La configuración ya está bien en la escena (OVRManager con passthrough, la capa por detrás
/// de todo y las cámaras borrando a transparente), pero en el visor solo se puede comprobar
/// probándolo. Este componente vuelve a aplicar lo esencial al arrancar, por si algo lo ha
/// cambiado por el camino, y unos segundos después pregunta al SDK de Meta si el passthrough
/// llegó a iniciarse. El resultado queda en el registro del visor (adb logcat, etiqueta
/// [Passthrough]): así, si el fondo sale negro, se sabe si es que el sistema no lo dio o que
/// algo de la escena lo está tapando.
/// </summary>
public class PassthroughGuard : MonoBehaviour
{
    [Tooltip("Segundos que se le dan al sistema para iniciar el passthrough antes de mirar.")]
    [SerializeField] private float checkAfter = 3f;

    private IEnumerator Start()
    {
        var manager = OVRManager.instance != null ? OVRManager.instance : FindObjectOfType<OVRManager>();
        if (manager != null) manager.isInsightPassthroughEnabled = true;

        foreach (var layer in FindObjectsOfType<OVRPassthroughLayer>(true))
        {
            if (!layer.gameObject.activeSelf) layer.gameObject.SetActive(true);
            layer.enabled = true;
        }

        // El passthrough solo se ve a través de lo que la cámara deja transparente.
        foreach (var cam in FindObjectsOfType<Camera>())
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        yield return new WaitForSeconds(checkAfter);

        if (OVRManager.IsInsightPassthroughInitialized())
        {
            Debug.Log("[Passthrough] iniciado: la habitación debería verse de fondo.");
        }
        else if (OVRManager.HasInsightPassthroughInitFailed())
        {
            Debug.LogWarning("[Passthrough] el sistema no pudo iniciarlo. Revisa en el visor " +
                             "Ajustes > Privacidad que la app tenga permiso de espacio, y que " +
                             "el passthrough esté activado en el Quest.");
        }
        else
        {
            Debug.LogWarning("[Passthrough] sigue sin iniciarse tras " + checkAfter + " s.");
        }
    }
}
