using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Monta el corte del volumen: la rebanada donde se dibuja el TAC y el componente que la
/// coloca donde toca.
///
/// Arregla de paso un fallo que aparecio al subir el volumen por encima de los organos: el
/// plano de corte se quedaba por debajo del volumen entero, asi que al activar el corte el
/// volumen desaparecia de golpe en vez de cortarse.
/// </summary>
public static class MedicalVolumeCut
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";
    private const string SliceName = "Corte_TAC";
    private const string MatDir = "Assets/Materials/Volume";

    [MenuItem("MedicalViewer/Step77 - Corte del volumen con la imagen del TAC")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject volumeGo = GameObject.Find("Volumen_3D");
        var controller = Object.FindObjectOfType<ClippingPlaneController>(true);
        if (volumeGo == null || controller == null)
        {
            Debug.LogError("[Step77] Falta Volumen_3D o el ClippingPlaneController.");
            return;
        }

        var renderer = volumeGo.GetComponent<Renderer>();
        Material volumeMat = renderer.sharedMaterial;

        Transform handle = new SerializedObject(controller).FindProperty("planeHandle").objectReferenceValue as Transform;
        if (handle == null)
        {
            Debug.LogError("[Step77] El controlador no tiene plano asignado.");
            return;
        }
        sb.AppendLine("plano de corte: " + handle.name + " en " + handle.position.ToString("F2"));
        sb.AppendLine("volumen centrado en " + renderer.bounds.center.ToString("F2") +
                      ", alto " + renderer.bounds.size.y.ToString("F2") + " m");

        // ---- material de la rebanada ----
        Shader shader = Shader.Find("MedicalViewer/VolumeSlice");
        if (shader == null)
        {
            Debug.LogError("[Step77] No encuentro el shader MedicalViewer/VolumeSlice.");
            return;
        }

        System.IO.Directory.CreateDirectory(MatDir);
        string matPath = MatDir + "/Mat_Corte_TAC.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, matPath);
        }
        material.shader = shader;
        material.SetTexture("_Density", volumeMat.GetTexture("_Density"));
        material.SetTexture("_Organs", volumeMat.GetTexture("_Organs"));
        material.SetFloat("_WindowCenter", 40f);
        material.SetFloat("_WindowWidth", 400f);
        EditorUtility.SetDirty(material);
        sb.AppendLine("material de la rebanada: " + matPath);

        // ---- la rebanada ----
        // Cuelga del mismo padre que el volumen, no del volumen: el volumen esta escalado
        // 0,50 x 0,87 x 0,50 y una hija saldria aplastada.
        Transform parent = volumeGo.transform.parent;
        Transform existing = parent != null ? parent.Find(SliceName) : null;

        GameObject slice = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Quad);
        slice.name = SliceName;
        if (parent != null) slice.transform.SetParent(parent, false);

        // Sin collider: si lo tuviera, el rayo del mando mediria contra la rebanada y no
        // se podrian agarrar los organos que hubiera detras.
        var collider = slice.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);

        var sliceRenderer = slice.GetComponent<Renderer>();
        sliceRenderer.sharedMaterial = material;
        sliceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sliceRenderer.receiveShadows = false;
        sliceRenderer.enabled = false;   // lo enciende el script cuando hay corte
        EditorUtility.SetDirty(slice);

        // ---- el componente que lo mueve todo ----
        var cut = volumeGo.GetComponent<VolumeCutSlice>();
        if (cut == null) cut = volumeGo.AddComponent<VolumeCutSlice>();

        var so = new SerializedObject(cut);
        so.FindProperty("controller").objectReferenceValue = controller;
        so.FindProperty("planeHandle").objectReferenceValue = handle;
        so.FindProperty("volume").objectReferenceValue = renderer;
        so.FindProperty("slice").objectReferenceValue = sliceRenderer;

        // Diagonal de la caja: asi la rebanada tapa el volumen gire como gire.
        Vector3 size = renderer.bounds.size;
        so.FindProperty("sliceSize").floatValue = size.magnitude * 1.05f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(cut);
        sb.AppendLine("rebanada: " + (size.magnitude * 1.05f).ToString("F2") + " m de lado");

        // ---- el plano azul se aparta de la vista ----
        // Desde que la rebanada dibuja el TAC encima, el plano solo sirve de asa para
        // agarrarlo. Con su azul original teñía la imagen médica de azul, que es justo lo
        // que no puede pasar cuando lo que se mira es una tomografía.
        var planeRenderer = handle.GetComponentInChildren<Renderer>(true);
        if (planeRenderer != null)
        {
            string handlePath = MatDir + "/Mat_Plano_Corte.mat";
            var handleMat = AssetDatabase.LoadAssetAtPath<Material>(handlePath);
            if (handleMat == null)
            {
                Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
                handleMat = new Material(unlit);
                AssetDatabase.CreateAsset(handleMat, handlePath);
            }

            var tint = new Color(0.16f, 0.52f, 0.72f, 0.14f);
            handleMat.SetColor("_BaseColor", tint);
            handleMat.SetColor("_Color", tint);
            handleMat.SetFloat("_Surface", 1f);
            handleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            handleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            handleMat.SetInt("_ZWrite", 0);
            handleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            handleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(handleMat);

            planeRenderer.sharedMaterial = handleMat;
            EditorUtility.SetDirty(planeRenderer);
            sb.AppendLine("plano azul: casi transparente, para no teñir el TAC");
        }

        // ---- "Centrar" tiene que contar con el volumen ----
        var anchor = Object.FindObjectOfType<ClippingPlaneAnchor>(true);
        if (anchor != null)
        {
            var aso2 = new SerializedObject(anchor);
            var list = aso2.FindProperty("organs");

            bool already = false;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == volumeGo) already = true;
            }

            if (!already)
            {
                list.arraySize += 1;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = volumeGo;
                aso2.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(anchor);
            }

            aso2 = new SerializedObject(anchor);
            aso2.FindProperty("preferred").objectReferenceValue = volumeGo;
            aso2.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(anchor);

            sb.AppendLine("boton Centrar: lleva el plano al centro del volumen");
        }

        // ---- el menú la enciende con el resto de la vista ----
        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions != null)
        {
            var aso = new SerializedObject(actions);
            foreach (string field in new[] { "model3DObjects", "segmentationObjects" })
            {
                var group = aso.FindProperty(field);
                bool already = false;
                for (int i = 0; i < group.arraySize; i++)
                {
                    if (group.GetArrayElementAtIndex(i).objectReferenceValue == slice) already = true;
                }
                if (already) continue;

                group.arraySize += 1;
                group.GetArrayElementAtIndex(group.arraySize - 1).objectReferenceValue = slice;
            }
            aso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actions);
            sb.AppendLine("el menu enciende la rebanada en Modelo 3D y en Segmentacion");
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step77_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP77_DONE");
    }
}
