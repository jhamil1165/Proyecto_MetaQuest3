using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Los tres cortes del TAC cruzándose dentro del cuerpo, como en un neuronavegador.
///
/// Es la vista que usan Brainlab y compañía: el modelo 3D con un corte axial, uno coronal y
/// uno sagital atravesándolo, los tres pasando por el mismo punto. Mover ese punto mueve los
/// tres a la vez, así que se recorre la anatomía sin perder de vista dónde se está.
///
/// La pieza difícil ya estaba hecha: el shader MedicalViewer/VolumeSlice sabe dibujar la
/// tomografía sobre un plano cualquiera, leyendo el volumen en el sitio que le toca a cada
/// píxel. Aquí sólo se colocan tres de esos planos, uno por eje, y se les dice dónde cruzarse.
///
/// El punto de cruce se puede agarrar con la mano y se queda siempre dentro del cuerpo: fuera
/// no habría nada que enseñar y los tres planos saldrían en blanco.
/// </summary>
public class TriPlaneView : MonoBehaviour
{
    [SerializeField] private Renderer volume;

    [Tooltip("El punto donde se cruzan los tres cortes. Se puede agarrar.")]
    [SerializeField] private Transform focus;

    [Header("Los tres cortes")]
    [Tooltip("De arriba abajo: el corte de toda la vida.")]
    [SerializeField] private Renderer axial;

    [Tooltip("De delante atrás.")]
    [SerializeField] private Renderer coronal;

    [Tooltip("De lado a lado.")]
    [SerializeField] private Renderer sagittal;

    [Header("Botón del menú")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Color onColor = new Color32(0x4C, 0x8D, 0xFF, 255);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.45f);
    [SerializeField] private Color onText = Color.white;
    [SerializeField] private Color offText = new Color32(0xF0, 0xF2, 0xF5, 255);

    [SerializeField] private bool visible;

    private static readonly int WorldToObjectId = Shader.PropertyToID("_VolumeWorldToObject");

    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        Apply();
        if (visible) Center();
    }

    /// <summary>Enseña u oculta los tres cortes. Enlazable a un botón.</summary>
    public void Toggle()
    {
        visible = !visible;
        Apply();
        if (visible) Center();
    }

    /// <summary>Devuelve el punto de cruce al centro del cuerpo. Enlazable a un botón.</summary>
    public void Center()
    {
        if (focus == null || volume == null) return;

        focus.position = volume.bounds.center;
    }

    private void LateUpdate()
    {
        if (!visible || volume == null || focus == null) return;

        // Fuera del cuerpo no hay nada que enseñar, así que el punto no sale de él.
        Bounds bounds = volume.bounds;
        focus.position = new Vector3(
            Mathf.Clamp(focus.position.x, bounds.min.x, bounds.max.x),
            Mathf.Clamp(focus.position.y, bounds.min.y, bounds.max.y),
            Mathf.Clamp(focus.position.z, bounds.min.z, bounds.max.z));

        // Un cuadrado mira hacia su eje Z, así que a cada corte se le gira su Z hacia el eje
        // del volumen que le toca. Se usan los ejes del volumen y no los del mundo: si alguien
        // gira el cuerpo, los cortes giran con él y siguen siendo axial, coronal y sagital.
        Quaternion body = volume.transform.rotation;
        Place(axial, focus.position, body * Quaternion.LookRotation(Vector3.up, Vector3.forward), bounds);
        Place(coronal, focus.position, body, bounds);
        Place(sagittal, focus.position, body * Quaternion.LookRotation(Vector3.right, Vector3.up), bounds);
    }

    private void Place(Renderer plane, Vector3 position, Quaternion rotation, Bounds bounds)
    {
        if (plane == null) return;

        // Del tamaño de la diagonal del cuerpo: así cubre el corte entero mire hacia donde
        // mire. Lo que sobra lo descarta el shader por estar fuera del volumen.
        float size = bounds.size.magnitude * 1.02f;

        plane.transform.SetPositionAndRotation(position, rotation);
        plane.transform.localScale = new Vector3(size, size, 1f);

        plane.GetPropertyBlock(_mpb);
        _mpb.SetMatrix(WorldToObjectId, volume.transform.worldToLocalMatrix);
        plane.SetPropertyBlock(_mpb);
    }

    private void Apply()
    {
        foreach (var plane in new[] { axial, coronal, sagittal })
        {
            if (plane != null) plane.enabled = visible;
        }

        if (focus != null) focus.gameObject.SetActive(visible);

        if (toggleButton != null)
        {
            if (toggleButton.targetGraphic != null)
                toggleButton.targetGraphic.color = visible ? onColor : offColor;

            var label = toggleButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = visible ? "Ocultar" : "Mostrar";
                label.color = visible ? onText : offText;
            }
        }
    }
}
