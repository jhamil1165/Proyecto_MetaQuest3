using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mantiene dos visores de TC en el mismo corte: al mover el deslizador de un plano,
/// el mismo plano del otro visor salta al corte equivalente. Así, en la vista
/// Segmentación, la tomografía normal y la del órgano pintado muestran siempre la misma
/// altura del cuerpo y se pueden comparar.
///
/// El corte se reparte en proporción (0 = primero, 1 = último) para que funcione aunque
/// las dos series no tengan el mismo número de cortes, y cada pareja puede invertirse
/// si una serie numera desde la cabeza y la otra desde los pies.
/// </summary>
public class SliceSync : MonoBehaviour
{
    [System.Serializable]
    public class Pair
    {
        public CTPlaneView a;
        public CTPlaneView b;

        [Tooltip("Invertir el orden: el primer corte de un visor es el último del otro.")]
        public bool reverse;
    }

    [SerializeField] private List<Pair> pairs = new List<Pair>();

    private void OnEnable()
    {
        foreach (var p in pairs)
        {
            if (p.a != null) p.a.SliceChanged += OnSliceChanged;
            if (p.b != null) p.b.SliceChanged += OnSliceChanged;
        }
    }

    private void OnDisable()
    {
        foreach (var p in pairs)
        {
            if (p.a != null) p.a.SliceChanged -= OnSliceChanged;
            if (p.b != null) p.b.SliceChanged -= OnSliceChanged;
        }
    }

    /// <summary>Lleva el segundo visor (b) a los cortes que muestra el primero (a).</summary>
    public void CopyAToB()
    {
        foreach (var p in pairs)
        {
            if (p.a != null && p.b != null) p.b.SetSliceWithoutNotify(Map(p.a.Slice, p.a, p.b, p.reverse));
        }
    }

    private void OnSliceChanged(CTPlaneView source, int slice)
    {
        foreach (var p in pairs)
        {
            if (source == p.a && p.b != null)
                p.b.SetSliceWithoutNotify(Map(slice, p.a, p.b, p.reverse));
            else if (source == p.b && p.a != null)
                p.a.SetSliceWithoutNotify(Map(slice, p.b, p.a, p.reverse));
        }
    }

    private static int Map(int slice, CTPlaneView from, CTPlaneView to, bool reverse)
    {
        float t = from.SliceCount > 1 ? slice / (float)(from.SliceCount - 1) : 0f;
        if (reverse) t = 1f - t;
        return Mathf.RoundToInt(t * Mathf.Max(0, to.SliceCount - 1));
    }
}
