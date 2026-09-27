using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Repinta toda la interfaz como vidrio oscuro, ahora que por detrás se ve la habitación.
///
/// Por qué así, y no a ojo:
///
/// - Meta dice que en un visor no se usen ni el blanco ni el negro puros, porque cansan la
///   vista: los fondos claros no deben pasar de #DADADA y los oscuros no deben bajar de
///   #1A1A1A. Las tarjetas estaban en #F4F5F7, mucho más brillantes que ese tope, y eso es
///   media pantalla de blanco a medio metro de los ojos.
/// - Apple, para gafas que dejan ver el mundo real, recomienda material oscuro traslúcido
///   con texto blanco, porque el fondo cambia todo el rato y el texto blanco es lo que
///   aguanta cualquier iluminación. Y avisa de no apilar capas translúcidas, que se pierde
///   contraste; por eso aquí sólo la tarjeta es traslúcida y lo de dentro va opaco sobre ella.
/// - Además, las imágenes médicas se miran sobre fondo oscuro, no sobre blanco: sobre blanco
///   los grises del TAC se aplastan y se pierden lesiones.
///
/// Lo que NO se toca: las imágenes del TAC, las miniaturas de los órganos y los puntos de
/// color de cada órgano, que significan algo.
/// </summary>
public static class MedicalDarkGlass
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string OutDir = "Logs/EditorAutomation/";

    // ---- paleta ----
    // Vidrio de la tarjeta: #1E2228 al 88%. Por encima del #1A1A1A que marca Meta como
    // limite, y con algo de transparencia para que se intuya la sala por detras.
    private static readonly Color Surface = new Color32(0x1E, 0x22, 0x28, 224);

    // Elementos de dentro: blanco muy bajito. Al ir sobre la tarjeta y no sobre la sala,
    // no cuenta como apilar traslucidos.
    private static readonly Color Raised = new Color(1f, 1f, 1f, 0.10f);
    private static readonly Color Control = new Color(1f, 1f, 1f, 0.18f);
    private static readonly Color Divider = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color Rim = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color Shadow = new Color32(0x05, 0x07, 0x0B, 150);

    // Azul mas claro que el de antes: el #176BE0 sobre fondo oscuro se apagaba.
    private static readonly Color Accent = new Color32(0x4C, 0x8D, 0xFF, 255);

    private static readonly Color TextPrimary = new Color32(0xF0, 0xF2, 0xF5, 255);
    private static readonly Color TextSecondary = new Color32(0xA8, 0xB1, 0xBC, 255);
    private static readonly Color TextOnAccent = Color.white;

    // ---- colores que habia ----
    private static readonly Color OldCardTint = new Color(1f, 1f, 1f, 1f);
    private static readonly Color OldRaised = new Color32(0xF4, 0xF5, 0xF7, 255);
    private static readonly Color OldTextPrimary = new Color32(0x21, 0x26, 0x2E, 255);
    private static readonly Color OldTextSecondary = new Color32(0x8C, 0x94, 0x9E, 255);
    private static readonly Color OldAccent = new Color32(0x17, 0x6B, 0xE0, 255);

    [MenuItem("MedicalViewer/Step83 - Repintar la interfaz como vidrio oscuro")]
    public static void Apply()
    {
        if (!Application.isBatchMode &&
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.OpenScene(ScenePath);
        var sb = new StringBuilder();

        GameObject root = GameObject.Find("Medical_Menu_UI");
        if (root == null)
        {
            Debug.LogError("[Step83] No encuentro Medical_Menu_UI.");
            return;
        }

        var counts = new Dictionary<string, int>();
        var cards = new List<Image>();

        foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
        {
            if (graphic is RawImage) { Count(counts, "imagen de TAC o miniatura: intacta"); continue; }

            if (graphic is TMP_Text text) { Paint(text, counts); continue; }
            if (graphic is Image image) { Paint(image, counts, cards); }
        }

        foreach (var card in cards) RemoveRim(card, counts);

        RepaintScripts(sb);

        sb.AppendLine("=== que se ha cambiado ===");
        foreach (var pair in counts) sb.AppendLine(string.Format("{0,4}  {1}", pair.Value, pair.Key));

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        sb.AppendLine("scene_saved=" + EditorSceneManager.SaveScene(scene));

        AssetDatabase.SaveAssets();   // sin esto los cambios de material se pierden

        System.IO.Directory.CreateDirectory(OutDir);
        System.IO.File.WriteAllText(OutDir + "step83_output.txt", sb.ToString());
        Debug.Log(sb.ToString());
        Debug.Log("STEP83_DONE");
    }

    private static void Paint(Image image, Dictionary<string, int> counts, List<Image> cards)
    {
        string name = image.name;
        string path = Path(image.transform);

        // Lo que significa algo se queda como esta.
        if (image.sprite != null && image.sprite.name.StartsWith("Thumb"))
        { Count(counts, "miniatura de organo: intacta"); return; }

        if (name == "Dot") { Count(counts, "punto de color del organo: intacto"); return; }
        if (image.color.a < 0.01f) { Count(counts, "zona invisible de pulsacion: intacta"); return; }

        if (name == "Shadow") { Set(image, Shadow); Count(counts, "sombra reforzada"); return; }

        if (name.StartsWith("Card"))
        {
            Set(image, Surface);
            cards.Add(image);
            Count(counts, "tarjeta a vidrio oscuro");
            return;
        }

        if (name == "Divider") { Set(image, Divider); Count(counts, "separador"); return; }

        // El respaldo de la radiografia ya era oscuro: es justo donde tiene que ir oscuro.
        if (name == "Film") { Count(counts, "respaldo del TAC: ya era oscuro"); return; }

        if (path.Contains("Slider/Fill")) { Set(image, Accent); Count(counts, "relleno de barra"); return; }
        if (path.Contains("Slider/Background")) { Set(image, Control); Count(counts, "fondo de barra"); return; }

        // El circulo blanco del interruptor se queda blanco: es el que se ve moverse.
        if (image.sprite != null && image.sprite.name == "Knob" && name != "Toggle")
        { Count(counts, "bolita del interruptor: intacta"); return; }

        if (name == "Toggle") { Set(image, Control); Count(counts, "interruptor"); return; }

        if (Close(image.color, OldAccent)) { Set(image, Accent); Count(counts, "azul aclarado"); return; }

        if (Close(image.color, OldRaised) || Close(image.color, OldCardTint))
        { Set(image, Raised); Count(counts, "pastilla o hueco claro"); return; }

        Count(counts, "sin regla, intacto: " + name);
    }

    private static void Paint(TMP_Text text, Dictionary<string, int> counts)
    {
        if (Close(text.color, OldTextPrimary)) { Set(text, TextPrimary); Count(counts, "texto principal a claro"); return; }
        if (Close(text.color, OldTextSecondary)) { Set(text, TextSecondary); Count(counts, "texto secundario"); return; }
        if (Close(text.color, OldAccent)) { Set(text, new Color(0.42f, 0.64f, 1f, 1f)); Count(counts, "enlace azul"); return; }
        if (text.color.r > 0.9f && text.color.g > 0.9f && text.color.b > 0.9f)
        { Count(counts, "texto ya blanco: intacto"); return; }

        // Cualquier otro texto oscuro se aclara: sobre vidrio oscuro no se leeria.
        float luminance = text.color.r * 0.2126f + text.color.g * 0.7152f + text.color.b * 0.0722f;
        if (luminance < 0.45f) { Set(text, TextPrimary); Count(counts, "texto oscuro suelto a claro"); return; }

        Count(counts, "texto sin regla: " + text.name);
    }

    /// <summary>
    /// Deja la pantalla oscura mientras no hay señal, pero con un material propio.
    ///
    /// La pantalla venia con el material "Lit" por defecto de URP, que no es un asset del
    /// proyecto sino del paquete y esta compartido con todo lo que no tenga material propio.
    /// Tintarlo habria oscurecido media escena, asi que se devuelve a blanco y se le crea a
    /// la pantalla uno suyo.
    /// </summary>
    private static void DarkenScreen(Renderer renderer, StringBuilder sb)
    {
        var idle = (Color)new Color32(0x12, 0x16, 0x1C, 255);
        Material current = renderer.sharedMaterial;

        if (current != null)
        {
            string path = AssetDatabase.GetAssetPath(current);
            bool mine = !string.IsNullOrEmpty(path) && path.StartsWith("Assets/");

            if (!mine)
            {
                // Reparar el estropicio: el material compartido vuelve a blanco.
                current.SetColor("_BaseColor", Color.white);
                current.SetColor("_Color", Color.white);
                EditorUtility.SetDirty(current);
                sb.AppendLine("material compartido '" + current.name + "' devuelto a blanco");
            }
            else
            {
                current.SetColor("_BaseColor", idle);
                current.SetColor("_Color", idle);
                EditorUtility.SetDirty(current);
                sb.AppendLine("pantalla: material propio '" + current.name + "' -> oscuro");
                return;
            }
        }

        const string dir = "Assets/Materials/NDI";
        System.IO.Directory.CreateDirectory(dir);
        string own = dir + "/Mat_NDI_Screen.mat";

        var material = AssetDatabase.LoadAssetAtPath<Material>(own);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, own);
        }

        material.SetColor("_BaseColor", idle);
        material.SetColor("_Color", idle);
        EditorUtility.SetDirty(material);

        renderer.sharedMaterial = material;
        EditorUtility.SetDirty(renderer);
        sb.AppendLine("pantalla: material propio creado en " + own);
    }

    /// <summary>
    /// Quita el filo que se probo alrededor de las tarjetas. La idea era despegarlas de la
    /// habitacion, pero las tarjetas que se estiran o crecen solas no comparten medidas con
    /// el, y salia como una losa gris por detras. La separacion la da la sombra, que ya esta.
    /// </summary>
    private static void RemoveRim(Image card, Dictionary<string, int> counts)
    {
        Transform parent = card.transform.parent;
        if (parent == null) return;

        Transform rim = parent.Find("Rim");
        if (rim == null) return;

        Object.DestroyImmediate(rim.gameObject);
        Count(counts, "filo retirado (salia como una losa)");
    }

    /// <summary>
    /// Los colores que guardan los scripts y que se aplican al pulsar. Sin esto, el primer
    /// clic devolveria las pastillas al blanco de antes.
    /// </summary>
    private static void RepaintScripts(StringBuilder sb)
    {
        foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>(true))
        {
            if (behaviour == null) continue;

            // La pantalla NDI se queda fuera: su offColor no es "boton sin pulsar", es
            // "no llega señal", y tiene que ser oscuro para que se note que esta muerta.
            if (behaviour is NdiScreenStatus status)
            {
                var nso = new SerializedObject(status);
                nso.FindProperty("offColor").colorValue = new Color32(0x12, 0x16, 0x1C, 255);
                nso.FindProperty("liveColor").colorValue = Color.white;
                nso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(status);

                // Y que no se quede blanca en el editor mientras no hay señal.
                var renderer = nso.FindProperty("screenRenderer").objectReferenceValue as Renderer;
                if (renderer != null) DarkenScreen(renderer, sb);

                sb.AppendLine("pantalla NDI: oscura sin señal, blanca con video");
                continue;
            }

            var so = new SerializedObject(behaviour);
            bool touched = false;

            touched |= SetColor(so, "onColor", Accent) | SetColor(so, "selectedColor", Accent);
            touched |= SetColor(so, "offColor", Raised) | SetColor(so, "idleColor", Raised);
            touched |= SetColor(so, "onText", TextOnAccent) | SetColor(so, "selectedText", TextOnAccent);
            touched |= SetColor(so, "offText", TextPrimary) | SetColor(so, "idleText", TextPrimary);

            if (!touched) continue;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(behaviour);
            sb.AppendLine("colores de " + behaviour.GetType().Name + " actualizados");
        }
    }

    private static bool SetColor(SerializedObject so, string field, Color value)
    {
        var property = so.FindProperty(field);
        if (property == null || property.propertyType != SerializedPropertyType.Color) return false;

        property.colorValue = value;
        return true;
    }

    private static void Set(Graphic graphic, Color color)
    {
        graphic.color = color;
        EditorUtility.SetDirty(graphic);
    }

    private static bool Close(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.04f && Mathf.Abs(a.g - b.g) < 0.04f &&
               Mathf.Abs(a.b - b.b) < 0.04f && Mathf.Abs(a.a - b.a) < 0.05f;
    }

    private static void Count(Dictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out int n);
        counts[key] = n + 1;
    }

    private static string Path(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
