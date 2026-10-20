using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El rayo con el que apuntan los mandos para las reglas (MeasureTool y CTMeasure2D). Las
/// manos van aparte, con HandPinch (más abajo); los mandos quedan como alternativa.
///
/// Antes las reglas leían el punto de choque de los XRRayInteractor de XRI. Con el paso al
/// Interaction SDK de Meta esos rayos están apagados, así que aquí se traza el rayo a mano:
/// sale del anclaje del mando (el mismo sitio y la misma dirección que tenía el rayo de XRI,
/// que iba montado en ese anclaje) y no depende de ningún sistema de interacción.
/// </summary>
public static class ControllerPointer
{
    private static OVRCameraRig _rig;

    /// <summary>Anclaje del mando derecho o izquierdo del rig, o null si no hay rig.</summary>
    public static Transform Anchor(bool right)
    {
        if (_rig == null) _rig = Object.FindObjectOfType<OVRCameraRig>();
        if (_rig == null) return null;
        return right ? _rig.rightControllerAnchor : _rig.leftControllerAnchor;
    }

    /// <summary>
    /// Si en este fotograma se pulsó el botón, devuelve el rayo del mando que lo pulsó.
    /// </summary>
    public static bool TryGetPressed(OVRInput.Button button, out Ray ray, out Transform origin)
    {
        ray = default;
        origin = null;

        if (OVRInput.GetDown(button, OVRInput.Controller.RTouch)) origin = Anchor(true);
        else if (OVRInput.GetDown(button, OVRInput.Controller.LTouch)) origin = Anchor(false);
        else if (OVRInput.GetDown(button))
        {
            origin = Anchor(true);
            if (origin == null) origin = Anchor(false);
        }

        if (origin == null) return false;
        ray = new Ray(origin.position, origin.forward);
        return true;
    }

    /// <summary>
    /// Si el rayo choca con algún panel interactivo (un Canvas en el mundo con
    /// GraphicRaycaster) antes de <paramref name="maxDistance"/>.
    /// </summary>
    public static bool HitsPanel(Ray ray, float maxDistance)
    {
        foreach (var canvas in Object.FindObjectsOfType<Canvas>())
        {
            if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.WorldSpace) continue;
            if (!canvas.TryGetComponent(out GraphicRaycaster _)) continue;
            if (TryHitRect((RectTransform)canvas.transform, ray, maxDistance, out _)) return true;
        }
        return false;
    }

    /// <summary>
    /// Dónde corta el rayo a un rectángulo de UI, si cae dentro de él.
    /// </summary>
    public static bool TryHitRect(RectTransform rect, Ray ray, float maxDistance, out Vector3 point)
    {
        point = default;
        var plane = new Plane(rect.forward, rect.position);
        if (!plane.Raycast(ray, out float distance)) return false;
        if (distance < 0f || distance > maxDistance) return false;

        point = ray.GetPoint(distance);
        Vector3 local = rect.InverseTransformPoint(point);
        return rect.rect.Contains(new Vector2(local.x, local.y));
    }
}

/// <summary>
/// Pellizco de índice y pulgar con hand tracking, para las reglas: en quirófano se trabaja
/// solo con las manos, sin mandos.
///
/// Detecta el instante en que se cierra el pellizco (no mientras se mantiene) y da el rayo de
/// apuntar de esa mano: OVRHand.PointerPose, el rayo que Meta ya estabiliza. Cada regla tiene
/// su propio HandPinch, porque cada una lleva la cuenta de cuándo se cerró.
/// </summary>
public sealed class HandPinch
{
    // Tras un punto, un momento sin aceptar otro: al abrir y cerrar rápido el seguimiento
    // puede dar dos pellizcos donde el usuario hizo uno.
    private const float Cooldown = 0.35f;

    private readonly Dictionary<OVRHand, bool> _wasPinching = new Dictionary<OVRHand, bool>();
    private OVRHand[] _hands;
    private float _nextAllowed;

    public bool TryGetPinched(out Ray ray, out Transform origin)
    {
        ray = default;
        origin = null;

        // Puede haber dos OVRHand por mano (la del rig y la del Interaction SDK). Se miran todas
        // y vale la primera que cierre el pellizco en este fotograma.
        if (_hands == null || _hands.Length == 0) _hands = Object.FindObjectsOfType<OVRHand>(true);

        foreach (var hand in _hands)
        {
            if (hand == null) continue;

            bool pinching = hand.isActiveAndEnabled && hand.IsTracked && hand.PointerPose != null
                            && hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            _wasPinching.TryGetValue(hand, out bool was);
            _wasPinching[hand] = pinching;

            if (origin != null || !pinching || was || Time.time < _nextAllowed) continue;
            origin = hand.PointerPose;
        }

        if (origin == null) return false;

        _nextAllowed = Time.time + Cooldown;
        ray = new Ray(origin.position, origin.forward);
        return true;
    }
}
