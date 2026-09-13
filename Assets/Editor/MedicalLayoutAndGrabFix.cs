using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Dos arreglos:
///
///  1. El visor de tomografía pasa a la IZQUIERDA y el menú a la derecha.
///  2. Cada órgano recibe un BoxCollider de respaldo. Un MeshCollider convexo se
///     simplifica a 255 polígonos como máximo; con mallas de 20.000 triángulos esa
///     conversión puede quedar degenerada y el órgano se queda sin collider util.
///     Una caja no puede fallar, y garantiza que siempre haya algo que agarrar.
/// </summary>
public static class MedicalLayoutAndGrabFix
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    // Menu a la derecha, contenido (Sistemas / DICOM) a la izquierda.
    private const float MenuAngle = 34f;
    private const float ContentAngle = -20f;

    [MenuItem("MedicalViewer/Step48 - Layout + Grab Fallback")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Relocate(sb);
        AddFallbackColliders(sb);
        DiagnoseAndRepair(sb);
        GrabReport(sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step48_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP48_DONE");
    }

    private static void Relocate(StringBuilder sb)
    {
        GameObject root = GameObject.Find("Medical_Menu_UI");
        if (root == null)
        {
            sb.AppendLine("[WARN] Medical_Menu_UI no encontrado");
            return;
        }

        // Los canvas cuelgan de la raiz en coordenadas locales sobre un arco de radio
        // fijo: basta con recalcular su angulo.
        const float radius = 1.8f;

        Place(root.transform, "Canvas_Actions", MenuAngle, radius, sb);
        Place(root.transform, "Canvas_Systems", ContentAngle, radius, sb);
        Place(root.transform, "Canvas_DICOM", ContentAngle, radius, sb);
    }

    private static void Place(Transform root, string name, float angle, float radius, StringBuilder sb)
    {
        Transform canvas = root.Find(name);
        if (canvas == null)
        {
            sb.AppendLine($"[WARN] {name} no encontrado");
            return;
        }

        Quaternion yaw = Quaternion.Euler(0f, angle, 0f);
        canvas.localPosition = yaw * Vector3.forward * radius;
        canvas.localRotation = yaw;

        EditorUtility.SetDirty(canvas);
        sb.AppendLine($"{name} -> {angle:F0}°  ({(angle < 0 ? "izquierda" : "derecha")})");
    }

    /// <summary>
    /// Por que el visor de TC no aparece. Step47 destruyo y recreo los hijos de
    /// Canvas_DICOM; si de paso se perdio la referencia en MedicalMenuActions, el
    /// boton "VER DICOM" no tiene nada que encender y solo deja un aviso en consola.
    /// Aqui se comprueba y, si falta, se vuelve a enlazar.
    /// </summary>
    private static void DiagnoseAndRepair(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("=== por que no aparece el visor ===");

        GameObject canvas = FindAnywhere("Canvas_DICOM");
        if (canvas == null)
        {
            sb.AppendLine("[FALLO] Canvas_DICOM no existe en la escena.");
            return;
        }

        sb.AppendLine($"Canvas_DICOM: activeSelf={canvas.activeSelf}  hijos={canvas.transform.childCount}");
        sb.AppendLine($"  visor multiplano: {(canvas.GetComponentInChildren<CTMultiPlaneViewer>(true) != null ? "si" : "NO")}");
        sb.AppendLine($"  columnas CTPlaneView: {canvas.GetComponentsInChildren<CTPlaneView>(true).Length}");

        var rt = canvas.GetComponent<RectTransform>();
        if (rt != null)
        {
            sb.AppendLine($"  tamano={rt.sizeDelta}  escala={rt.localScale}  mundo={rt.position}");
        }

        // Un padre desactivado deja el canvas invisible aunque este activeSelf=true.
        for (Transform p = canvas.transform.parent; p != null; p = p.parent)
        {
            if (!p.gameObject.activeSelf)
                sb.AppendLine($"  [OJO] padre desactivado: {p.name}");
        }

        var actions = Object.FindObjectOfType<MedicalMenuActions>(true);
        if (actions == null)
        {
            sb.AppendLine("[FALLO] no hay MedicalMenuActions en la escena.");
            return;
        }

        var so = new SerializedObject(actions);
        SerializedProperty list = so.FindProperty("dicomObjects");

        sb.AppendLine($"MedicalMenuActions.dicomObjects: {list.arraySize} elemento(s)");

        bool linked = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            Object o = list.GetArrayElementAtIndex(i).objectReferenceValue;
            sb.AppendLine($"  [{i}] {(o == null ? "<VACIO>" : o.name)}");
            if (o == canvas) linked = true;
        }

        if (!linked)
        {
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = canvas;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actions);
            sb.AppendLine("  -> REPARADO: dicomObjects apunta ahora a Canvas_DICOM");
        }
        else
        {
            sb.AppendLine("  -> el enlace ya era correcto");
        }
    }

    /// <summary>
    /// El corazon es el unico organo que nunca se ha movido. Volcamos su configuracion
    /// completa en vez de seguir adivinando: capa, mascara de interaccion, rigidbody y
    /// cada collider con su estado real de convexidad.
    /// </summary>
    private static void GrabReport(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("=== estado de agarre por organo ===");

        foreach (var grab in Object.FindObjectsOfType<XRGrabInteractable>(true))
        {
            GameObject go = grab.gameObject;
            sb.AppendLine($"{go.name}:");
            sb.AppendLine($"  activo={go.activeInHierarchy}  capa={LayerMask.LayerToName(go.layer)}({go.layer})  " +
                          $"componente habilitado={grab.enabled}");
            sb.AppendLine($"  interactionLayers={grab.interactionLayers.value}  " +
                          $"movimiento={grab.movementType}  attachDinamico={grab.useDynamicAttach}");

            var rb = go.GetComponent<Rigidbody>();
            sb.AppendLine(rb == null
                ? "  [OJO] sin Rigidbody: XRGrabInteractable lo exige"
                : $"  rigidbody: kinematic={rb.isKinematic} gravedad={rb.useGravity}");

            var cols = go.GetComponents<Collider>();
            if (cols.Length == 0)
            {
                sb.AppendLine("  [FALLO] sin ningun collider: imposible de agarrar");
            }

            foreach (var c in cols)
            {
                string extra = c is MeshCollider mc ? $" convex={mc.convex} malla={(mc.sharedMesh == null ? "NULA" : mc.sharedMesh.name)}" : "";
                sb.AppendLine($"  collider {c.GetType().Name}: habilitado={c.enabled} trigger={c.isTrigger}" +
                              $" bounds={c.bounds.size:F3}{extra}");
            }

            // Los colliders que XRI usara realmente son los de la lista del interactable,
            // no todos los del objeto: si esa lista quedo vacia, no hay agarre.
            sb.AppendLine($"  colliders registrados en el interactable: {grab.colliders.Count}");
        }
    }

    private static GameObject FindAnywhere(string name)
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        }
        return null;
    }

    private static void AddFallbackColliders(StringBuilder sb)
    {
        var grabs = Object.FindObjectsOfType<XRGrabInteractable>(true);
        sb.AppendLine($"--- collider de respaldo en {grabs.Length} agarrables ---");

        foreach (var grab in grabs)
        {
            GameObject go = grab.gameObject;

            if (go.GetComponent<BoxCollider>() != null)
            {
                sb.AppendLine($"{go.name}: ya tenia BoxCollider");
                continue;
            }

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                sb.AppendLine($"{go.name}: sin malla, se omite");
                continue;
            }

            // Los bounds de la malla estan en espacio local, que es justo lo que
            // espera BoxCollider: no hace falta convertir por la escala del objeto.
            Bounds local = mf.sharedMesh.bounds;

            var box = Undo.AddComponent<BoxCollider>(go);
            box.center = local.center;
            box.size = local.size;

            EditorUtility.SetDirty(box);

            int tris = mf.sharedMesh.triangles.Length / 3;
            sb.AppendLine($"{go.name}: BoxCollider añadido  (malla de {tris:N0} tris, " +
                          $"tamaño local {local.size:F3})");
        }
    }
}
