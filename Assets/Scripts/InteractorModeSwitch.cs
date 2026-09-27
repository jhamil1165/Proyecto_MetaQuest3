using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Alterna entre manos y mandos, dejando activo solo un juego de interactores.
///
/// Sin esto conviven cuatro rayos: los dos de los mandos siguen dibujándose desde
/// donde quedaron aunque ya no se rastreen, y además compiten por los mismos órganos.
/// Un XRGrabInteractable en modo Single admite un solo interactor, así que esa
/// competencia puede impedir el agarre con la mano.
/// </summary>
public class InteractorModeSwitch : MonoBehaviour
{
    [Header("Manos")]
    [SerializeField] private List<GameObject> handInteractors = new List<GameObject>();

    [Header("Mandos")]
    [SerializeField] private List<GameObject> controllerInteractors = new List<GameObject>();
    [Tooltip("Modelos 3D de los mandos, para ocultarlos al usar manos.")]
    [SerializeField] private List<GameObject> controllerModels = new List<GameObject>();

    [Header("Detección")]
    [SerializeField] private OVRHand leftHand;
    [SerializeField] private OVRHand rightHand;

    [Tooltip("Tiempo sin tocar los mandos antes de pasar a manos. Evita que el modo salte " +
             "de uno a otro por un roce.")]
    [SerializeField] private float switchDelay = 1.2f;

    private bool _handsActive;
    private bool _initialised;
    private float _lastControllerUse;

    private void Update()
    {
        // Se mira si alguien esta USANDO los mandos, no si estan encendidos. Dejandolos en la
        // mesa siguen conectados y con cualquier temblor el sistema los da por activos: por
        // eso el modo saltaba a manos y volvia solo a los mandos con los mandos en la mesa.
        if (ControllersInUse()) _lastControllerUse = Time.time;

        bool hands = HandsInUse() && (Time.time - _lastControllerUse) > switchDelay;

        if (_initialised && hands == _handsActive) return;

        _initialised = true;
        _handsActive = hands;
        Apply(hands);
    }

    /// <summary>
    /// Si hay una mano de verdad puesta en un mando.
    ///
    /// La pista buena es el sensor de contacto: los mandos detectan el dedo apoyado aunque no
    /// se apriete nada, y en la mesa no hay ningun dedo apoyado. Ademas vale cualquier uso
    /// real (boton, gatillo o joystick), por si alguien los sujeta con guantes y el sensor de
    /// contacto no lo nota.
    /// </summary>
    private static bool ControllersInUse()
    {
        foreach (var side in new[] { OVRInput.Controller.LTouch, OVRInput.Controller.RTouch })
        {
            if (OVRInput.Get(OVRInput.Touch.Any, side)) return true;
            if (OVRInput.Get(OVRInput.Button.Any, side)) return true;
            if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, side) > 0.15f) return true;
            if (OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, side) > 0.15f) return true;
            if (OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, side).magnitude > 0.2f) return true;
        }

        return false;
    }

    /// <summary>
    /// Si el usuario esta usando las manos.
    ///
    /// Se pregunta primero al propio sistema, que es quien mejor lo sabe. Si no lo tiene
    /// claro se mira el rastreo, exigiendo confianza alta: con confianza baja la mano da
    /// tumbos y es peor que no tenerla.
    /// </summary>
    private bool HandsInUse()
    {
        if (OVRInput.GetActiveController() == OVRInput.Controller.Hands) return true;
        return Confident(leftHand) || Confident(rightHand);
    }

    private static bool Confident(OVRHand hand)
    {
        return hand != null && hand.IsTracked &&
               hand.HandConfidence == OVRHand.TrackingConfidence.High;
    }

    private void Apply(bool useHands)
    {
        SetAll(handInteractors, useHands);
        SetAll(controllerInteractors, !useHands);
        SetAll(controllerModels, !useHands);

        Debug.Log($"[MedicalViewer] Modo de interacción: {(useHands ? "MANOS" : "MANDOS")}");
    }

    private static void SetAll(List<GameObject> objects, bool active)
    {
        if (objects == null) return;

        foreach (var go in objects)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }
    }
}
