using UnityEngine;

/// <summary>
/// Los paneles nunca quedan al alcance de la mano.
///
/// Con el passthrough la mano real se ve, pero el visor no sabe que está delante de un panel
/// virtual: el panel se dibuja encima de ella y parece que la mano lo atraviesa. Pasaba al
/// acercarse a la mesa para pinzar, y además estorbaba al apuntar. Los paneles se manejan con
/// el rayo, a distancia, así que no hace falta poder tocarlos.
///
/// Cada panel tiene su sitio (el que le da el menú). Si la cabeza se acerca a menos de la
/// distancia mínima, el panel retrocede en la misma dirección hasta esa distancia; si uno se
/// aleja, vuelve a su sitio. Se mira solo en horizontal: lo que importa es el alcance del
/// brazo, no la altura del panel.
/// </summary>
public class PanelDistanceKeeper : MonoBehaviour
{
    [SerializeField] private Transform[] panels = new Transform[0];

    [Tooltip("Distancia mínima, en horizontal, entre la cabeza y un panel. Un brazo alcanza unos 65 cm.")]
    [SerializeField] private float minDistance = 0.8f;

    [Tooltip("Rapidez con la que el panel retrocede o vuelve, en metros por segundo.")]
    [SerializeField] private float speed = 2.5f;

    private Vector3[] _home;
    private Transform _head;

    private void Awake()
    {
        _home = new Vector3[panels.Length];
        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null) _home[i] = panels[i].localPosition;
        }
    }

    private void LateUpdate()
    {
        if (_head == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _head = cam.transform;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            Transform panel = panels[i];
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;

            // Su sitio en el mundo, siguiendo al menú si se recoloca.
            Vector3 home = panel.parent != null ? panel.parent.TransformPoint(_home[i]) : _home[i];

            Vector3 away = home - _head.position;
            away.y = 0f;
            float distance = away.magnitude;

            Vector3 target = home;
            if (distance < minDistance)
            {
                Vector3 direction = distance > 1e-3f ? away / distance : Flat(_head.forward);
                Vector3 pushed = _head.position + direction * minDistance;
                target = new Vector3(pushed.x, home.y, pushed.z);
            }

            panel.position = Vector3.MoveTowards(panel.position, target, speed * Time.deltaTime);
        }
    }

    private static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward;
    }
}
