using UnityEngine;

/// <summary>
/// Mantiene el círculo de apuntar del mismo tamaño a la vista, esté cerca o lejos: sobre un
/// panel a medio metro y sobre otro a tres metros ocupa lo mismo en el ojo. Si tuviera un
/// tamaño fijo en metros, de lejos sería un punto invisible y de cerca taparía el botón.
/// </summary>
public class ReticleScaler : MonoBehaviour
{
    [Tooltip("Diámetro del círculo visto a 1 metro, en metros.")]
    [SerializeField] private float sizeAtOneMetre = 0.03f;

    [SerializeField] private float minSize = 0.012f;
    [SerializeField] private float maxSize = 0.12f;

    private Transform _camera;

    private void LateUpdate()
    {
        if (_camera == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            _camera = cam.transform;
        }

        float distance = Vector3.Distance(_camera.position, transform.position);
        transform.localScale = Vector3.one * Mathf.Clamp(sizeAtOneMetre * distance, minSize, maxSize);
    }
}
