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

    private static readonly OVRInput.Controller[] Sides =
        { OVRInput.Controller.LTouch, OVRInput.Controller.RTouch };

    // Cuanto tiene que durar el movimiento de un mando para contar como uso.
    private const float SustainedMotion = 0.15f;
    // Margen tras recuperar el rastreo de un mando antes de fiarse de su velocidad.
    private const float TrackingSettle = 0.5f;

    private readonly float[] _movingSince = { -1f, -1f };
    private readonly float[] _trackedSince = { -1f, -1f };

    private bool _handsActive;
    private bool _initialised;
    private float _lastControllerUse;
    private float _lastSwitch;

    private void Update()
    {
        // Se mira si alguien esta USANDO los mandos, no si estan encendidos. Dejandolos en la
        // mesa siguen conectados y con cualquier temblor el sistema los da por activos: por
        // eso el modo saltaba a manos y volvia solo a los mandos con los mandos en la mesa.
        bool controllersInUse = ControllersInUse();
        if (controllersInUse) _lastControllerUse = Time.time;

        bool hands;
        if (!_initialised)
        {
            hands = HandsInUse() && !controllersInUse;
        }
        else if (_handsActive)
        {
            // En modo manos SOLO se vuelve a los mandos si alguien los usa de verdad. Perder el
            // rastreo de la mano no cuenta: al agarrar y girar un organo las manos se tapan
            // entre si o ponen la palma hacia la cara, la confianza cae un instante y antes eso
            // bastaba para devolver el control a unos mandos que seguian en la mesa.
            // Tampoco se cambia a media manipulacion: soltaria el objeto de golpe.
            hands = !controllersInUse || HandsHoldingSomething();
        }
        else
        {
            hands = HandsInUse() && (Time.time - _lastControllerUse) > switchDelay;
        }

        if (_initialised && hands == _handsActive) return;

        // Garantia contra el parpadeo: entre dos cambios tiene que pasar al menos el margen.
        // Sin esto, una señal que dudase podria encender y apagar los rayos cada fotograma, y
        // eso no solo se ve mal: XRI se pasaria el rato dando de alta y de baja interactores,
        // el rendimiento se hunde y la interfaz deja de responder. Que es justo lo que pasaba.
        if (_initialised && Time.time - _lastSwitch < switchDelay) return;

        _initialised = true;
        _handsActive = hands;
        _lastSwitch = Time.time;
        Apply(hands);
    }

    /// <summary>
    /// Si hay una mano de verdad puesta en un mando.
    ///
    /// La pista que vale es el movimiento. Un mando en la mano nunca está del todo quieto: el
    /// pulso lo mueve siempre un poco. Uno en la mesa está inmóvil y ahí se queda.
    ///
    /// Antes se miraba el sensor de contacto, y eso falla justo en el caso que importa: un
    /// mando tumbado apoya sus sensores capacitivos contra la mesa y se da por tocado, así que
    /// el visor lo reactivaba solo y volvían a aparecer los rayos desde la mesa.
    ///
    /// También vale una pulsación de verdad, porque alguien puede coger el mando y apretar sin
    /// llegar a moverlo; pero no basta con rozarlo.
    /// </summary>
    private bool ControllersInUse()
    {
        for (int i = 0; i < Sides.Length; i++)
        {
            var side = Sides[i];

            // Un mando que el visor no rastrea no puede estar en uso. Sus velocidades en ese
            // estado no valen nada, y justo al recuperar el rastreo (al mirar hacia la mesa o
            // darle un golpe) la pose salta y parece que se ha movido de golpe.
            if (!Tracked(side))
            {
                _movingSince[i] = -1f;
                _trackedSince[i] = -1f;
                continue;
            }
            if (_trackedSince[i] < 0f) _trackedSince[i] = Time.time;

            // El movimiento tiene que durar un poco y no contar recien recuperado el rastreo:
            // un salto aislado de un fotograma no es una mano cogiendo el mando.
            if (Moving(side) && Time.time - _trackedSince[i] > TrackingSettle)
            {
                if (_movingSince[i] < 0f) _movingSince[i] = Time.time;
                if (Time.time - _movingSince[i] > SustainedMotion) return true;
            }
            else
            {
                _movingSince[i] = -1f;
            }

            // Pulsaciones deliberadas, no el simple contacto.
            if (OVRInput.Get(OVRInput.Button.Any, side)) return true;
            if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, side) > 0.3f) return true;
            if (OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, side) > 0.3f) return true;
            if (OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, side).magnitude > 0.3f) return true;
        }

        return false;
    }

    /// <summary>
    /// Si el mando se está moviendo más de lo que se mueve algo apoyado en una mesa.
    ///
    /// Los umbrales son pequeños a propósito: se trata de distinguir "quieto en un mueble" de
    /// "en una mano", y una mano quieta tiembla bastante más que una mesa.
    /// </summary>
    private static bool Tracked(OVRInput.Controller side)
    {
        return OVRInput.IsControllerConnected(side) &&
               OVRInput.GetControllerPositionTracked(side);
    }

    private static bool Moving(OVRInput.Controller side)
    {
        if (OVRInput.GetLocalControllerVelocity(side).magnitude > 0.04f) return true;
        return OVRInput.GetLocalControllerAngularVelocity(side).magnitude > 0.25f;
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

    /// <summary>Si alguna mano tiene un objeto agarrado ahora mismo.</summary>
    private bool HandsHoldingSomething()
    {
        foreach (var go in handInteractors)
        {
            if (go == null || !go.activeInHierarchy) continue;
            foreach (var interactor in go.GetComponentsInChildren<IXRSelectInteractor>())
            {
                if (interactor.hasSelection) return true;
            }
        }
        return false;
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
