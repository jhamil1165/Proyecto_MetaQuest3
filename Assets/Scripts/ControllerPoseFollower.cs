using UnityEngine;

/// <summary>
/// Pone el anclaje del mando exactamente donde está el mando, siempre.
///
/// OVRCameraRig mueve LeftHandAnchor / RightHandAnchor según lo que el SISTEMA cree que se
/// está usando: si cree que son las manos, el anclaje sigue a la muñeca; si cree que son los
/// mandos, sigue al mando. La app decide por su cuenta (InteractorModeSwitch, por movimiento)
/// y los dos no siempre coinciden. Al dejar los mandos en la mesa y volver a cogerlos había
/// un rato en que la app ya estaba en modo mandos pero el sistema seguía con las manos: el
/// modelo del mando y su rayo se colocaban en la postura de la muñeca, girados y caídos hacia
/// abajo.
///
/// Este componente va en LeftControllerAnchor / RightControllerAnchor y, justo después de que
/// el rig actualice sus anclajes, les pone la pose del mando leída directamente, sin pasar
/// por la decisión del sistema.
/// </summary>
public class ControllerPoseFollower : MonoBehaviour
{
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.LTouch;
    [SerializeField] private OVRCameraRig rig;

    private void OnEnable()
    {
        if (rig == null) rig = GetComponentInParent<OVRCameraRig>();
        if (rig != null) rig.UpdatedAnchors += OnUpdatedAnchors;
    }

    private void OnDisable()
    {
        if (rig != null) rig.UpdatedAnchors -= OnUpdatedAnchors;
    }

    private void OnUpdatedAnchors(OVRCameraRig cameraRig)
    {
        if (!OVRInput.GetControllerPositionValid(controller)) return;

        // Las poses de OVRInput están en el espacio de seguimiento: se pasan al mundo con
        // TrackingSpace, que es lo que lleva el giro y el desplazamiento del rig.
        Transform space = cameraRig.trackingSpace;
        Vector3 position = OVRInput.GetLocalControllerPosition(controller);
        Quaternion rotation = OVRInput.GetLocalControllerRotation(controller);

        transform.SetPositionAndRotation(space.TransformPoint(position), space.rotation * rotation);
    }
}
