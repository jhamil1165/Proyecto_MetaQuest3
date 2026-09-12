using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Panel flotante que muestra el estado del hand tracking dentro del headset.
///
/// Depurar hand tracking sin esto es adivinar: no se ve la consola, no hay
/// breakpoints, y un fallo de "no agarra" puede ser la mano no rastreada, la pinza
/// no detectada, o el interactor sin objeto delante. Cada línea distingue un caso.
///
/// Desactiva el GameObject para ocultarlo cuando ya no haga falta.
/// </summary>
public class HandDebugHud : MonoBehaviour
{
    [SerializeField] private TMP_Text output;
    [SerializeField] private OVRHandXRController leftHand;
    [SerializeField] private OVRHandXRController rightHand;
    [SerializeField] private XRRayInteractor leftRay;
    [SerializeField] private XRRayInteractor rightRay;

    private readonly StringBuilder _sb = new StringBuilder();
    private Transform _camera;

    private void Update()
    {
        FaceUser();

        if (output == null) return;

        _sb.Clear();
        Append("IZQUIERDA", leftHand, leftRay);
        _sb.AppendLine();
        Append("DERECHA", rightHand, rightRay);

        output.text = _sb.ToString();
    }

    /// <summary>
    /// Orientar el HUD hacia el usuario en cada frame, en vez de fijar una rotacion.
    /// Con una rotacion fija el texto salia invertido segun como girara la muneca.
    /// </summary>
    private void FaceUser()
    {
        if (_camera == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            _camera = cam.transform;
        }

        // Misma convencion que los paneles del menu: alinear con la vista del usuario
        // deja el texto legible.
        transform.rotation = Quaternion.LookRotation(_camera.forward, Vector3.up);
    }

    private void Append(string label, OVRHandXRController controller, XRRayInteractor ray)
    {
        _sb.Append("<b>").Append(label).AppendLine("</b>");

        if (controller == null)
        {
            _sb.AppendLine("  sin controlador");
            return;
        }

        _sb.Append("  enlazado a: ").AppendLine(controller.BoundHandName);
        _sb.Append("  mano: ").AppendLine(controller.HandTracked ? "RASTREADA" : "no");
        _sb.Append("  pinza: ").Append(controller.PinchStrength.ToString("F2"))
           .Append(controller.IsPinching ? "  AGARRANDO" : "").AppendLine();

        if (controller.SystemGesture) _sb.AppendLine("  gesto de sistema activo");

        if (ray == null)
        {
            _sb.AppendLine("  sin rayo");
            return;
        }

        _sb.Append("  rayo: ").AppendLine(ray.enabled ? "activo" : "apagado");

        // Distinguir "no apunta a nada" de "apunta pero no agarra" es justo lo que
        // separa un problema de puntería de uno de input.
        // interactablesHovered es el estado real que mantiene XRI. GetValidTargets,
        // llamado desde Update, puede devolver vacio aunque si haya hover.
        _sb.Append("  objetivos: ").AppendLine(ray.interactablesHovered.Count.ToString());

        if (ray.interactablesHovered.Count > 0 && ray.interactablesHovered[0] is Object obj)
        {
            _sb.Append("  apuntando: ").AppendLine(obj.name);
        }

        _sb.Append("  seleccionando: ").AppendLine(ray.hasSelection ? "SÍ" : "no");
    }
}
