using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloca el plano de corte en el centro de los órganos visibles al abrirse la vista.
///
/// Hace falta porque WorkspaceRecenter mueve los órganos al arrancar, según dónde esté
/// mirando el usuario: una posición fija dejaría el plano lejos de ellos y el corte no
/// se vería. Después el usuario lo mueve con la mano.
/// </summary>
public class ClippingPlaneAnchor : MonoBehaviour
{
    [SerializeField] private List<GameObject> organs = new List<GameObject>();

    [Tooltip("Espera a que WorkspaceRecenter haya colocado los órganos.")]
    [SerializeField] private float delay = 1.3f;

    [Tooltip("Margen por encima de los órganos. El plano arranca ahí para que se vean enteros.")]
    [SerializeField] private float margin = 0.04f;

    [Tooltip("Si está encendido, el plano se centra en él y no en la media de todo. " +
             "Es el volumen 3D: es lo que casi siempre se quiere cortar.")]
    [SerializeField] private GameObject preferred;

    private void OnEnable()
    {
        StartCoroutine(PlaceWhenReady());
    }

    private IEnumerator PlaceWhenReady()
    {
        yield return new WaitForSeconds(delay);
        Place();
    }

    /// <summary>
    /// Lleva el plano al centro de todo lo que se puede cortar y esté a la vista.
    ///
    /// Antes miraba sólo la lista de órganos sueltos y dejaba el plano por encima de ellos.
    /// Desde que existe el volumen 3D eso se queda corto: se pulsaba "Centrar" y el plano se
    /// iba con los órganos, lejos del volumen, que es lo que casi siempre se quiere cortar.
    /// Ahora entra todo lo que esté en la lista, el volumen incluido, y el plano se queda en
    /// el centro, ya cortando, en vez de por encima sin cortar nada.
    /// </summary>
    public void Place()
    {
        bool found = false;
        Bounds bounds = default;

        // El plano es un cuadrado pequeño, pero el corte que aplica es un plano infinito. Si
        // el cuadrado se queda lejos de lo que se está cortando, el usuario ve el corte
        // ocurrir en un sitio y el cuadrado en otro, y parece que van descompasados. Por eso
        // se centra en una sola cosa, la principal, y no en la media de todas.
        var list = new List<GameObject>();
        if (preferred != null && preferred.activeInHierarchy) list.Add(preferred);
        else list.AddRange(organs);

        foreach (var target in list)
        {
            if (target == null || !target.activeInHierarchy) continue;

            foreach (var renderer in target.GetComponentsInChildren<Renderer>(false))
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!found) return;

        transform.position = bounds.center;

        // Plano horizontal: su eje Y es la normal que lee el shader.
        transform.rotation = Quaternion.identity;
    }
}
