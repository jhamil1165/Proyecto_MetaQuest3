using Klak.Ndi;
using TMPro;
using UnityEngine;

/// <summary>
/// Dice en qué estado está la emisión NDI en vez de dejar un rectángulo blanco.
///
/// NdiReceiver solo escribe la textura cuando recibe algo (NdiReceiver.cs:80 sale
/// antes si el RenderTexture es nulo), así que sin emisor el Quad se queda con el
/// color base del material: un panel blanco de 1,6 x 0,9 m que parece un fallo y que
/// además deslumbra dentro del visor.
///
/// Aquí se mira NdiReceiver.texture, que es null mientras no llega señal, y se apaga
/// la pantalla mostrando el nombre de la fuente que se está buscando.
/// </summary>
[RequireComponent(typeof(NdiReceiver))]
public class NdiScreenStatus : MonoBehaviour
{
    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private TMP_Text statusLabel;

    [Tooltip("Cada cuánto se comprueba si hay señal. No hace falta mirarlo cada frame.")]
    [SerializeField] private float checkInterval = 0.5f;

    [SerializeField] private Color offColor = new Color(0.07f, 0.08f, 0.10f, 1f);
    [SerializeField] private Color liveColor = Color.white;

    private NdiReceiver _receiver;
    private MaterialPropertyBlock _mpb;
    private float _timer;
    private bool _hasState;
    private bool _live;

    private void Awake()
    {
        _receiver = GetComponent<NdiReceiver>();
        if (screenRenderer == null) screenRenderer = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        // Forzar el primer repintado: si no, la pantalla se queda como quedase la
        // última vez hasta que pase el intervalo.
        _hasState = false;
        _timer = 0f;
        Check();
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = checkInterval;
        Check();
    }

    private void Check()
    {
        bool live = _receiver != null && _receiver.texture != null;

        // Solo repintar en los cambios: escribir el property block cada medio segundo
        // no cuesta nada, pero tampoco aporta.
        if (_hasState && live == _live) return;

        _hasState = true;
        _live = live;
        Apply(live);
    }

    private void Apply(bool live)
    {
        if (screenRenderer != null)
        {
            // Get antes de Set, igual que hace NdiReceiver: así conservamos la textura
            // que él haya puesto en el mismo bloque en vez de borrarla.
            screenRenderer.GetPropertyBlock(_mpb);

            Color tint = live ? liveColor : offColor;
            _mpb.SetColor("_BaseColor", tint);  // URP Lit / Unlit
            _mpb.SetColor("_Color", tint);      // materiales del pipeline antiguo

            screenRenderer.SetPropertyBlock(_mpb);
        }

        if (statusLabel != null)
        {
            statusLabel.gameObject.SetActive(!live);

            if (!live)
            {
                string fuente = _receiver != null && !string.IsNullOrEmpty(_receiver.ndiName)
                    ? _receiver.ndiName
                    : "(sin fuente configurada)";

                statusLabel.text = "NDI · SIN SEÑAL" + System.Environment.NewLine +
                                   "<size=55%>Buscando  " + fuente + "</size>";
            }
        }

        Debug.Log("[NDI] " + (live ? "señal recibida" : "sin señal, buscando " +
                  (_receiver != null ? _receiver.ndiName : "?")));
    }
}
