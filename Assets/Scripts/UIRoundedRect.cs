using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inyecta el tamaño del RectTransform y el radio de esquina en los vértices del
/// gráfico, para que el shader MedicalViewer/UIRoundedRect pueda dibujar la esquina
/// en píxeles reales.
///
/// Hace falta porque un material es compartido por muchos elementos de distinto
/// tamaño, y CanvasRenderer no admite MaterialPropertyBlock: el canal de vértices
/// es la única vía por elemento.
/// </summary>
[RequireComponent(typeof(Graphic))]
[ExecuteAlways]
[DisallowMultipleComponent]
public class UIRoundedRect : BaseMeshEffect
{
    [Tooltip("Radio de esquina en píxeles del canvas.")]
    [SerializeField] private float radius = 24f;

    public float Radius
    {
        get => radius;
        set
        {
            radius = value;
            if (graphic != null) graphic.SetVerticesDirty();
        }
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || graphic == null) return;

        Rect r = graphic.rectTransform.rect;
        var info = new Vector4(r.width, r.height, radius, 0f);

        var vertex = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref vertex, i);
            vertex.uv1 = info; // TEXCOORD1: lo que lee el shader
            vh.SetUIVertex(vertex, i);
        }
    }
}
