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
    [SerializeField] private float switchDelay = 0.4f;

    private bool _handsActive;
    private bool _initialised;
    private float _lastHandSeen;

    private void Update()
    {
        bool handsTracked = (leftHand != null && leftHand.IsTracked) ||
                            (rightHand != null && rightHand.IsTracked);

        if (handsTracked) _lastHandSeen = Time.time;

        // El margen evita que un parpadeo del tracking haga saltar el modo de ida y vuelta.
        bool useHands = handsTracked || (Time.time - _lastHandSeen) < switchDelay;

        if (_initialised && useHands == _handsActive) return;

        _initialised = true;
        _handsActive = useHands;
        Apply(useHands);
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
