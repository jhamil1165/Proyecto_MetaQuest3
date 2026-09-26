using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Formas de ver el volumen 3D: tejido, esqueleto o solo los órganos.
///
/// Cada modo es una combinación de ventana (qué densidades se ven) y de opacidad de cada
/// cosa. El esqueleto no sale de un umbral de grises: la segmentación trae marcadas las
/// costillas, vértebras, caderas y fémures, así que se enciende y se apaga como un
/// elemento más.
///
/// Los valores se mandan con un MaterialPropertyBlock, así que no se modifica el material
/// del proyecto y se puede cambiar de modo sin crear materiales nuevos.
/// </summary>
public class VolumeDisplay : MonoBehaviour
{
    [Serializable]
    public class Mode
    {
        public string label = "Tejido";

        [Tooltip("Centro y ancho de ventana, en unidades Hounsfield.")]
        public float windowCenter = 40f;
        public float windowWidth = 500f;

        [Range(0f, 1f)] public float tissueOpacity = 0.10f;
        [Range(0f, 1f)] public float organOpacity = 0.85f;
        [Range(0f, 1f)] public float boneOpacity = 0f;
    }

    [SerializeField] private Renderer volume;
    [SerializeField] private Mode[] modes = Array.Empty<Mode>();
    [SerializeField] private int current;

    [Header("Botones del menú")]
    [SerializeField] private Button[] buttons = Array.Empty<Button>();
    [SerializeField] private Color selectedColor = new Color(0.09f, 0.42f, 0.88f, 1f);
    [SerializeField] private Color idleColor = new Color(0.955f, 0.960f, 0.968f, 1f);
    [SerializeField] private Color selectedText = Color.white;
    [SerializeField] private Color idleText = new Color(0.13f, 0.15f, 0.18f, 1f);

    private static readonly int WindowCenterId = Shader.PropertyToID("_WindowCenter");
    private static readonly int WindowWidthId = Shader.PropertyToID("_WindowWidth");
    private static readonly int TissueId = Shader.PropertyToID("_TissueOpacity");
    private static readonly int OrganId = Shader.PropertyToID("_OrganOpacity");
    private static readonly int BoneId = Shader.PropertyToID("_BoneOpacity");

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        if (volume == null) volume = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        Show(current);
    }

    /// <summary>Cambia de modo. Enlazable a los botones del menú.</summary>
    public void Show(int index)
    {
        if (modes.Length == 0 || volume == null) return;

        current = Mathf.Clamp(index, 0, modes.Length - 1);
        Mode mode = modes[current];

        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        // Get antes de Set: el plano de corte escribe en el mismo bloque.
        volume.GetPropertyBlock(_mpb);
        _mpb.SetFloat(WindowCenterId, mode.windowCenter);
        _mpb.SetFloat(WindowWidthId, mode.windowWidth);
        _mpb.SetFloat(TissueId, mode.tissueOpacity);
        _mpb.SetFloat(OrganId, mode.organOpacity);
        _mpb.SetFloat(BoneId, mode.boneOpacity);
        volume.SetPropertyBlock(_mpb);

        if (!volume.enabled) volume.enabled = true;

        Highlight();
    }

    /// <summary>Muestra u oculta el volumen entero.</summary>
    public void ToggleVisible()
    {
        if (volume == null) return;

        volume.enabled = !volume.enabled;
        Highlight();
    }

    private void Highlight()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;

            bool on = volume != null && volume.enabled && i == current;
            if (buttons[i].targetGraphic != null) buttons[i].targetGraphic.color = on ? selectedColor : idleColor;

            var text = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (text != null) text.color = on ? selectedText : idleText;
        }
    }
}
