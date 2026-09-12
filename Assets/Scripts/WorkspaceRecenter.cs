using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloca el menú y los órganos delante del usuario usando su pose REAL, en tiempo
/// de ejecución.
///
/// Por qué hace falta: las posiciones calculadas en el editor son fijas en el mundo,
/// pero al arrancar en el Quest la orientación física del usuario define hacia dónde
/// mira, y su altura real define dónde están sus ojos. Nada de eso se conoce en el
/// editor, así que el contenido aparecía desviado y por debajo de la vista.
///
/// Se usa solo el giro horizontal (yaw) de la cabeza: si se copiara la rotación
/// completa, al mirar hacia arriba o abajo el espacio de trabajo se inclinaría con
/// la cabeza en vez de quedarse nivelado.
/// </summary>
public class WorkspaceRecenter : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Transform menuRoot;
    [SerializeField] private List<Transform> organs = new List<Transform>();

    [Header("Menú")]
    [Tooltip("Altura del menú respecto a los ojos. Negativo = algo por debajo. " +
             "La distancia al usuario la define el arco dentro del propio menú.")]
    [SerializeField] private float menuHeightOffset = -0.05f;

    [Header("Órganos")]
    [SerializeField] private float organDistance = 1.15f;
    [SerializeField] private float organHeightOffset = -0.35f;
    [SerializeField] private float organArcSpread = 60f;

    [Header("Arranque")]
    [Tooltip("Espera a que el tracking del headset se asiente antes de colocar nada.")]
    [SerializeField] private float settleDelay = 1.0f;

    private IEnumerator Start()
    {
        // Al arrancar, el head pose todavía puede ser (0,0,0) durante varios frames.
        // Colocar el espacio con ese dato daría exactamente el error que se veía.
        yield return new WaitForSeconds(settleDelay);
        Recenter();
    }

    /// <summary>Recoloca todo delante del usuario. Enlazable a un botón del mando.</summary>
    public void Recenter()
    {
        if (headAnchor == null)
        {
            Debug.LogWarning("[MedicalViewer] WorkspaceRecenter: falta headAnchor.");
            return;
        }

        Vector3 eye = headAnchor.position;

        Vector3 forward = headAnchor.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward; // mirando recto arriba/abajo
        forward.Normalize();

        Quaternion yaw = Quaternion.LookRotation(forward, Vector3.up);

        if (menuRoot != null)
        {
            // La raiz se coloca EN los ojos, no delante: los canvas ya cuelgan de ella
            // a su distancia, en coordenadas locales. Sumar aqui la distancia otra vez
            // alejaria el menu al doble.
            menuRoot.position = eye + Vector3.up * menuHeightOffset;
            menuRoot.rotation = yaw;
        }

        PlaceOrgans(eye, yaw);

        Debug.Log($"[MedicalViewer] Espacio recentrado. Ojos a {eye.y:F2} m, mirando {forward}");
    }

    private void PlaceOrgans(Vector3 eye, Quaternion yaw)
    {
        int count = organs.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            Transform organ = organs[i];
            if (organ == null) continue;

            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float angle = Mathf.Lerp(-organArcSpread * 0.5f, organArcSpread * 0.5f, t);

            Vector3 dir = yaw * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 target = eye + dir * organDistance + Vector3.up * organHeightOffset;

            // Se mueve por el centro de los bounds: los modelos segmentados tienen el
            // pivote en el origen del volumen de la TC, lejos de la malla visible.
            if (TryGetBoundsCentre(organ, out Vector3 centre))
            {
                organ.position = target - (centre - organ.position);
            }
            else
            {
                organ.position = target;
            }
        }
    }

    private static bool TryGetBoundsCentre(Transform t, out Vector3 centre)
    {
        centre = default;

        var renderers = t.GetComponentsInChildren<Renderer>(true);
        bool found = false;
        Bounds bounds = default;

        foreach (var r in renderers)
        {
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!found) return false;

        centre = bounds.center;
        return true;
    }
}
