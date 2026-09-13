using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Visor de tomografía con los tres planos a la vez: axial, coronal y sagital,
/// cada uno con su propio deslizador.
///
/// Es como se revisa una TC en radiología: los tres cortes en paralelo, no uno
/// cada vez. Sustituye al visor de plano único anterior.
/// </summary>
public class CTMultiPlaneViewer : MonoBehaviour
{
    [SerializeField] private List<CTPlaneView> planes = new List<CTPlaneView>();

    [Tooltip("Carpetas dentro de StreamingAssets/CT, una por órgano.")]
    [SerializeField] private List<string> organs = new List<string> { "higado", "estomago", "pancreas", "vesicula" };

    [SerializeField] private TMP_Text organLabel;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    private int _index;

    private void Start()
    {
        if (previousButton != null) previousButton.onClick.AddListener(PreviousOrgan);
        if (nextButton != null) nextButton.onClick.AddListener(NextOrgan);

        ApplyOrgan();
    }

    public void NextOrgan()
    {
        if (organs.Count == 0) return;

        _index = (_index + 1) % organs.Count;
        ApplyOrgan();
    }

    public void PreviousOrgan()
    {
        if (organs.Count == 0) return;

        _index = (_index - 1 + organs.Count) % organs.Count;
        ApplyOrgan();
    }

    private void ApplyOrgan()
    {
        if (organs.Count == 0) return;

        string organ = organs[_index];
        if (organLabel != null) organLabel.text = Capitalize(organ);

        // Los tres planos cambian a la vez: siempre muestran el mismo estudio.
        foreach (var plane in planes)
        {
            if (plane != null) plane.SetOrgan(organ);
        }
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
