using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de "Sistemas": una fila por órgano, con interruptor que lo muestra u oculta,
/// y un pie que informa cuántas piezas quedan visibles.
///
/// Este panel es el ÚNICO dueño de la visibilidad de los órganos. Antes también los
/// encendía MedicalMenuActions al cambiar de vista, y los dos se pisaban: el panel
/// sincronizaba los interruptores con el estado del momento y acababa apagando lo
/// que el menú acababa de encender. Ahora el menú solo muestra u oculta este panel,
/// y el panel decide qué órganos se ven.
///
/// El conteo de piezas se calcula contando renderers reales, no con un número
/// escrito a mano, para que siga siendo correcto si alguien cambia los modelos.
/// </summary>
public class OrganSystemsPanel : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        public string label;
        public GameObject target;
        public Toggle toggle;
        public TMP_Text countText;
    }

    [SerializeField] private List<Row> rows = new List<Row>();
    [SerializeField] private TMP_Text headerBadge;
    [SerializeField] private TMP_Text footerText;
    [SerializeField] private Button hideAllButton;

    private bool _wired;

    private void OnEnable()
    {
        Wire();

        // Al abrirse el panel los órganos empiezan ocultos y es el usuario quien enciende
        // los que quiere mirar. Antes aparecían los cinco a la vez: el que entraba se
        // encontraba un amasijo de órganos delante y tenía que ir apagando para ver algo.
        // Empezando de cero, cada uno aparece porque alguien ha decidido verlo.
        foreach (var row in rows)
        {
            if (row.toggle == null) continue;
            row.toggle.SetIsOnWithoutNotify(false);
            if (row.target != null) row.target.SetActive(false);
        }

        Refresh();

        // Y otra vez en el siguiente fotograma. El menú enciende los objetos de la vista uno
        // a uno, y este panel es uno de ellos: si mañana alguien reordena esa lista y los
        // órganos pasan a encenderse después del panel, aparecerían igualmente. Repasando al
        // fotograma siguiente, manda siempre lo que digan los interruptores.
        if (isActiveAndEnabled) StartCoroutine(SyncNextFrame());
    }

    /// <summary>Pone cada órgano como diga su interruptor, pase lo que pase antes.</summary>
    private IEnumerator SyncNextFrame()
    {
        yield return null;

        foreach (var row in rows)
        {
            if (row.toggle == null || row.target == null) continue;
            row.target.SetActive(row.toggle.isOn);
        }

        Refresh();
    }

    private void OnDisable()
    {
        // Al cerrarse (por ejemplo al pasar a la vista DICOM), los órganos se van con él.
        foreach (var row in rows)
        {
            if (row.target != null) row.target.SetActive(false);
        }
    }

    private void Wire()
    {
        if (_wired) return;
        _wired = true;

        foreach (var row in rows)
        {
            if (row.toggle == null || row.target == null) continue;

            if (row.countText != null) row.countText.text = CountPieces(row.target).ToString();

            Row captured = row; // sin esto todas las filas capturarían la última
            row.toggle.onValueChanged.AddListener(isOn =>
            {
                captured.target.SetActive(isOn);
                Refresh();
            });
        }

        if (hideAllButton != null) hideAllButton.onClick.AddListener(ToggleAll);
    }

    /// <summary>
    /// Enciende todos o los apaga todos, según lo que haya ahora.
    ///
    /// Antes sólo sabía apagar, que era lo útil cuando todo empezaba encendido. Ahora que se
    /// empieza de cero hace falta lo contrario, y un botón que sólo apagase lo ya apagado no
    /// serviría de nada. El texto del botón dice en cada momento lo que va a hacer.
    /// </summary>
    public void ToggleAll()
    {
        bool anyVisible = false;
        foreach (var row in rows)
        {
            if (row.target != null && row.target.activeSelf) anyVisible = true;
        }

        foreach (var row in rows)
        {
            if (row.toggle != null) row.toggle.isOn = !anyVisible;
        }

        Refresh();
    }

    /// <summary>Se mantiene por si algún botón antiguo seguía llamándola.</summary>
    public void HideAll()
    {
        foreach (var row in rows)
        {
            if (row.toggle != null) row.toggle.isOn = false;
        }
        Refresh();
    }

    private void Refresh()
    {
        int visible = 0;
        int total = 0;

        foreach (var row in rows)
        {
            if (row.target == null) continue;

            int pieces = CountPieces(row.target);
            total += pieces;
            if (row.target.activeSelf) visible += pieces;
        }

        if (headerBadge != null) headerBadge.text = total.ToString();
        if (footerText != null) footerText.text = $"{visible} piezas visibles";

        if (hideAllButton != null)
        {
            var label = hideAllButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = visible > 0 ? "Ocultar todo" : "Mostrar todo";
        }
    }

    private static int CountPieces(GameObject go)
    {
        // Contar renderers activos e inactivos: apagar un órgano no debe cambiar
        // su número de piezas, solo si cuenta como visible.
        return go.GetComponentsInChildren<Renderer>(true).Length;
    }
}
