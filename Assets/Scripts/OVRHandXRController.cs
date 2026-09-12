using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Puente entre el hand tracking de Meta y el sistema de interacción de Unity.
///
/// Son dos mundos distintos: OVRHand sabe dónde están tus dedos, pero XRI (que maneja
/// el agarre de los órganos y la interfaz) solo entiende de "controladores". Este
/// componente traduce uno al otro:
///
///   pose de la mano  ->  posición y rotación del controlador XRI
///   gesto de pinza   ->  botón de selección (agarrar)
///
/// Expone su estado en propiedades públicas para que un HUD pueda mostrarlo: sin eso,
/// depurar hand tracking dentro del headset es adivinar a ciegas.
/// </summary>
[AddComponentMenu("MedicalViewer/OVR Hand XR Controller")]
public class OVRHandXRController : XRBaseController
{
    [Header("Mano de Meta")]
    [SerializeField] private OVRHand hand;

    [Tooltip("Fuerza de pinza a partir de la cual se considera agarre. Más bajo = más sensible.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float pinchThreshold = 0.5f;

    [Tooltip("Una vez agarrado, hace falta bajar de este valor para soltar. Evita que " +
             "el objeto se suelte solo por micro-variaciones del tracking.")]
    [Range(0.05f, 1f)]
    [SerializeField] private float releaseThreshold = 0.3f;

    [Tooltip("Interactores que se apagan cuando la mano no está rastreada.")]
    [SerializeField] private XRBaseInteractor[] interactorsToGate;

    // Estado observable, para el HUD de diagnóstico.
    public bool HandTracked { get; private set; }
    public float PinchStrength { get; private set; }
    public bool IsPinching { get; private set; }
    public bool SystemGesture { get; private set; }
    public float HandConfidence { get; private set; }

    /// <summary>A que mano de Meta esta enlazado, para verificar el cableado.</summary>
    public string BoundHandName => hand != null ? hand.name : "(sin enlazar)";

    private bool _wasTracked;
    private bool _pinchLatched;
    private Transform _attachedTo;

    protected override void Awake()
    {
        base.Awake();

        // Si esto quedara en false, UpdateInput no se aplicaría y la pinza nunca
        // llegaría a XRI por mucho que la detectemos.
        enableInputActions = true;

        // Si XRI escribiera la pose, sobrescribiria el parentado al PointerPose.
        enableInputTracking = false;
    }

    protected override void UpdateTrackingInput(XRControllerState controllerState)
    {
        base.UpdateTrackingInput(controllerState);
        if (controllerState == null) return;

        HandTracked = hand != null && hand.IsTracked;
        HandConfidence = HandTracked && hand.HandConfidence == OVRHand.TrackingConfidence.High ? 1f : 0f;

        GateInteractors(HandTracked);

        if (!HandTracked)
        {
            controllerState.isTracked = false;
            controllerState.inputTrackingState = InputTrackingState.None;
            return;
        }

        // PointerPose es el rayo que Meta ya estabiliza y orienta donde el usuario
        // espera. Calcularlo a mano desde los huesos daría un rayo mucho más tembloroso.
        Transform pointer = hand.PointerPose;
        if (pointer == null) return;

        AttachToPointer(pointer);

        // La pose NO se copia. Copiarla obligaba a convertir entre espacios y cada
        // conversion era una oportunidad de que el rayo saliera de otro sitio. Ahora
        // este objeto cuelga del PointerPose, asi que su transform ES el de la mano
        // por construccion. Se marca como rastreado para que XRI lo de por valido.
        controllerState.isTracked = true;
        controllerState.inputTrackingState = InputTrackingState.Position | InputTrackingState.Rotation;
    }

    protected override void UpdateInput(XRControllerState controllerState)
    {
        base.UpdateInput(controllerState);
        if (controllerState == null) return;

        if (hand == null || !hand.IsTracked)
        {
            SetStates(controllerState, false, 0f);
            _pinchLatched = false;
            return;
        }

        // Se MIDE siempre, aunque luego se decida ignorarlo: salir antes de leer
        // dejaba el HUD mostrando 0.00 y ocultaba el dato justo cuando hacía falta.
        SystemGesture = hand.IsSystemGestureInProgress;
        PinchStrength = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
        bool rawPinch = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        // Antes se anulaba la pinza en cuanto IsSystemGestureInProgress era true, pero
        // eso se activa con solo poner la palma hacia la cara: bloqueaba el agarre en
        // posturas normales. Meta ya deja de reportar pinza durante un gesto real de
        // sistema, asi que basta con no forzar nada aqui.

        // Histéresis: cuesta más soltar que agarrar. Con un único umbral, el ruido del
        // tracking hacía que el objeto se soltara solo a media manipulación.
        float threshold = _pinchLatched ? releaseThreshold : pinchThreshold;
        _pinchLatched = rawPinch || PinchStrength >= threshold;
        IsPinching = _pinchLatched;

        SetStates(controllerState, IsPinching, PinchStrength);
    }

    private static void SetStates(XRControllerState state, bool active, float value)
    {
        state.selectInteractionState.SetFrameState(active, value);
        state.activateInteractionState.SetFrameState(active, value);
        state.uiPressInteractionState.SetFrameState(active, value);
    }

    /// <summary>
    /// Cuelga este objeto del PointerPose de Meta la primera vez que existe. Es un
    /// Transform creado en ejecucion, asi que no puede enlazarse desde el editor.
    /// </summary>
    private void AttachToPointer(Transform pointer)
    {
        if (_attachedTo == pointer) return;

        _attachedTo = pointer;
        transform.SetParent(pointer, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    private void GateInteractors(bool tracked)
    {
        if (tracked == _wasTracked) return;
        _wasTracked = tracked;

        if (interactorsToGate == null) return;

        foreach (var interactor in interactorsToGate)
        {
            if (interactor != null) interactor.enabled = tracked;
        }
    }
}
