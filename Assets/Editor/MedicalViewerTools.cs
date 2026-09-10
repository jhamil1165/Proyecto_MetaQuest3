using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only utility used to inspect and incrementally modify SampleScene
/// for the Medical Viewer VR project. Each public static method is invoked
/// individually from the command line via -executeMethod during development.
/// Safe to delete once the redesign work is finished.
/// </summary>
public static class MedicalViewerTools
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/"; // outside Assets/ so the AssetDatabase never tries to import it mid-write

    private static GameObject Find(string name)
    {
        // GameObject.Find only matches active objects in the loaded scene, which is what we want here.
        GameObject go = GameObject.Find(name);
        return go;
    }

    private static string Describe(Transform t, int depth = 0)
    {
        var sb = new StringBuilder();
        string indent = new string(' ', depth * 2);
        sb.AppendLine($"{indent}{t.name}  pos={t.localPosition:F4} rot={t.localEulerAngles:F2} scale={t.localScale:F4} components=[{string.Join(",", t.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name))}]");
        return sb.ToString();
    }

    [MenuItem("MedicalViewer/Dump Scene Diagnostics")]
    public static void DumpDiagnostics()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        string[] namesToFind =
        {
            "UI_Manager", "Medical_Menu", "Title_MedicalViewer", "Btn_DICOM", "Button_DICOM_BG",
            "Btn_Segmentacion", "Button_Segmentacion_BG", "Btn_Modelo3D", "Button_Modelo3D_BG",
            "Btn_Salir", "Button_Salir_BG", "CenterEyeAnchor", "TrackingSpace", "[BuildingBlock] Camera Rig"
        };

        foreach (var n in namesToFind)
        {
            GameObject go = Find(n);
            if (go == null)
            {
                sb.AppendLine($"[MISSING] {n}");
                continue;
            }
            sb.AppendLine($"[FOUND] {n}  parent={(go.transform.parent ? go.transform.parent.name : "<root>")}");
            sb.Append(Describe(go.transform));
        }

        // Look for the model instances anywhere in the scene by matching mesh/fbx name fragments.
        sb.AppendLine("--- Searching all root GameObjects for model instances ---");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            DumpTreeIfMatches(root.transform, sb);
        }

        string outPath = OutDir + "diagnostics_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log("DIAGNOSTICS_WRITTEN:" + outPath);
        Debug.Log(sb.ToString());
    }

    [MenuItem("MedicalViewer/Step1 - Position Menu")]
    public static void Step1_PositionMenu()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject menu = Find("Medical_Menu");
        GameObject centerEye = Find("CenterEyeAnchor");

        if (menu == null || centerEye == null)
        {
            Debug.LogError($"STEP1_FAILED missing_menu={menu == null} missing_centerEye={centerEye == null}");
            return;
        }

        Transform menuT = menu.transform;
        Transform eyeT = centerEye.transform;

        // 2m forward, 1.5m up, centered, expressed in CenterEyeAnchor's own local space
        // so it stays correct regardless of the camera rig's current world rotation.
        Vector3 worldPos = eyeT.TransformPoint(new Vector3(0f, 1.5f, 2f));
        Quaternion worldRot = Quaternion.Euler(0f, 180f, 0f);

        Undo.RecordObject(menuT, "Step1 Position Medical_Menu");
        menuT.position = worldPos;
        menuT.rotation = worldRot;

        const float targetScale = 0.4f;
        menuT.localScale = new Vector3(targetScale, targetScale, targetScale);

        EditorUtility.SetDirty(menu);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);

        sb.AppendLine("STEP1_DONE");
        sb.AppendLine($"world_pos={menuT.position:F4}");
        sb.AppendLine($"local_pos_under_{menuT.parent.name}={menuT.localPosition:F4}");
        sb.AppendLine($"local_rot={menuT.localEulerAngles:F2}");
        sb.AppendLine($"local_scale={menuT.localScale:F4}");
        sb.AppendLine($"scene_saved={saved}");

        string outPath = OutDir + "step1_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
    }

    [MenuItem("MedicalViewer/Step2 - Visual Redesign (Holo Glass)")]
    public static void Step2_VisualRedesign()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        Shader holoShader = Shader.Find("MedicalViewer/HoloGlassPanel");
        if (holoShader == null)
        {
            Debug.LogError("STEP2_FAILED shader_not_found=MedicalViewer/HoloGlassPanel");
            return;
        }

        System.IO.Directory.CreateDirectory("Assets/Materials/Holo");

        // Main panel: a bit more present than the buttons, still very translucent.
        Material mainMat = CreateOrReplaceMaterial(
            "Assets/Materials/Holo/Mat_HoloPanel_Main.mat", holoShader,
            baseColor: new Color(0.35f, 0.85f, 1f, 0.18f),
            rimColor: new Color(0.65f, 0.97f, 1f, 1f),
            rimPower: 2.5f, rimIntensity: 1.2f);

        // Buttons: subtler body + thinner rim so the main panel still reads as the anchor.
        Material buttonMat = CreateOrReplaceMaterial(
            "Assets/Materials/Holo/Mat_HoloPanel_Button.mat", holoShader,
            baseColor: new Color(0.35f, 0.85f, 1f, 0.10f),
            rimColor: new Color(0.7f, 0.98f, 1f, 1f),
            rimPower: 3f, rimIntensity: 0.6f);

        GameObject menu = Find("Medical_Menu");
        if (menu != null && menu.TryGetComponent(out Renderer menuRenderer))
        {
            menuRenderer.sharedMaterial = mainMat;
            sb.AppendLine("Medical_Menu -> Mat_HoloPanel_Main");
        }
        else
        {
            sb.AppendLine("[WARN] Medical_Menu renderer not found");
        }

        string[] buttonBgNames =
        {
            "Button_DICOM_BG", "Button_Segmentacion_BG", "Button_Modelo3D_BG", "Button_Salir_BG"
        };

        foreach (var bgName in buttonBgNames)
        {
            GameObject bg = Find(bgName);
            if (bg == null)
            {
                sb.AppendLine($"[WARN] {bgName} not found");
                continue;
            }

            if (bg.TryGetComponent(out Renderer bgRenderer))
            {
                bgRenderer.sharedMaterial = buttonMat;
            }

            if (bg.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>() == null)
            {
                sb.AppendLine($"[WARN] {bgName} has no XRSimpleInteractable, HoloHoverGlow skipped");
                continue;
            }

            if (bg.GetComponent<HoloHoverGlow>() == null)
            {
                Undo.AddComponent<HoloHoverGlow>(bg);
            }

            sb.AppendLine($"{bgName} -> Mat_HoloPanel_Button + HoloHoverGlow");
        }

        // Typography: thin/light look via a dedicated TMP material (negative face dilate),
        // uppercase + wide letter spacing on the button labels only (titles stay as typed).
        Material tmpHoloMat = CreateOrReplaceTmpHoloMaterial("Assets/Materials/Holo/Mat_TMP_Holo.mat");

        string[] titleNames = { "Title_MedicalViewer", "Title_MedicalViewer (1)" };
        string[] buttonLabelNames = { "Btn_DICOM", "Btn_Segmentacion", "Btn_Modelo3D", "Btn_Salir" };

        foreach (var n in titleNames)
        {
            ApplyHoloTypography(Find(n), tmpHoloMat, uppercase: false, sb);
        }
        foreach (var n in buttonLabelNames)
        {
            ApplyHoloTypography(Find(n), tmpHoloMat, uppercase: true, sb);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine($"scene_saved={saved}");

        string outPath = OutDir + "step2_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP2_DONE");
    }

    [MenuItem("MedicalViewer/Step3 - Appear Animation")]
    public static void Step3_AppearAnimation()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        GameObject menu = Find("Medical_Menu");
        if (menu == null)
        {
            Debug.LogError("STEP3_FAILED Medical_Menu not found");
            return;
        }

        if (menu.GetComponent<MedicalMenuIntro>() == null)
        {
            Undo.AddComponent<MedicalMenuIntro>(menu);
            sb.AppendLine("Medical_Menu -> added MedicalMenuIntro (fade + scale-in, 0.35s)");
        }
        else
        {
            sb.AppendLine("Medical_Menu already had MedicalMenuIntro, left as-is");
        }

        EditorUtility.SetDirty(menu);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine($"scene_saved={saved}");

        string outPath = OutDir + "step3_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP3_DONE");
    }

    [MenuItem("MedicalViewer/Step4 - Organ Selection + Info Panel")]
    public static void Step4_OrganSelection()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        Shader outlineShader = Shader.Find("MedicalViewer/HoloOutline");
        Shader glassShader = Shader.Find("MedicalViewer/HoloGlassPanel");
        if (outlineShader == null || glassShader == null)
        {
            Debug.LogError($"STEP4_FAILED outline_missing={outlineShader == null} glass_missing={glassShader == null}");
            return;
        }

        System.IO.Directory.CreateDirectory("Assets/Materials/Holo");

        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloOutline.mat");
        if (outlineMat == null)
        {
            outlineMat = new Material(outlineShader);
            AssetDatabase.CreateAsset(outlineMat, "Assets/Materials/Holo/Mat_HoloOutline.mat");
        }
        outlineMat.shader = outlineShader;
        outlineMat.SetColor("_OutlineColor", new Color(0.6f, 0.95f, 1f, 1f));
        outlineMat.SetFloat("_OutlineWidth", 0.004f);
        outlineMat.SetFloat("_OutlineAlpha", 0f);
        EditorUtility.SetDirty(outlineMat);

        Material panelMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloPanel_Button.mat");
        Material tmpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_TMP_Holo.mat");

        GameObject panelRoot = BuildOrFindInfoPanel(panelMat, tmpMat, sb);

        SetupOrgan("Heart", "CORAZÓN", outlineMat, sb);
        SetupOrgan("estomago_sin_render", "ESTÓMAGO", outlineMat, sb);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine($"info_panel={(panelRoot != null ? panelRoot.name : "NULL")}");
        sb.AppendLine($"scene_saved={saved}");

        string outPath = OutDir + "step4_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP4_DONE");
    }

    private static void SetupOrgan(string rootName, string displayName, Material outlineMat, StringBuilder sb)
    {
        GameObject root = Find(rootName);
        if (root == null)
        {
            sb.AppendLine($"[WARN] organ {rootName} not found");
            return;
        }

        // The FBX root sometimes only holds a Transform (estomago), with the mesh on a child.
        MeshFilter meshFilter = root.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = root.GetComponentInChildren<MeshFilter>();
        }

        if (meshFilter == null)
        {
            sb.AppendLine($"[WARN] {rootName} has no MeshFilter anywhere, skipped");
            return;
        }

        GameObject host = meshFilter.gameObject;

        if (host.GetComponent<MeshCollider>() == null)
        {
            var mc = Undo.AddComponent<MeshCollider>(host);
            mc.sharedMesh = meshFilter.sharedMesh;
            mc.convex = false; // raycast-only interaction, no physics needed
            sb.AppendLine($"{rootName}/{host.name} -> MeshCollider added");
        }

        if (host.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>() == null)
        {
            Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>(host);
            sb.AppendLine($"{rootName}/{host.name} -> XRSimpleInteractable added");
        }

        var info = host.GetComponent<OrganSelectionInfo>();
        if (info == null)
        {
            info = Undo.AddComponent<OrganSelectionInfo>(host);
            sb.AppendLine($"{rootName}/{host.name} -> OrganSelectionInfo added");
        }

        var so = new SerializedObject(info);
        so.FindProperty("organName").stringValue = displayName;
        so.FindProperty("organInfo").stringValue = $"{displayName}\n(Placeholder - completar con datos médicos reales.)";
        so.ApplyModifiedPropertiesWithoutUndo();

        // Append the outline material as a second slot so the inverted hull renders
        // over the existing model material without replacing it.
        if (host.TryGetComponent(out Renderer hostRenderer))
        {
            var mats = hostRenderer.sharedMaterials;
            bool hasOutline = mats.Any(m => m != null && m.shader != null && m.shader.name == "MedicalViewer/HoloOutline");
            if (!hasOutline)
            {
                var newMats = mats.ToList();
                newMats.Add(outlineMat);
                hostRenderer.sharedMaterials = newMats.ToArray();
                sb.AppendLine($"{rootName}/{host.name} -> outline material appended (slots={newMats.Count})");
            }
        }

        EditorUtility.SetDirty(host);
    }

    private static GameObject BuildOrFindInfoPanel(Material panelMat, Material tmpMat, StringBuilder sb)
    {
        GameObject root = Find("Organ_Info_Panel");
        if (root != null)
        {
            sb.AppendLine("Organ_Info_Panel already exists, left as-is");
            return root;
        }

        root = new GameObject("Organ_Info_Panel");
        Undo.RegisterCreatedObjectUndo(root, "Create Organ_Info_Panel");

        // Child that gets toggled - carries the intro animation so it fades/scales in.
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Panel_BG";
        bg.transform.SetParent(visual.transform, false);
        bg.transform.localScale = new Vector3(0.36f, 0.2f, 1f);
        Object.DestroyImmediate(bg.GetComponent<Collider>()); // purely decorative
        if (panelMat != null && bg.TryGetComponent(out Renderer bgRenderer))
        {
            bgRenderer.sharedMaterial = panelMat;
        }

        TMP_Text title = CreatePanelText("Organ_Info_Title", visual.transform, tmpMat,
            new Vector3(0f, 0.055f, -0.01f), new Vector2(0.32f, 0.05f), TextAlignmentOptions.Center);
        TMP_Text body = CreatePanelText("Organ_Info_Body", visual.transform, tmpMat,
            new Vector3(0f, -0.025f, -0.01f), new Vector2(0.32f, 0.10f), TextAlignmentOptions.Top);

        title.text = "ÓRGANO";
        body.text = "Información médica pendiente.";

        var intro = visual.AddComponent<MedicalMenuIntro>();
        var controller = root.AddComponent<OrganInfoPanelController>();

        var so = new SerializedObject(controller);
        so.FindProperty("visual").objectReferenceValue = visual;
        so.FindProperty("titleText").objectReferenceValue = title;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.ApplyModifiedPropertiesWithoutUndo();

        visual.SetActive(false); // hidden until an organ is selected

        sb.AppendLine("Organ_Info_Panel created (root + Visual + Panel_BG + title/body TMP)");
        sb.AppendLine($"  intro_component={intro.GetType().Name}");
        return root;
    }

    private static TMP_Text CreatePanelText(string name, Transform parent, Material tmpMat, Vector3 localPos, Vector2 size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.alignment = alignment;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.01f;
        tmp.fontSizeMax = 0.4f;
        tmp.characterSpacing = 6f;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = Color.white;

        if (tmpMat != null)
        {
            tmp.fontSharedMaterial = tmpMat;
        }

        var rt = go.GetComponent<RectTransform>();
        rt.localPosition = localPos;
        rt.sizeDelta = size;

        return tmp;
    }

    [MenuItem("MedicalViewer/Step5 - Wire Buttons To Actions")]
    public static void Step5_WireButtons()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // MedicalMenuActions wasn't on any GameObject yet - host it on UI_Manager,
        // the parent of the menu, so the reference is stable in the Inspector.
        MedicalMenuActions actions = Object.FindObjectOfType<MedicalMenuActions>();
        if (actions == null)
        {
            GameObject host = Find("UI_Manager");
            if (host == null)
            {
                Debug.LogError("STEP5_FAILED UI_Manager not found and no MedicalMenuActions in scene");
                return;
            }

            actions = Undo.AddComponent<MedicalMenuActions>(host);
            sb.AppendLine("UI_Manager -> MedicalMenuActions added");
        }
        else
        {
            sb.AppendLine($"MedicalMenuActions found on {actions.gameObject.name}");
        }

        WireButton("Button_DICOM_BG", actions, actions.OpenDICOM, nameof(MedicalMenuActions.OpenDICOM), sb);
        WireButton("Button_Segmentacion_BG", actions, actions.OpenSegmentation, nameof(MedicalMenuActions.OpenSegmentation), sb);
        WireButton("Button_Modelo3D_BG", actions, actions.Open3DModel, nameof(MedicalMenuActions.Open3DModel), sb);
        WireButton("Button_Salir_BG", actions, actions.ExitMenu, nameof(MedicalMenuActions.ExitMenu), sb);

        EditorUtility.SetDirty(actions);
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine($"scene_saved={saved}");

        string outPath = OutDir + "step5_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP5_DONE");
    }

    private static void WireButton(string buttonName, MedicalMenuActions actions, UnityEngine.Events.UnityAction call, string methodName, StringBuilder sb)
    {
        GameObject button = Find(buttonName);
        if (button == null)
        {
            sb.AppendLine($"[WARN] {buttonName} not found");
            return;
        }

        var interactable = button.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>();
        if (interactable == null)
        {
            sb.AppendLine($"[WARN] {buttonName} has no XRSimpleInteractable");
            return;
        }

        var evt = interactable.selectEntered;

        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            if (evt.GetPersistentTarget(i) == actions && evt.GetPersistentMethodName(i) == methodName)
            {
                sb.AppendLine($"{buttonName} -> already wired to {methodName}, skipped");
                return;
            }
        }

        // Void persistent listener = exactly what picking a no-arg method in the
        // Inspector's OnSelectEntered list produces, so it stays editable there.
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(evt, call);

        EditorUtility.SetDirty(interactable);
        sb.AppendLine($"{buttonName}.selectEntered -> MedicalMenuActions.{methodName}");
    }

    [MenuItem("MedicalViewer/Step6 - Passthrough + Ice White Palette")]
    public static void Step6_PassthroughAndPalette()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        // ---------- Passthrough ----------
        // An opaque skybox hides passthrough completely, so it has to go. Ambient
        // would fall to black with a null skybox, hence the flat neutral ambient.
        sb.AppendLine($"skybox_before={(RenderSettings.skybox != null ? RenderSettings.skybox.name : "none")}");
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.5f);
        sb.AppendLine("skybox -> none, ambient -> flat neutral (models keep their lighting)");

        GameObject centerEye = Find("CenterEyeAnchor");
        if (centerEye != null && centerEye.TryGetComponent(out Camera cam))
        {
            sb.AppendLine($"camera_clear_before={cam.clearFlags} bg_before={cam.backgroundColor}");
            // Passthrough only shows through a camera that clears to TRANSPARENT black.
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            EditorUtility.SetDirty(cam);
            sb.AppendLine("CenterEyeAnchor camera -> Solid Color, RGBA(0,0,0,0)");
        }
        else
        {
            sb.AppendLine("[WARN] CenterEyeAnchor camera not found");
        }

        // Any large environment sphere would also occlude passthrough. Disabled, not
        // deleted, so a teammate can flip it back on with one checkbox.
        GameObject sphere = Find("Sphere");
        if (sphere != null)
        {
            Vector3 s = sphere.transform.lossyScale;
            string matName = sphere.TryGetComponent(out Renderer sr) && sr.sharedMaterial != null ? sr.sharedMaterial.name : "none";
            sb.AppendLine($"Sphere found scale={s:F2} material={matName}");

            if (Mathf.Max(s.x, s.y, s.z) >= 5f)
            {
                sphere.SetActive(false);
                sb.AppendLine("Sphere -> DISABLED (large enough to be an environment dome, would block passthrough)");
            }
            else
            {
                sb.AppendLine("Sphere -> left untouched (too small to be the environment)");
            }
        }

        var passthroughLayer = Object.FindObjectOfType<OVRPassthroughLayer>();
        if (passthroughLayer != null)
        {
            sb.AppendLine($"OVRPassthroughLayer placement_before={passthroughLayer.overlayType}");
            passthroughLayer.overlayType = OVROverlay.OverlayType.Underlay; // must render BEHIND the scene
            EditorUtility.SetDirty(passthroughLayer);
            sb.AppendLine("OVRPassthroughLayer -> Underlay");
        }
        else
        {
            sb.AppendLine("[WARN] no OVRPassthroughLayer in scene");
        }

        var ovrManager = Object.FindObjectOfType<OVRManager>();
        if (ovrManager != null)
        {
            ovrManager.isInsightPassthroughEnabled = true;
            EditorUtility.SetDirty(ovrManager);
            sb.AppendLine("OVRManager.isInsightPassthroughEnabled -> true");
        }

        // ---------- Ice white palette ----------
        Shader glassShader = Shader.Find("MedicalViewer/HoloGlassPanel");
        if (glassShader != null)
        {
            Color iceBody = new Color(0.82f, 0.89f, 0.96f);
            Color iceRim = new Color(1f, 1f, 1f, 1f);

            CreateOrReplaceMaterial("Assets/Materials/Holo/Mat_HoloPanel_Main.mat", glassShader,
                new Color(iceBody.r, iceBody.g, iceBody.b, 0.20f), iceRim, 2.5f, 1.0f);
            CreateOrReplaceMaterial("Assets/Materials/Holo/Mat_HoloPanel_Button.mat", glassShader,
                new Color(iceBody.r, iceBody.g, iceBody.b, 0.10f), iceRim, 3f, 0.5f);
            sb.AppendLine("glass materials -> ice white (neutral, slight blue tint)");
        }

        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloOutline.mat");
        if (outlineMat != null)
        {
            outlineMat.SetColor("_OutlineColor", new Color(0.95f, 0.98f, 1f, 1f));
            EditorUtility.SetDirty(outlineMat);
            sb.AppendLine("outline -> ice white");
        }

        Material tmpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_TMP_Holo.mat");
        if (tmpMat != null)
        {
            if (tmpMat.HasProperty("_FaceColor")) tmpMat.SetColor("_FaceColor", Color.white);
            if (tmpMat.HasProperty("_OutlineColor")) tmpMat.SetColor("_OutlineColor", new Color(0.75f, 0.85f, 0.95f, 1f));
            if (tmpMat.HasProperty("_OutlineWidth")) tmpMat.SetFloat("_OutlineWidth", 0.04f);
            if (tmpMat.HasProperty("_GlowColor")) tmpMat.SetColor("_GlowColor", new Color(0.9f, 0.95f, 1f, 1f));
            if (tmpMat.HasProperty("_GlowPower")) tmpMat.SetFloat("_GlowPower", 0.25f);

            // Over passthrough the real room can be bright, so white text needs a soft
            // dark drop shadow to stay readable against anything behind it.
            if (tmpMat.HasProperty("_UnderlayColor"))
            {
                tmpMat.EnableKeyword("UNDERLAY_ON");
                tmpMat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.65f));
                tmpMat.SetFloat("_UnderlayOffsetX", 0.4f);
                tmpMat.SetFloat("_UnderlayOffsetY", -0.4f);
                tmpMat.SetFloat("_UnderlaySoftness", 0.3f);
                if (tmpMat.HasProperty("_UnderlayDilate")) tmpMat.SetFloat("_UnderlayDilate", 0.1f);
                sb.AppendLine("TMP -> white face + soft dark underlay (legible over bright passthrough)");
            }

            EditorUtility.SetDirty(tmpMat);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine($"scene_saved={saved}");

        string outPath = OutDir + "step6_output.txt";
        System.IO.File.WriteAllText(outPath, sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP6_DONE");
    }

    [MenuItem("MedicalViewer/Step7 - Dark Smoked Glass + Ice Text")]
    public static void Step7_DarkGlassPalette()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Shader glassShader = Shader.Find("MedicalViewer/HoloGlassPanel");
        if (glassShader == null)
        {
            Debug.LogError("STEP7_FAILED glass shader missing");
            return;
        }

        // Dark blue-grey smoked glass. Alpha is deliberately far above the 15-20% from
        // the original brief: over passthrough the real room shows through the panel,
        // and anything lighter than this makes the text unreadable.
        Color smoke = new Color(0.055f, 0.075f, 0.105f);
        Color iceRim = new Color(1f, 1f, 1f, 1f);

        CreateOrReplaceMaterial("Assets/Materials/Holo/Mat_HoloPanel_Main.mat", glassShader,
            new Color(smoke.r, smoke.g, smoke.b, 0.55f), iceRim, 2.5f, 1.1f);
        sb.AppendLine("Mat_HoloPanel_Main -> smoked glass alpha 0.55, white rim 1.1");

        // Buttons sit on top of the main panel, so their alpha stacks with it - kept low
        // so they read as a subtle lift rather than a second solid slab.
        CreateOrReplaceMaterial("Assets/Materials/Holo/Mat_HoloPanel_Button.mat", glassShader,
            new Color(smoke.r, smoke.g, smoke.b, 0.30f), iceRim, 3f, 0.55f);
        sb.AppendLine("Mat_HoloPanel_Button -> smoked glass alpha 0.30, white rim 0.55");

        Material tmpMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_TMP_Holo.mat");
        if (tmpMat != null)
        {
            if (tmpMat.HasProperty("_FaceColor")) tmpMat.SetColor("_FaceColor", Color.white);
            if (tmpMat.HasProperty("_FaceDilate")) tmpMat.SetFloat("_FaceDilate", -0.15f);
            // The dark panel now supplies the contrast, so drop the tinted outline that
            // was muddying the glyph edges and keep only a soft shadow for passthrough.
            if (tmpMat.HasProperty("_OutlineWidth")) tmpMat.SetFloat("_OutlineWidth", 0f);
            if (tmpMat.HasProperty("_UnderlayColor"))
            {
                tmpMat.EnableKeyword("UNDERLAY_ON");
                tmpMat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.45f));
                tmpMat.SetFloat("_UnderlayOffsetX", 0.3f);
                tmpMat.SetFloat("_UnderlayOffsetY", -0.3f);
                tmpMat.SetFloat("_UnderlaySoftness", 0.25f);
            }
            EditorUtility.SetDirty(tmpMat);
            sb.AppendLine("Mat_TMP_Holo -> pure white face, no outline, soft shadow");
        }

        AssetDatabase.SaveAssets();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step7_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP7_DONE");
    }

    [MenuItem("MedicalViewer/Step8 - Fix Organ Scale + Distance")]
    public static void Step8_FixOrganPlacement()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject centerEye = Find("CenterEyeAnchor");
        if (centerEye == null)
        {
            Debug.LogError("STEP8_FAILED CenterEyeAnchor not found");
            return;
        }

        // Anatomically sensible sizes, measured across the model's largest dimension.
        // Slightly above life size so detail is readable at arm's length.
        PlaceOrgan("Heart", centerEye.transform, new Vector3(-0.35f, 1.15f, 1.2f), 0.18f, sb);
        PlaceOrgan("estomago_sin_render", centerEye.transform, new Vector3(0.35f, 1.15f, 1.2f), 0.28f, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step8_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP8_DONE");
    }

    private static void PlaceOrgan(string rootName, Transform eye, Vector3 localOffset, float targetMaxSize, StringBuilder sb)
    {
        GameObject root = Find(rootName);
        if (root == null)
        {
            sb.AppendLine($"[WARN] {rootName} not found");
            return;
        }

        if (!TryGetCombinedBounds(root.transform, out Bounds before))
        {
            sb.AppendLine($"[WARN] {rootName} has no renderers, skipped");
            return;
        }

        float distBefore = Vector3.Distance(before.center, eye.position);
        float maxBefore = Mathf.Max(before.size.x, before.size.y, before.size.z);
        int tris = CountTriangles(root.transform);

        sb.AppendLine($"--- {rootName} ---");
        sb.AppendLine($"BEFORE size={before.size:F3} (max {maxBefore:F3} m)  distance_from_eye={distBefore:F1} m  scale={root.transform.localScale:F3}  triangles={tris}");

        // Rescale so the largest dimension matches the anatomical target.
        if (maxBefore > 0.0001f)
        {
            float factor = targetMaxSize / maxBefore;
            root.transform.localScale *= factor;
        }

        // Reposition by BOUNDS CENTRE, not pivot: these FBX pivots sit far off the mesh,
        // so moving the transform alone would leave the model somewhere unexpected.
        TryGetCombinedBounds(root.transform, out Bounds scaled);
        Vector3 pivotToCentre = scaled.center - root.transform.position;
        Vector3 desiredCentre = eye.TransformPoint(localOffset);
        root.transform.position = desiredCentre - pivotToCentre;

        TryGetCombinedBounds(root.transform, out Bounds after);
        float distAfter = Vector3.Distance(after.center, eye.position);
        float maxAfter = Mathf.Max(after.size.x, after.size.y, after.size.z);

        sb.AppendLine($"AFTER  size={after.size:F3} (max {maxAfter:F3} m)  distance_from_eye={distAfter:F2} m  scale={root.transform.localScale:F5}");

        EditorUtility.SetDirty(root);
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return true;
    }

    private static int CountTriangles(Transform root)
    {
        int total = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh != null) total += mf.sharedMesh.triangles.Length / 3;
        }
        return total;
    }

    [MenuItem("MedicalViewer/Diagnose Organ Children")]
    public static void DiagnoseOrganChildren()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        foreach (var rootName in new[] { "Heart", "estomago_sin_render" })
        {
            GameObject root = Find(rootName);
            if (root == null)
            {
                sb.AppendLine($"[MISSING] {rootName}");
                continue;
            }

            sb.AppendLine($"=== {rootName} === world_pos={root.transform.position:F3} scale={root.transform.localScale:F5}");

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                var mf = t.GetComponent<MeshFilter>();

                string kind = r != null ? "RENDERER" : (t.GetComponent<Camera>() != null ? "CAMERA" : (t.GetComponent<Light>() != null ? "LIGHT" : "empty"));
                string mats = r != null ? string.Join("|", r.sharedMaterials.Select(m => m == null ? "NULL" : m.name)) : "-";
                string size = r != null ? $"{r.bounds.size:F3}" : "-";
                string centre = r != null ? $"{r.bounds.center:F3}" : $"{t.position:F3}";
                int tris = mf != null && mf.sharedMesh != null ? mf.sharedMesh.triangles.Length / 3 : 0;

                sb.AppendLine($"  [{kind}] {t.name} active={t.gameObject.activeSelf} centre={centre} size={size} tris={tris} mats=[{mats}]");
            }
            sb.AppendLine();
        }

        System.IO.File.WriteAllText(OutDir + "organ_children.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("DIAGNOSE_DONE");
    }

    [MenuItem("MedicalViewer/Step9 - Fix Stomach Structure")]
    public static void Step9_FixStomach()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject root = Find("estomago_sin_render");
        GameObject centerEye = Find("CenterEyeAnchor");
        if (root == null || centerEye == null)
        {
            Debug.LogError($"STEP9_FAILED root_missing={root == null} eye_missing={centerEye == null}");
            return;
        }

        Transform seg = FindChildByName(root.transform, "Segmentation");
        Transform segDup = FindChildByName(root.transform, "Segmentation.001");
        Transform cube = FindChildByName(root.transform, "Cube");
        Transform strayCam = FindChildByName(root.transform, "Camera");
        Transform strayLight = FindChildByName(root.transform, "Light");

        if (seg == null)
        {
            Debug.LogError("STEP9_FAILED Segmentation mesh not found");
            return;
        }

        // 1. Kill the z-fighting: the duplicate sits at the exact same position.
        foreach (var (t, why) in new[] { (segDup, "duplicate mesh, z-fighting"), (strayCam, "stray camera from FBX"), (strayLight, "stray light from FBX") })
        {
            if (t != null)
            {
                t.gameObject.SetActive(false);
                sb.AppendLine($"DISABLED {t.name} ({why})");
            }
        }

        // 2. Undo my Step 4 mistake: interactivity was bound to a 1mm junk cube.
        if (cube != null)
        {
            RemoveIfPresent<OrganSelectionInfo>(cube.gameObject, sb);
            RemoveIfPresent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>(cube.gameObject, sb);
            RemoveIfPresent<MeshCollider>(cube.gameObject, sb);

            if (cube.TryGetComponent(out Renderer cubeRenderer))
            {
                var kept = cubeRenderer.sharedMaterials
                    .Where(m => m == null || m.shader == null || m.shader.name != "MedicalViewer/HoloOutline")
                    .ToArray();
                cubeRenderer.sharedMaterials = kept;
            }

            cube.gameObject.SetActive(false);
            sb.AppendLine("DISABLED Cube (1mm junk mesh, stripped my components + outline material)");
        }

        // 3. Put the interactivity on the mesh people can actually see and point at.
        GameObject host = seg.gameObject;
        var mf = host.GetComponent<MeshFilter>();

        if (host.GetComponent<MeshCollider>() == null)
        {
            var mc = Undo.AddComponent<MeshCollider>(host);
            if (mf != null) mc.sharedMesh = mf.sharedMesh;
            mc.convex = false;
            sb.AppendLine("Segmentation -> MeshCollider added");
        }

        if (host.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>() == null)
        {
            Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>(host);
            sb.AppendLine("Segmentation -> XRSimpleInteractable added");
        }

        var info = host.GetComponent<OrganSelectionInfo>();
        if (info == null)
        {
            info = Undo.AddComponent<OrganSelectionInfo>(host);
            sb.AppendLine("Segmentation -> OrganSelectionInfo added");
        }

        var so = new SerializedObject(info);
        so.FindProperty("organName").stringValue = "ESTÓMAGO";
        so.FindProperty("organInfo").stringValue = "ESTÓMAGO\n(Placeholder - completar con datos médicos reales.)";
        so.ApplyModifiedPropertiesWithoutUndo();

        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloOutline.mat");
        if (outlineMat != null && host.TryGetComponent(out Renderer segRenderer))
        {
            if (!segRenderer.sharedMaterials.Any(m => m != null && m.shader != null && m.shader.name == "MedicalViewer/HoloOutline"))
            {
                var list = segRenderer.sharedMaterials.ToList();
                list.Add(outlineMat);
                segRenderer.sharedMaterials = list.ToArray();
                sb.AppendLine($"Segmentation -> outline material appended (slots={list.Count})");
            }
        }

        // 4. Resize using ONLY the real mesh - the previous pass measured bounds that
        //    were inflated by the stray camera/light sitting far from the model.
        Renderer r = host.GetComponent<Renderer>();
        float maxBefore = Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z);
        sb.AppendLine($"BEFORE stomach real size={r.bounds.size:F3} (max {maxBefore:F3} m)");

        const float targetMax = 0.28f;
        if (maxBefore > 0.00001f)
        {
            root.transform.localScale *= targetMax / maxBefore;
        }

        Vector3 desiredCentre = centerEye.transform.TransformPoint(new Vector3(0.35f, 1.15f, 1.2f));
        Vector3 pivotToCentre = r.bounds.center - root.transform.position;
        root.transform.position = desiredCentre - pivotToCentre;

        sb.AppendLine($"AFTER  stomach real size={r.bounds.size:F3} (max {Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z):F3} m) centre={r.bounds.center:F3} scale={root.transform.localScale:F5}");

        EditorUtility.SetDirty(root);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step9_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP9_DONE");
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private static void RemoveIfPresent<T>(GameObject go, StringBuilder sb) where T : Component
    {
        var c = go.GetComponent<T>();
        if (c != null)
        {
            Undo.DestroyObjectImmediate(c);
            sb.AppendLine($"  removed {typeof(T).Name} from {go.name}");
        }
    }

    [MenuItem("MedicalViewer/Step10 - Wire Menu Views + Toggle")]
    public static void Step10_WireMenuViews()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var actions = Object.FindObjectOfType<MedicalMenuActions>();
        if (actions == null)
        {
            Debug.LogError("STEP10_FAILED MedicalMenuActions not in scene (run Step5 first)");
            return;
        }

        GameObject menu = Find("Medical_Menu");
        GameObject heart = Find("Heart");
        GameObject stomach = Find("estomago_sin_render");
        GameObject ndiScreen = Find("NDI_Screen");

        var so = new SerializedObject(actions);
        so.FindProperty("menuRoot").objectReferenceValue = menu;

        AssignArray(so, "model3DObjects", new[] { heart, stomach }, sb);
        AssignArray(so, "dicomObjects", new[] { ndiScreen }, sb);
        // Left empty on purpose: the FBX duplicate turned out to be junk, so there is no
        // real segmentation layer yet. The button warns instead of hiding everything.
        AssignArray(so, "segmentationObjects", new GameObject[0], sb);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(actions);
        sb.AppendLine($"MedicalMenuActions wired on {actions.gameObject.name} (menuRoot={(menu != null ? menu.name : "NULL")})");

        // The toggle has to live on an always-active object, not on the menu itself.
        GameObject toggleHost = actions.gameObject;
        var toggle = toggleHost.GetComponent<MenuToggleInput>();
        if (toggle == null)
        {
            toggle = Undo.AddComponent<MenuToggleInput>(toggleHost);
            sb.AppendLine($"{toggleHost.name} -> MenuToggleInput added");
        }

        var tso = new SerializedObject(toggle);
        tso.FindProperty("menuRoot").objectReferenceValue = menu;
        tso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(toggle);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step10_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP10_DONE");
    }

    private static void AssignArray(SerializedObject so, string propertyName, GameObject[] values, StringBuilder sb)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            sb.AppendLine($"[WARN] property {propertyName} not found");
            return;
        }

        var valid = values.Where(v => v != null).ToArray();
        prop.arraySize = valid.Length;
        for (int i = 0; i < valid.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = valid[i];
        }

        sb.AppendLine($"  {propertyName} = [{string.Join(", ", valid.Select(v => v.name))}]");
    }

    [MenuItem("MedicalViewer/Step11 - Holographic Platforms")]
    public static void Step11_OrganPlatforms()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        Shader platformShader = Shader.Find("MedicalViewer/HoloPlatform");
        if (platformShader == null)
        {
            Debug.LogError("STEP11_FAILED platform shader missing");
            return;
        }

        Material platformMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Holo/Mat_HoloPlatform.mat");
        if (platformMat == null)
        {
            platformMat = new Material(platformShader);
            AssetDatabase.CreateAsset(platformMat, "Assets/Materials/Holo/Mat_HoloPlatform.mat");
        }
        platformMat.shader = platformShader;
        platformMat.SetColor("_BaseColor", new Color(0.78f, 0.92f, 1f, 0.55f));
        platformMat.SetFloat("_RingRadius", 0.78f);
        platformMat.SetFloat("_RingWidth", 0.09f);
        platformMat.SetFloat("_FillAlpha", 0.12f);
        EditorUtility.SetDirty(platformMat);

        AddPlatform("Heart", platformMat, sb);
        AddPlatform("estomago_sin_render", platformMat, sb);

        AssetDatabase.SaveAssets();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step11_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP11_DONE");
    }

    private static void AddPlatform(string organName, Material platformMat, StringBuilder sb)
    {
        GameObject organ = Find(organName);
        if (organ == null)
        {
            sb.AppendLine($"[WARN] {organName} not found");
            return;
        }

        string platformName = $"Holo_Platform_{organName}";
        Transform existing = FindChildByName(organ.transform, platformName);
        if (existing != null)
        {
            sb.AppendLine($"{platformName} already exists, skipped");
            return;
        }

        if (!TryGetCombinedBounds(organ.transform, out Bounds bounds))
        {
            sb.AppendLine($"[WARN] {organName} has no active renderers");
            return;
        }

        GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Quad);
        platform.name = platformName;
        Object.DestroyImmediate(platform.GetComponent<Collider>()); // must never block the interaction ray

        if (platform.TryGetComponent(out Renderer pr))
        {
            pr.sharedMaterial = platformMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
        }

        // Parented to the organ so it shows/hides with it when the menu switches views.
        platform.transform.SetParent(organ.transform, false);

        // World-space placement first, then counter the organ's own scale (5.6x on Heart,
        // 0.002x on the stomach) so the disc ends up the size we actually want.
        platform.transform.position = new Vector3(bounds.center.x, bounds.min.y - 0.015f, bounds.center.z);
        platform.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, facing up

        float diameter = Mathf.Max(bounds.size.x, bounds.size.z) * 2.2f;
        float parentScale = organ.transform.lossyScale.x;
        if (Mathf.Abs(parentScale) < 1e-8f) parentScale = 1f;
        float local = diameter / parentScale;
        platform.transform.localScale = new Vector3(local, local, 1f);

        sb.AppendLine($"{organName} -> {platformName} added (diameter={diameter:F3} m, y={bounds.min.y - 0.015f:F3})");
    }

    [MenuItem("MedicalViewer/Step12 - Visible Controllers")]
    public static void Step12_VisibleControllers()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rig = Find("[BuildingBlock] Camera Rig");
        if (rig == null)
        {
            Debug.LogError("STEP12_FAILED camera rig not found");
            return;
        }

        const string prefabPath = "Packages/com.meta.xr.sdk.core/Prefabs/OVRControllerPrefab.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"STEP12_FAILED prefab not found at {prefabPath}");
            return;
        }

        // Report where the interaction rays actually live, so we can confirm the visible
        // controller model ends up in the same place the ray shoots from.
        foreach (var ray in Object.FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>(true))
        {
            sb.AppendLine($"[RAY] {GetPath(ray.transform)}");
        }

        AttachController(rig.transform, "LeftControllerAnchor", OVRInput.Controller.LTouch, prefab, sb);
        AttachController(rig.transform, "RightControllerAnchor", OVRInput.Controller.RTouch, prefab, sb);

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step12_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP12_DONE");
    }

    private static void AttachController(Transform rig, string anchorName, OVRInput.Controller side, GameObject prefab, StringBuilder sb)
    {
        Transform anchor = FindChildByName(rig, anchorName);
        if (anchor == null)
        {
            sb.AppendLine($"[WARN] {anchorName} not found under the rig");
            return;
        }

        sb.AppendLine($"[ANCHOR] {anchorName} -> {GetPath(anchor)}");

        if (anchor.GetComponentInChildren<OVRControllerHelper>(true) != null)
        {
            sb.AppendLine($"  {anchorName} already has a controller model, skipped");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
        instance.name = $"ControllerModel_{side}";
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;

        var helper = instance.GetComponent<OVRControllerHelper>();
        if (helper != null)
        {
            helper.m_controller = side; // Quest 3 uses the Meta Touch Plus models
            EditorUtility.SetDirty(helper);
        }

        Undo.RegisterCreatedObjectUndo(instance, "Add controller model");
        sb.AppendLine($"  {anchorName} -> {instance.name} added ({side})");
    }

    [MenuItem("MedicalViewer/Diagnose Rig Interaction")]
    public static void DiagnoseRigInteraction()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rig = Find("[BuildingBlock] Camera Rig");
        if (rig == null)
        {
            Debug.LogError("DIAG_FAILED rig not found");
            return;
        }

        sb.AppendLine("=== Full rig hierarchy ===");
        DumpHierarchy(rig.transform, 0, sb);

        sb.AppendLine();
        sb.AppendLine("=== Components on the interaction-relevant objects ===");
        foreach (var name in new[] { "LeftHandAnchor", "RightHandAnchor", "LeftControllerAnchor", "RightControllerAnchor", "RightEyeAnchor", "LeftEyeAnchor", "CenterEyeAnchor" })
        {
            Transform t = FindChildByName(rig.transform, name);
            if (t == null)
            {
                sb.AppendLine($"[MISSING] {name}");
                continue;
            }

            sb.AppendLine($"--- {GetPath(t)}  localPos={t.localPosition:F3} localRot={t.localEulerAngles:F1}");
            foreach (var c in t.GetComponents<Component>())
            {
                sb.AppendLine($"      {(c == null ? "MISSING SCRIPT" : c.GetType().FullName)}");
            }
        }

        System.IO.File.WriteAllText(OutDir + "rig_interaction.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("DIAG_RIG_DONE");
    }

    private static void DumpHierarchy(Transform t, int depth, StringBuilder sb)
    {
        sb.AppendLine($"{new string(' ', depth * 2)}{t.name}");
        foreach (Transform child in t)
        {
            DumpHierarchy(child, depth + 1, sb);
        }
    }

    private const string ActionsAssetPath = "Assets/Samples/XR Interaction Toolkit/2.6.5/Starter Assets/XRI Default Input Actions.inputactions";

    [MenuItem("MedicalViewer/Step13 - Repair Rig + Wire XRI Input")]
    public static void Step13_RepairRigAndInput()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        var actionRefs = AssetDatabase.LoadAllAssetsAtPath(ActionsAssetPath)
            .OfType<UnityEngine.InputSystem.InputActionReference>()
            .ToArray();

        if (actionRefs.Length == 0)
        {
            Debug.LogError($"STEP13_FAILED no InputActionReferences at {ActionsAssetPath}");
            return;
        }
        sb.AppendLine($"loaded {actionRefs.Length} input action references");

        GameObject rig = Find("[BuildingBlock] Camera Rig");
        Transform trackingSpace = FindChildByName(rig.transform, "TrackingSpace");

        // Transform.Find only looks at DIRECT children - exactly what OVRCameraRig does at
        // runtime, so this resolves the same anchors Unity will actually drive.
        Transform leftHand = trackingSpace.Find("LeftHandAnchor");
        Transform rightHand = trackingSpace.Find("RightHandAnchor");
        Transform leftCtrl = leftHand != null ? leftHand.Find("LeftControllerAnchor") : null;
        Transform rightCtrl = rightHand != null ? rightHand.Find("RightControllerAnchor") : null;

        if (leftCtrl == null || rightCtrl == null)
        {
            Debug.LogError($"STEP13_FAILED leftCtrl={leftCtrl} rightCtrl={rightCtrl}");
            return;
        }

        sb.AppendLine($"[TARGET L] {GetPath(leftCtrl)}");
        sb.AppendLine($"[TARGET R] {GetPath(rightCtrl)}");

        // The orphan duplicate: RightHandAnchor nested under LeftHandAnchor. OVRCameraRig
        // never drives it, so everything on it would ride the LEFT hand instead.
        Transform orphan = leftHand != null ? leftHand.Find("RightHandAnchor") : null;
        if (orphan != null)
        {
            Transform orphanCtrl = orphan.Find("RightControllerAnchor");
            if (orphanCtrl != null)
            {
                // Rescue the controller model that Step12 put on the wrong anchor.
                Transform model = FindChildByName(orphanCtrl, "ControllerModel_RTouch");
                if (model != null)
                {
                    model.SetParent(rightCtrl, false);
                    model.localPosition = Vector3.zero;
                    model.localRotation = Quaternion.identity;
                    sb.AppendLine("moved ControllerModel_RTouch to the real RightControllerAnchor");
                }
            }

            orphan.gameObject.SetActive(false);
            orphan.name = "RightHandAnchor_ORPHAN_DISABLED";
            sb.AppendLine("DISABLED orphan LeftHandAnchor/RightHandAnchor (renamed so OVRCameraRig can't bind it)");
        }

        // A ray shooting out of the user's eyeball helps nobody.
        Transform rightEye = trackingSpace.Find("RightEyeAnchor");
        if (rightEye != null)
        {
            RemoveIfPresent<UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual>(rightEye.gameObject, sb);
            RemoveIfPresent<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>(rightEye.gameObject, sb);
            RemoveIfPresent<LineRenderer>(rightEye.gameObject, sb);
        }

        SetupControllerRay(leftCtrl.gameObject, "XRI LeftHand", "XRI LeftHand Interaction", actionRefs, sb);
        SetupControllerRay(rightCtrl.gameObject, "XRI RightHand", "XRI RightHand Interaction", actionRefs, sb);

        // Without an InputActionManager nothing enables the action maps, so every action
        // stays silent even when correctly referenced.
        var manager = Object.FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>();
        if (manager == null)
        {
            GameObject host = Find("XR Interaction Manager");
            if (host == null) host = rig;

            manager = Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(host);
            sb.AppendLine($"{host.name} -> InputActionManager added");
        }

        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(ActionsAssetPath);
        var mso = new SerializedObject(manager);
        var listProp = mso.FindProperty("m_ActionAssets");
        listProp.arraySize = 1;
        listProp.GetArrayElementAtIndex(0).objectReferenceValue = asset;
        mso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(manager);
        sb.AppendLine("InputActionManager -> XRI Default Input Actions assigned");

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step13_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP13_DONE");
    }

    private static void SetupControllerRay(GameObject go, string trackingMap, string interactionMap,
        UnityEngine.InputSystem.InputActionReference[] refs, StringBuilder sb)
    {
        var controller = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(go);
            sb.AppendLine($"{go.name} -> ActionBasedController added");
        }

        var so = new SerializedObject(controller);
        BindAction(so, "m_PositionAction", refs, trackingMap, "Position", sb);
        BindAction(so, "m_RotationAction", refs, trackingMap, "Rotation", sb);
        BindAction(so, "m_IsTrackedAction", refs, trackingMap, "Is Tracked", sb);
        BindAction(so, "m_TrackingStateAction", refs, trackingMap, "Tracking State", sb);
        BindAction(so, "m_SelectAction", refs, interactionMap, "Select", sb);
        BindAction(so, "m_SelectActionValue", refs, interactionMap, "Select Value", sb);
        BindAction(so, "m_ActivateAction", refs, interactionMap, "Activate", sb);
        BindAction(so, "m_ActivateActionValue", refs, interactionMap, "Activate Value", sb);
        BindAction(so, "m_UIPressAction", refs, interactionMap, "UI Press", sb);
        BindAction(so, "m_UIPressActionValue", refs, interactionMap, "UI Press Value", sb);
        BindAction(so, "m_HapticDeviceAction", refs, interactionMap, "Haptic Device", sb);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);

        if (go.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>() == null)
        {
            Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRRayInteractor>(go);
            sb.AppendLine($"{go.name} -> XRRayInteractor added");
        }

        var line = go.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = Undo.AddComponent<LineRenderer>(go);
            line.widthMultiplier = 0.005f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            sb.AppendLine($"{go.name} -> LineRenderer added");
        }

        if (go.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual>() == null)
        {
            Undo.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual>(go);
            sb.AppendLine($"{go.name} -> XRInteractorLineVisual added");
        }
    }

    private static void BindAction(SerializedObject so, string propertyName,
        UnityEngine.InputSystem.InputActionReference[] refs, string mapName, string actionName, StringBuilder sb)
    {
        var reference = refs.FirstOrDefault(r =>
            r != null && r.action != null &&
            r.action.actionMap != null &&
            r.action.actionMap.name == mapName &&
            r.action.name == actionName);

        if (reference == null)
        {
            sb.AppendLine($"  [WARN] no reference for {mapName}/{actionName}");
            return;
        }

        var prop = so.FindProperty(propertyName);
        prop.FindPropertyRelative("m_UseReference").boolValue = true;
        prop.FindPropertyRelative("m_Reference").objectReferenceValue = reference;
    }

    [MenuItem("MedicalViewer/Step14 - Editor Eye Height Preview")]
    public static void Step14_EditorEyeHeight()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject rig = Find("[BuildingBlock] Camera Rig");
        if (rig == null)
        {
            Debug.LogError("STEP14_FAILED camera rig not found");
            return;
        }

        if (rig.GetComponent<EditorEyeHeightSimulator>() == null)
        {
            Undo.AddComponent<EditorEyeHeightSimulator>(rig);
            sb.AppendLine($"{rig.name} -> EditorEyeHeightSimulator added (editor-only, 1.6 m)");
        }
        else
        {
            sb.AppendLine("EditorEyeHeightSimulator already present");
        }

        // Also finish the one binding Step13 missed: Haptic Device lives in the tracking
        // map ("XRI LeftHand"), not the interaction map.
        var actionRefs = AssetDatabase.LoadAllAssetsAtPath(ActionsAssetPath)
            .OfType<UnityEngine.InputSystem.InputActionReference>()
            .ToArray();

        Transform trackingSpace = FindChildByName(rig.transform, "TrackingSpace");
        BindHaptics(trackingSpace, "LeftHandAnchor", "LeftControllerAnchor", "XRI LeftHand", actionRefs, sb);
        BindHaptics(trackingSpace, "RightHandAnchor", "RightControllerAnchor", "XRI RightHand", actionRefs, sb);

        EditorUtility.SetDirty(rig);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step14_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP14_DONE");
    }

    private static void BindHaptics(Transform trackingSpace, string handAnchor, string controllerAnchor,
        string trackingMap, UnityEngine.InputSystem.InputActionReference[] refs, StringBuilder sb)
    {
        Transform hand = trackingSpace.Find(handAnchor);
        Transform ctrl = hand != null ? hand.Find(controllerAnchor) : null;
        if (ctrl == null)
        {
            sb.AppendLine($"[WARN] {handAnchor}/{controllerAnchor} not found");
            return;
        }

        var controller = ctrl.GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
        if (controller == null)
        {
            sb.AppendLine($"[WARN] no ActionBasedController on {controllerAnchor}");
            return;
        }

        var so = new SerializedObject(controller);
        BindAction(so, "m_HapticDeviceAction", refs, trackingMap, "Haptic Device", sb);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        sb.AppendLine($"{controllerAnchor} -> Haptic Device bound from {trackingMap}");
    }

    [MenuItem("MedicalViewer/Step15 - Resize Menu For Comfort")]
    public static void Step15_ResizeMenu()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject menu = Find("Medical_Menu");
        GameObject centerEye = Find("CenterEyeAnchor");
        if (menu == null || centerEye == null)
        {
            Debug.LogError("STEP15_FAILED menu or CenterEyeAnchor missing");
            return;
        }

        Transform eye = centerEye.transform;

        if (!TryGetCombinedBounds(menu.transform, out Bounds before))
        {
            Debug.LogError("STEP15_FAILED menu has no renderers");
            return;
        }

        // Angular size is what actually decides whether VR UI feels close or far away.
        float distBefore = Vector3.Distance(before.center, eye.position);
        float widthBefore = before.size.x;
        float angleBefore = 2f * Mathf.Atan2(widthBefore * 0.5f, distBefore) * Mathf.Rad2Deg;

        sb.AppendLine($"BEFORE width={widthBefore:F3} m  height={before.size.y:F3} m  distance={distBefore:F2} m  angular_width={angleBefore:F1} deg  scale={menu.transform.localScale:F3}");

        // Target: ~1.1 m wide seen from 1.8 m ~= 34 deg, the comfortable band for a
        // main menu in VR (wide enough to read, small enough not to need head turns).
        const float targetDistance = 1.8f;
        const float targetAngularWidth = 34f;
        float targetWidth = 2f * targetDistance * Mathf.Tan(targetAngularWidth * 0.5f * Mathf.Deg2Rad);

        float factor = targetWidth / widthBefore;
        menu.transform.localScale *= factor;

        // Re-place by bounds centre so the panel stays centred on the sight line after scaling.
        TryGetCombinedBounds(menu.transform, out Bounds scaled);
        Vector3 pivotToCentre = scaled.center - menu.transform.position;
        Vector3 desiredCentre = eye.TransformPoint(new Vector3(0f, 1.5f, targetDistance));
        menu.transform.position = desiredCentre - pivotToCentre;

        TryGetCombinedBounds(menu.transform, out Bounds after);
        float distAfter = Vector3.Distance(after.center, eye.position);
        float angleAfter = 2f * Mathf.Atan2(after.size.x * 0.5f, distAfter) * Mathf.Rad2Deg;

        sb.AppendLine($"AFTER  width={after.size.x:F3} m  height={after.size.y:F3} m  distance={distAfter:F2} m  angular_width={angleAfter:F1} deg  scale={menu.transform.localScale:F4}");

        EditorUtility.SetDirty(menu);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine($"scene_saved={EditorSceneManager.SaveScene(scene)}");

        System.IO.File.WriteAllText(OutDir + "step15_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP15_DONE");
    }

    private static Material CreateOrReplaceMaterial(string path, Shader shader, Color baseColor, Color rimColor, float rimPower, float rimIntensity)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_RimColor", rimColor);
        mat.SetFloat("_RimPower", rimPower);
        mat.SetFloat("_RimIntensity", rimIntensity);
        mat.SetFloat("_HoverGlow", 0f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material CreateOrReplaceTmpHoloMaterial(string path)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            // Base it on whatever material the scene's TMP text objects are already using
            // so the shader/font atlas reference stays correct.
            TMP_Text sample = Object.FindObjectOfType<TMP_Text>();
            Material baseMat = sample != null ? sample.fontSharedMaterial : null;
            if (baseMat == null)
            {
                Debug.LogWarning("STEP2_WARN no TMP_Text found in scene to source a base font material from");
                return null;
            }

            mat = new Material(baseMat);
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_FaceColor")) mat.SetColor("_FaceColor", new Color(0.88f, 0.99f, 1f, 1f));
        if (mat.HasProperty("_FaceDilate")) mat.SetFloat("_FaceDilate", -0.3f);
        if (mat.HasProperty("_OutlineColor")) mat.SetColor("_OutlineColor", new Color(0.4f, 0.9f, 1f, 1f));
        if (mat.HasProperty("_OutlineWidth")) mat.SetFloat("_OutlineWidth", 0.05f);
        if (mat.HasProperty("_GlowColor")) mat.SetColor("_GlowColor", new Color(0.5f, 0.95f, 1f, 1f));
        if (mat.HasProperty("_GlowPower")) mat.SetFloat("_GlowPower", 0.35f);
        if (mat.HasProperty("_GlowOuter")) mat.SetFloat("_GlowOuter", 0.35f);

        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void ApplyHoloTypography(GameObject go, Material tmpHoloMat, bool uppercase, StringBuilder sb)
    {
        if (go == null)
        {
            sb.AppendLine("[WARN] typography target missing (null GameObject)");
            return;
        }

        if (!go.TryGetComponent(out TMP_Text tmp))
        {
            sb.AppendLine($"[WARN] {go.name} has no TMP_Text component");
            return;
        }

        if (tmpHoloMat != null)
        {
            tmp.fontSharedMaterial = tmpHoloMat;
        }

        tmp.fontStyle = FontStyles.Normal; // remove bold/italic for a thin, clean look
        tmp.characterSpacing = 10f;

        if (uppercase)
        {
            tmp.text = tmp.text.ToUpperInvariant();
        }

        EditorUtility.SetDirty(go);
        sb.AppendLine($"{go.name} -> typography updated (uppercase={uppercase})");
    }

    private static void DumpTreeIfMatches(Transform t, StringBuilder sb)
    {
        string lname = t.name.ToLowerInvariant();
        if (lname.Contains("heart") || lname.Contains("estomago") || lname.Contains("corazon") || lname.Contains("stomach"))
        {
            sb.AppendLine($"[MODEL CANDIDATE] {GetPath(t)}");
            sb.Append(Describe(t));
        }
        foreach (Transform child in t)
        {
            DumpTreeIfMatches(child, sb);
        }
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        Transform p = t.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }
}
