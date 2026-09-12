using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Diagnostica y repara el agarre de los órganos.
///
/// Sospecha principal: los modelos de 3D Slicer guardan el pivote en el origen del
/// volumen de la TC, lejos de la malla visible. Al agarrar, XRI alinea el punto de
/// anclaje con la mano; si ese punto es el pivote, el órgano salta para llevarlo allí
/// y la malla se va a otro sitio. De ahí el "se teletransportan abajo".
///
/// El informe mide esa distancia pivote-malla para confirmarlo con datos.
/// </summary>
public static class MedicalGrabDiagnose
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step45 - Diagnose + Fix Grab")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var grabs = Object.FindObjectsOfType<XRGrabInteractable>(true);
        sb.AppendLine($"=== {grabs.Length} objetos agarrables ===");

        foreach (var grab in grabs)
        {
            Report(grab, sb);
            Repair(grab, sb);
            sb.AppendLine();
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step45_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP45_DONE");
    }

    private static void Report(XRGrabInteractable grab, StringBuilder sb)
    {
        var t = grab.transform;
        sb.AppendLine($"--- {t.name} ---");

        // Distancia entre el pivote y el centro real de la malla: si es grande, el
        // salto al agarrar esta explicado.
        var renderers = grab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            float offset = Vector3.Distance(t.position, b.center);
            sb.AppendLine($"  pivote a centro de malla: {offset:F3} m  " +
                          (offset > 0.05f ? "<-- DESPLAZADO" : "(ok)"));
            sb.AppendLine($"  tamaño de malla: {b.size:F3}");
        }

        var colliders = grab.colliders;
        sb.AppendLine($"  colliders registrados: {(colliders != null ? colliders.Count : 0)}");

        var mc = grab.GetComponentInChildren<MeshCollider>(true);
        if (mc != null)
        {
            sb.AppendLine($"  MeshCollider: mesh={(mc.sharedMesh != null ? mc.sharedMesh.name : "NULO")} " +
                          $"convex={mc.convex} trigger={mc.isTrigger} enabled={mc.enabled}");
        }
        else
        {
            sb.AppendLine("  MeshCollider: NINGUNO  <-- sin collider no se puede agarrar");
        }

        var rb = grab.GetComponent<Rigidbody>();
        if (rb != null)
        {
            sb.AppendLine($"  Rigidbody: kinematic={rb.isKinematic} gravity={rb.useGravity}");
        }

        sb.AppendLine($"  dynamicAttach={grab.useDynamicAttach} movement={grab.movementType} " +
                      $"selectMode={grab.selectMode} attachTransform=" +
                      $"{(grab.attachTransform != null ? grab.attachTransform.name : "ninguno")}");
    }

    private static void Repair(XRGrabInteractable grab, StringBuilder sb)
    {
        // Anclaje dinamico: el objeto se agarra POR DONDE lo tocas, no por su pivote.
        // Es lo que evita el salto en modelos con el pivote descentrado.
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;
        grab.snapToColliderVolume = true;
        grab.attachTransform = null; // cualquier anclaje fijo anularia lo anterior

        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.throwOnDetach = false;
        grab.trackPosition = true;
        grab.trackRotation = true;

        // Un MeshCollider no convexo no genera eventos de trigger, asi que el agarre
        // por contacto (que usa una esfera trigger) nunca lo detectaba.
        foreach (var mc in grab.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc.sharedMesh == null)
            {
                var mf = mc.GetComponent<MeshFilter>();
                if (mf != null) mc.sharedMesh = mf.sharedMesh;
            }

            if (!mc.convex)
            {
                mc.convex = true;
                sb.AppendLine($"  {mc.name}: MeshCollider -> convex (necesario para agarre por contacto)");
            }

            EditorUtility.SetDirty(mc);
        }

        EditorUtility.SetDirty(grab);
        sb.AppendLine("  reparado: anclaje dinamico + colliders convexos");
    }
}
