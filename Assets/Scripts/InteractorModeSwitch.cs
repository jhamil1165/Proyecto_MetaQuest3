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

    [Tooltip("Margen tras perder las manos antes de volver a los mandos. Evita " +
             "parpadeos cuando el tracking se pierde un instante.")]
    [SerializeField] private float switchDelay = 1.2f;

    private bool _handsActive;
    private bool _initialised;
    private float _lastHandSeen;

    private void Update()
    {
        bool handsTracked = HandsInUse();

        if (handsTracked) _lastHandSeen = Time.time;

        // El margen evita que un parpadeo del tracking haga saltar el modo de ida y vuelta.
        bool useHands = handsTracked || (Time.time - _lastHandSeen) < switchDelay;

        if (_initialised && useHands == _handsActive) return;

        _initialised = true;
        _handsActive = useHands;
        Apply(useHands);
    }

    /// <summary>
    /// Si el usuario está usando las manos o los mandos.
    ///
    /// Antes bastaba con que una mano estuviera rastreada, y eso da muchos falsos positivos:
    /// el visor sigue viendo las manos mientras sujetan los mandos, así que el modo saltaba
    /// de uno a otro constantemente y ni las manos ni los mandos acababan de funcionar.
    ///
    /// Ahora se pregunta primero al propio sistema con qué se está jugando, que es quien
    /// mejor lo sabe y ya trae su propio margen. Sólo cuando no lo tiene claro se mira el
    /// rastreo de las manos, y entonces se exige confianza alta: con confianza baja la mano
    /// da tumbos y es peor que no tenerla.
    /// </summary>
    private bool HandsInUse()
    {
        OVRInput.Controller active = OVRInput.GetActiveController();

        if (active == OVRInput.Controller.Hands) return true;
        if (active != OVRInput.Controller.None) return false;   // hay mandos despiertos

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
