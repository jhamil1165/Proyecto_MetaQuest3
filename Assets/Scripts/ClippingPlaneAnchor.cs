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

    private void OnEnable()
    {
        StartCoroutine(PlaceWhenReady());
    }

    private IEnumerator PlaceWhenReady()
    {
        yield return new WaitForSeconds(delay);
        Place();
    }

    /// <summary>Centra el plano en los órganos visibles. Se puede enlazar a un botón.</summary>
    public void Place()
    {
        bool found = false;
        Bounds bounds = default;

        foreach (var organ in organs)
        {
            if (organ == null || !organ.activeInHierarchy) continue;

            foreach (var renderer in organ.GetComponentsInChildren<Renderer>(false))
            {
                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!found) return;

        // Por encima de los órganos: así se ven enteros al abrir la vista y es el usuario
        // quien baja el plano cuando quiere cortar.
        transform.position = new Vector3(bounds.center.x, bounds.max.y + margin, bounds.center.z);

        // Plano horizontal: su eje Y es la normal que lee el shader.
        transform.rotation = Quaternion.identity;
    }
}
