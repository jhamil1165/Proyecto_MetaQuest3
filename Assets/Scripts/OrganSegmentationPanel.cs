using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Vista Segmentación: un botón por órgano (hígado, estómago, páncreas, vesícula) y uno
/// "Todos". Al elegir un órgano se ve a la vez lo que pidió el profesor: su modelo 3D,
/// la tomografía normal y la tomografía con ese órgano pintado, en el mismo corte.
///
/// Mientras la vista está abierta este panel es el dueño de la visibilidad de los
/// órganos: al abrirse muestra el elegido y al cerrarse los oculta, igual que hacía el
/// panel "Sistemas" en Modelo 3D. Así los dos nunca se pisan.
///
/// "Todos" muestra los cuatro modelos. La tomografía pintada con todos los órganos a la
/// vez necesita la segmentación exportada de 3D Slicer, que todavía no está; mientras
/// tanto se muestra un aviso en su lugar.
/// </summary>
public class OrganSegmentationPanel : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public string label;

        [Tooltip("Carpeta de las capturas de Slicer en StreamingAssets/CT.")]
        public string folder;

        public GameObject model;
        public Button button;
    }

    [SerializeField] private List<Entry> organs = new List<Entry>();
    [SerializeField] private Button allButton;

    [Header("Visores")]
    [SerializeField] private CTMultiPlaneViewer paintedViewer;
    [SerializeField] private TMP_Text paintedLabel;
    [SerializeField] private GameObject paintedPlanes;
    [SerializeField] private GameObject allNote;
    [SerializeField] private SliceSync sync;

    [Header("Estilo de los botones")]
    [SerializeField] private Color selectedColor = new Color(0.09f, 0.42f, 0.88f, 1f);
    [SerializeField] private Color idleColor = new Color(0.955f, 0.960f, 0.968f, 1f);
    [SerializeField] private Color selectedText = Color.white;
    [SerializeField] private Color idleText = new Color(0.13f, 0.15f, 0.18f, 1f);

    private const int All = -1;

    private int _selected;
    private bool _wired;

    private void OnEnable()
    {
        Wire();
        ApplyModels();
        ApplyButtons();
        StartCoroutine(ApplyImagesNextFrame());
    }

    private void OnDisable()
    {
        foreach (var e in organs)
        {
            if (e.model != null) e.model.SetActive(false);
        }
    }

    public void Select(int index)
    {
        _selected = index;
        ApplyModels();
        ApplyButtons();
        ApplyImages();
    }

    private void Wire()
    {
        if (_wired) return;
        _wired = true;

        for (int i = 0; i < organs.Count; i++)
        {
            int captured = i; // sin esto todos los botones elegirían el último órgano
            if (organs[i].button != null) organs[i].button.onClick.AddListener(() => Select(captured));
        }

        if (allButton != null) allButton.onClick.AddListener(() => Select(All));
    }

    private IEnumerator ApplyImagesNextFrame()
    {
        // Un frame de espera: el visor pintado hace su propio Start y, si no, cargaría el
        // primer órgano de su lista encima de la selección.
        yield return null;
        ApplyImages();
    }

    private void ApplyModels()
    {
        for (int i = 0; i < organs.Count; i++)
        {
            if (organs[i].model != null) organs[i].model.SetActive(_selected == All || _selected == i);
        }
    }

    private void ApplyButtons()
    {
        for (int i = 0; i < organs.Count; i++) Style(organs[i].button, _selected == i);
        Style(allButton, _selected == All);
    }

    private void ApplyImages()
    {
        bool single = _selected >= 0 && _selected < organs.Count;

        if (paintedPlanes != null) paintedPlanes.SetActive(single);
        if (allNote != null) allNote.SetActive(!single);

        if (single)
        {
            if (paintedViewer != null) paintedViewer.ShowOrgan(organs[_selected].folder);

            // Tras cambiar de órgano, la tomografía pintada vuelve al corte que se está
            // viendo en la normal, no al central.
            if (sync != null) sync.CopyAToB();
        }

        if (paintedLabel != null) paintedLabel.text = single ? organs[_selected].label : "Todos los órganos";
    }

    private void Style(Button button, bool selected)
    {
        if (button == null) return;

        if (button.targetGraphic != null) button.targetGraphic.color = selected ? selectedColor : idleColor;

        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null) text.color = selected ? selectedText : idleText;
    }
}
