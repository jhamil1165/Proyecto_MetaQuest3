using System;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Dice si un objeto está apuntado o agarrado con el Interaction SDK de Meta.
///
/// Sustituye a lo que antes daban los XRBaseInteractable de XRI (isHovered, isSelected y
/// sus eventos). El agarre de Meta reparte el objeto en varias piezas: el Grabbable en el
/// propio objeto y los interactables (mano, pinza, distancia, rayo) en el hijo
/// ISDK_HandGrabInteraction. Aquí se escuchan todas y se lleva la cuenta por puntero
/// (PointerEvent.Identifier), así que un mismo dedo que llega por dos piezas cuenta una vez.
/// </summary>
public sealed class PointableTracker
{
    private readonly List<IPointable> _sources = new List<IPointable>();
    private readonly HashSet<int> _hovering = new HashSet<int>();
    private readonly HashSet<int> _selecting = new HashSet<int>();
    private bool _attached;

    /// <summary>Se dispara cada vez que cambia el apuntado o el agarre.</summary>
    public event Action Changed;

    public bool IsHovered => _hovering.Count > 0;
    public bool IsSelected => _selecting.Count > 0;

    /// <summary>Si el objeto tiene alguna pieza de Meta que escuchar.</summary>
    public bool HasSources => _sources.Count > 0;

    public PointableTracker(GameObject root)
    {
        // Incluye los inactivos: HoldStill apaga el hijo de agarre y luego lo vuelve a encender.
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour is IPointable pointable) _sources.Add(pointable);
        }
    }

    public void Attach()
    {
        if (_attached) return;
        _attached = true;
        foreach (var source in _sources) source.WhenPointerEventRaised += Handle;
    }

    public void Detach()
    {
        if (!_attached) return;
        _attached = false;
        foreach (var source in _sources) source.WhenPointerEventRaised -= Handle;

        bool had = IsHovered || IsSelected;
        _hovering.Clear();
        _selecting.Clear();
        if (had) Changed?.Invoke();
    }

    private void Handle(PointerEvent evt)
    {
        switch (evt.Type)
        {
            case PointerEventType.Hover:
                if (!_hovering.Add(evt.Identifier)) return;
                break;

            case PointerEventType.Unhover:
                if (!_hovering.Remove(evt.Identifier)) return;
                break;

            case PointerEventType.Select:
                if (!_selecting.Add(evt.Identifier)) return;
                break;

            case PointerEventType.Unselect:
                if (!_selecting.Remove(evt.Identifier)) return;
                break;

            case PointerEventType.Cancel:
                // Cancelado (por ejemplo, el interactable se apagó): ese puntero ya no está.
                bool removed = _selecting.Remove(evt.Identifier) | _hovering.Remove(evt.Identifier);
                if (!removed) return;
                break;

            default:
                return;   // Move: no cambia el estado
        }

        Changed?.Invoke();
    }
}
