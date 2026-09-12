using System;
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

        // Al abrirse el panel, los órganos vuelven a verse. Es lo que espera quien
        // acaba de pulsar "Modelo 3D".
        foreach (var row in rows)
        {
            if (row.toggle == null) continue;
            row.toggle.SetIsOnWithoutNotify(true);
            if (row.target != null) row.target.SetActive(true);
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

        if (hideAllButton != null) hideAllButton.onClick.AddListener(HideAll);
    }

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
    }

    private static int CountPieces(GameObject go)
    {
        // Contar renderers activos e inactivos: apagar un órgano no debe cambiar
        // su número de piezas, solo si cuenta como visible.
        return go.GetComponentsInChildren<Renderer>(true).Length;
    }
}
