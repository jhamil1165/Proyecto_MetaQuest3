using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Instala el conmutador manos/mandos y verifica que los órganos sean agarrables por
/// varios interactores, para que el modo activo no dependa de quién llegó primero.
/// </summary>
public static class MedicalInteractorMode
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step43 - Hands vs Controllers Switch")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        Transform trackingSpace = rig != null ? rig.transform.Find("TrackingSpace") : null;
        if (trackingSpace == null)
        {
            Debug.LogError("STEP43_FAILED TrackingSpace no encontrado");
            return;
        }

        var handObjects = new List<Object>();
        var controllerObjects = new List<Object>();
        var controllerModels = new List<Object>();
        OVRHand leftHand = null, rightHand = null;

        foreach (var side in new[] { "Left", "Right" })
        {
            Transform anchor = trackingSpace.Find($"{side}HandAnchor");
            if (anchor == null)
            {
                sb.AppendLine($"[WARN] {side}HandAnchor no encontrado");
                continue;
            }

            Transform handInteractor = anchor.Find("HandInteractor");
            if (handInteractor != null) handObjects.Add(handInteractor.gameObject);

            OVRHand hand = anchor.GetComponentInChildren<OVRHand>(true);
            if (side == "Left") leftHand = hand; else rightHand = hand;

            Transform ctrlAnchor = anchor.Find($"{side}ControllerAnchor");
            if (ctrlAnchor != null)
            {
                // El anchor lleva el ray interactor: desactivarlo apaga su rayo entero.
                var ray = ctrlAnchor.GetComponent<XRRayInteractor>();
                if (ray != null) controllerObjects.Add(ctrlAnchor.gameObject);

                foreach (Transform child in ctrlAnchor)
                {
                    if (child.name.StartsWith("ControllerModel_")) controllerModels.Add(child.gameObject);
                }
            }
        }

        GameObject host = GameObject.Find("UI_Manager");
        if (host == null)
        {
            Debug.LogError("STEP43_FAILED UI_Manager no encontrado");
            return;
        }

        var mode = host.GetComponent<InteractorModeSwitch>();
        if (mode == null) mode = Undo.AddComponent<InteractorModeSwitch>(host);

        var so = new SerializedObject(mode);
        FillList(so, "handInteractors", handObjects);
        FillList(so, "controllerInteractors", controllerObjects);
        FillList(so, "controllerModels", controllerModels);
        so.FindProperty("leftHand").objectReferenceValue = leftHand;
        so.FindProperty("rightHand").objectReferenceValue = rightHand;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mode);

        sb.AppendLine($"conmutador: {handObjects.Count} interactores de mano, " +
                      $"{controllerObjects.Count} de mando, {controllerModels.Count} modelos");

        // Multiple: si el modo fuera Single y un interactor quedara enganchado, ningun
        // otro podria agarrar el organo aunque el primero ya no se use.
        var grabs = Object.FindObjectsOfType<XRGrabInteractable>(true);
        foreach (var grab in grabs)
        {
            grab.selectMode = InteractableSelectMode.Multiple;
            EditorUtility.SetDirty(grab);
        }
        sb.AppendLine($"{grabs.Length} agarrables -> selectMode = Multiple");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step43_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP43_DONE");
    }

    private static void FillList(SerializedObject so, string propertyName, List<Object> values)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null) return;

        prop.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
