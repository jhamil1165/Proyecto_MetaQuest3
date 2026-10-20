using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El rayo con el que apuntan los mandos para las reglas (MeasureTool y CTMeasure2D).
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
