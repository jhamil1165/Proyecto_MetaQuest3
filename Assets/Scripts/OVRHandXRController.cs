using System.Collections.Generic;
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

    [Header("Rayo")]
    [Tooltip("Grados que se levanta el rayo respecto al de Meta. El de Meta sale del hombro " +
             "y pasa por la mano: con la mano relajada a la altura de la mesa apunta al suelo, " +
             "y había que estirar el brazo hacia los paneles para que apareciera el círculo.")]
    [Range(0f, 30f)]
    [SerializeField] private float pitchOffset = 15f;

    [Tooltip("Rapidez con la que el rayo sigue a la mano. Más bajo = más estable pero más lento.")]
    [SerializeField] private float smoothing = 14f;

    [Tooltip("Segundos que el rayo se queda quieto al empezar la pinza. Al juntar los dedos " +
             "la mano se mueve y el rayo se iba del botón justo en el momento de pulsar.")]
    [SerializeField] private float pinchFreeze = 0.18f;

    [Tooltip("Por debajo de esta fuerza, con la pinza aún cerrada, se entiende que se está " +
             "soltando y el rayo se queda quieto hasta soltar del todo. Al abrir los dedos " +
             "la mano también se mueve, y el clic se perdía porque el botón se suelta fuera.")]
    [Range(0.3f, 1f)]
    [SerializeField] private float releaseFreezeBelow = 0.65f;

    [Tooltip("Cuánto antes de la pinza se toma la dirección que se congela, en segundos.")]
    [SerializeField] private float pinchLookBack = 0.1f;

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

    private Quaternion _aim;
    private bool _hasAim;
    private int _aimFrame = -1;
    private float _pinchStart = -1f;
    private Quaternion _frozenAim;
    private bool _releasing;
    private readonly Queue<(float time, Quaternion rotation)> _aimHistory = new Queue<(float, Quaternion)>();

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
        Aim(pointer);

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
        bool wasPinching = _pinchLatched;
        _pinchLatched = rawPinch || PinchStrength >= threshold;
        IsPinching = _pinchLatched;

        // Al empezar la pinza se congela el rayo en la dirección de un instante antes.
        if (_pinchLatched && !wasPinching)
        {
            _pinchStart = Time.time;
            _frozenAim = AimBefore(Time.time - pinchLookBack);
        }

        // Soltando: los dedos ya se separan pero la pinza sigue cerrada. Se congela el rayo
        // en la dirección de justo antes de empezar a abrir, hasta que suelte del todo.
        bool releasing = _pinchLatched && PinchStrength < releaseFreezeBelow && Time.time - _pinchStart > pinchFreeze;
        if (releasing && !_releasing) _frozenAim = AimBefore(Time.time - pinchLookBack);
        _releasing = releasing;
        if (!_pinchLatched) _releasing = false;

        SetStates(controllerState, IsPinching, PinchStrength);
    }

    /// <summary>
    /// Orienta el rayo: el de Meta un poco levantado, suavizado contra el temblor, y quieto
    /// un instante al pinzar. La posición sigue siendo la de Meta (este objeto cuelga de su
    /// PointerPose); solo se corrige la dirección.
    /// </summary>
    private void Aim(Transform pointer)
    {
        Quaternion target = pointer.rotation * Quaternion.Euler(-pitchOffset, 0f, 0f);

        // XRI pide la pose dos veces por fotograma (Update y antes de dibujar): el filtro
        // avanza una sola vez por fotograma para no ir el doble de rápido.
        if (_aimFrame != Time.frameCount)
        {
            _aimFrame = Time.frameCount;

            if (!_hasAim) { _aim = target; _hasAim = true; }

            // Filtro adaptativo: los giros pequeños (temblor) se suavizan mucho y los
            // grandes (mover el brazo a otro panel) se siguen casi al momento.
            float angle = Quaternion.Angle(_aim, target);
            float speed = smoothing * Mathf.Lerp(0.35f, 2f, Mathf.InverseLerp(0.5f, 10f, angle));
            _aim = Quaternion.Slerp(_aim, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

            _aimHistory.Enqueue((Time.time, _aim));
            while (_aimHistory.Count > 0 && Time.time - _aimHistory.Peek().time > 0.4f) _aimHistory.Dequeue();
        }

        bool frozen = (_pinchStart >= 0f && Time.time - _pinchStart < pinchFreeze) || _releasing;
        transform.rotation = frozen ? _frozenAim : _aim;
    }

    /// <summary>Dirección que tenía el rayo en el instante dado, o la más antigua guardada.</summary>
    private Quaternion AimBefore(float time)
    {
        Quaternion result = _aim;
        foreach (var entry in _aimHistory)
        {
            if (entry.time > time) break;
            result = entry.rotation;
        }
        return result;
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
        _hasAim = false;
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
