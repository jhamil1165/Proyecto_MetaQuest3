using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Corrige el tipo de mano de los OVRHand / OVRSkeleton / OVRMesh de la escena.
///
/// El bug: al instalarlos se usó SerializedProperty.enumValueIndex, que es la POSICIÓN
/// del valor en la lista del enum, no el valor. Y estos enums empiezan en -1:
///
///     None = -1,  HandLeft = 0,  HandRight = 1
///
/// Así que escribir el índice 0 dejaba "None" (mano que nunca se rastrea) y el índice
/// 1 dejaba "HandLeft" en la mano derecha. De ahí que solo apareciera una mano y en el
/// sitio equivocado. Con intValue se escribe el valor real.
/// </summary>
public static class MedicalHandTypeFix
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    [MenuItem("MedicalViewer/Step39 - Fix Hand Types")]
    public static void Apply()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rig = GameObject.Find("[BuildingBlock] Camera Rig");
        Transform trackingSpace = rig != null ? rig.transform.Find("TrackingSpace") : null;
        if (trackingSpace == null)
        {
            Debug.LogError("STEP39_FAILED TrackingSpace no encontrado");
            return;
        }

        FixHand(trackingSpace, "LeftHandAnchor", OVRHand.Hand.HandLeft,
            OVRSkeleton.SkeletonType.HandLeft, OVRMesh.MeshType.HandLeft, sb);
        FixHand(trackingSpace, "RightHandAnchor", OVRHand.Hand.HandRight,
            OVRSkeleton.SkeletonType.HandRight, OVRMesh.MeshType.HandRight, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        System.IO.File.WriteAllText(OutDir + "step39_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP39_DONE");
    }

    private static void FixHand(Transform trackingSpace, string anchorName, OVRHand.Hand handType,
        OVRSkeleton.SkeletonType skeletonType, OVRMesh.MeshType meshType, StringBuilder sb)
    {
        Transform anchor = trackingSpace.Find(anchorName);
        if (anchor == null)
        {
            sb.AppendLine($"[WARN] {anchorName} no encontrado");
            return;
        }

        var hand = anchor.GetComponentInChildren<OVRHand>(true);
        if (hand == null)
        {
            sb.AppendLine($"[WARN] {anchorName} sin OVRHand");
            return;
        }

        var so = new SerializedObject(hand);
        var prop = so.FindProperty("HandType");
        int before = prop.intValue;
        prop.intValue = (int)handType; // intValue, no enumValueIndex
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hand);
        sb.AppendLine($"{anchorName}: OVRHand.HandType  {before} -> {(int)handType} ({handType})");

        var skeleton = hand.GetComponent<OVRSkeleton>();
        if (skeleton != null)
        {
            var sso = new SerializedObject(skeleton);
            var sp = sso.FindProperty("_skeletonType");
            int sBefore = sp.intValue;
            sp.intValue = (int)skeletonType;
            sso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(skeleton);
            sb.AppendLine($"{anchorName}: SkeletonType  {sBefore} -> {(int)skeletonType} ({skeletonType})");
        }

        var mesh = hand.GetComponent<OVRMesh>();
        if (mesh != null)
        {
            var mso = new SerializedObject(mesh);
            var mp = mso.FindProperty("_meshType");
            int mBefore = mp.intValue;
            mp.intValue = (int)meshType;
            mso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(mesh);
            sb.AppendLine($"{anchorName}: MeshType  {mBefore} -> {(int)meshType} ({meshType})");
        }
    }
}
