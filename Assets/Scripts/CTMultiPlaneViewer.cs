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
///
/// Puede trabajar con dos fuentes. Las series DICOM reales (lista "studies") tienen
/// prioridad; si está vacía se usan las capturas de Slicer por órgano (lista "organs").
/// </summary>
public class CTMultiPlaneViewer : MonoBehaviour
{
    /// <summary>
    /// Una serie de TC convertida desde DICOM. Cada serie tiene su propio número de
    /// cortes axiales, así que los recuentos van con la serie y no con la columna.
    /// </summary>
    [System.Serializable]
    public class Study
    {
        [Tooltip("Carpeta dentro de StreamingAssets/CT.")]
        public string folder;

        [Tooltip("Nombre que aparece en la cabecera del visor.")]
        public string label;

        public int axial;
        public int coronal;
        public int sagittal;
    }

    [SerializeField] private List<CTPlaneView> planes = new List<CTPlaneView>();

    [Tooltip("Series DICOM reales. Si hay alguna, se usan en lugar de la lista de órganos.")]
    [SerializeField] private List<Study> studies = new List<Study>();

    [Tooltip("Carpetas dentro de StreamingAssets/CT, una por órgano (capturas de Slicer).")]
    [SerializeField] private List<string> organs = new List<string> { "higado", "estomago", "pancreas", "vesicula" };

    [SerializeField] private TMP_Text organLabel;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;

    private int _index;

    private bool UseStudies => studies.Count > 0;
    private int Count => UseStudies ? studies.Count : organs.Count;

    private void Start()
    {
        if (previousButton != null) previousButton.onClick.AddListener(PreviousOrgan);
        if (nextButton != null) nextButton.onClick.AddListener(NextOrgan);

        ApplyOrgan();
    }

    public void NextOrgan()
    {
        if (Count == 0) return;

        _index = (_index + 1) % Count;
        ApplyOrgan();
    }

    public void PreviousOrgan()
    {
        if (Count == 0) return;

        _index = (_index - 1 + Count) % Count;
        ApplyOrgan();
    }

    private void ApplyOrgan()
    {
        if (Count == 0) return;

        if (UseStudies)
        {
            Study study = studies[_index];
            if (organLabel != null) organLabel.text = string.IsNullOrEmpty(study.label) ? study.folder : study.label;

            foreach (var plane in planes)
            {
                if (plane != null) plane.SetOrgan(study.folder, CountFor(study, plane.Plane));
            }
            return;
        }

        string organ = organs[_index];
        if (organLabel != null) organLabel.text = Capitalize(organ);

        // Los tres planos cambian a la vez: siempre muestran el mismo estudio.
        foreach (var plane in planes)
        {
            if (plane != null) plane.SetOrgan(organ);
        }
    }

    private static int CountFor(Study study, string plane)
    {
        switch (plane)
        {
            case "axial": return study.axial;
            case "coronal": return study.coronal;
            case "sagital": return study.sagittal;
            default: return 0;
        }
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
