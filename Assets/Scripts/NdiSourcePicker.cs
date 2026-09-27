using System.Collections.Generic;
using Klak.Ndi;
using UnityEngine;

/// <summary>
/// Busca las emisiones NDI que haya en la red y engancha la pantalla a una.
///
/// Hasta ahora el nombre de la fuente estaba escrito a mano en la escena
/// ("10.36.202.200 (stream0)"). Eso funciona el día que se configuró y deja de funcionar en
/// cuanto el ordenador del laboratorio cambia de IP, se emite desde otro equipo o alguien
/// renombra la salida. Y desde dentro del visor no había forma de arreglarlo: tocaba abrir
/// Unity, cambiar el texto y volver a compilar.
///
/// Ahora se pregunta a la red qué hay emitiendo. Si aparece la fuente de siempre se usa esa;
/// si no, se usa la primera que se encuentre, y el botón "Siguiente" permite ir pasando
/// entre las que haya sin salir del visor.
///
/// Sólo busca mientras no hay imagen: en cuanto la señal entra, deja de tocar nada, para no
/// cortar una emisión que ya funciona.
/// </summary>
public class NdiSourcePicker : MonoBehaviour
{
    [SerializeField] private NdiReceiver receiver;

    [Tooltip("La de siempre. Si aparece en la red, se prefiere a cualquier otra.")]
    [SerializeField] private string preferred;

    [Tooltip("Cada cuánto se vuelve a preguntar a la red, en segundos.")]
    [SerializeField] private float searchInterval = 1.5f;

    [Tooltip("Engancharse solo a lo que se encuentre. Apagado, sólo informa.")]
    [SerializeField] private bool autoConnect = true;

    private readonly List<string> _sources = new List<string>();
    private float _timer;

    /// <summary>Cuántas emisiones se ven ahora mismo en la red.</summary>
    public int SourceCount => _sources.Count;

    /// <summary>A cuál está enganchada la pantalla.</summary>
    public string CurrentSource => receiver != null ? receiver.ndiName : string.Empty;

    private void Awake()
    {
        if (receiver == null) receiver = GetComponent<NdiReceiver>();

        // La primera vez se toma como preferida la que estuviera puesta en la escena.
        if (string.IsNullOrEmpty(preferred) && receiver != null) preferred = receiver.ndiName;
    }

    private void OnEnable()
    {
        _timer = 0f;
        Scan();
    }

    private void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;

        _timer = searchInterval;
        Scan();
    }

    private void Scan()
    {
        _sources.Clear();
        foreach (string name in NdiFinder.sourceNames) _sources.Add(name);

        if (!autoConnect || receiver == null) return;

        // Con imagen no se toca nada: cambiar el nombre reinicia la recepción.
        if (receiver.texture != null) return;
        if (_sources.Count == 0) return;

        string pick = Choose();
        if (!string.IsNullOrEmpty(pick) && receiver.ndiName != pick) receiver.ndiName = pick;
    }

    /// <summary>La de siempre si está; si no, la primera que haya.</summary>
    private string Choose()
    {
        if (!string.IsNullOrEmpty(preferred))
        {
            foreach (string name in _sources)
            {
                if (name == preferred) return name;
            }

            // Media coincidencia: el mismo equipo aunque haya cambiado el nombre de la salida.
            foreach (string name in _sources)
            {
                if (name.StartsWith(preferred) || preferred.StartsWith(name)) return name;
            }
        }

        return _sources[0];
    }

    /// <summary>Pasa a la siguiente emisión de la red. Enlazable a un botón.</summary>
    public void Next()
    {
        if (receiver == null || _sources.Count == 0) return;

        int index = _sources.IndexOf(receiver.ndiName);
        receiver.ndiName = _sources[(index + 1) % _sources.Count];
    }

    /// <summary>Lo que hay en la red, para escribirlo en pantalla.</summary>
    public string Describe()
    {
        if (_sources.Count == 0) return "no se ve ninguna emisión en la red";
        if (_sources.Count == 1) return "1 emisión encontrada";
        return _sources.Count + " emisiones encontradas";
    }
}
