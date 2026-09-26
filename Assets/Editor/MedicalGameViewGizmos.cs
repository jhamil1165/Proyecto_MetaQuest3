using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mantiene apagada la capa de gizmos de la vista Game.
///
/// Con esa capa encendida, Unity dibuja el contorno de cada elemento de interfaz encima de
/// la imagen. Como el visor tiene muchos paneles, salen cientos de rectangulos azules y
/// parece que la escena esta rota, cuando lo que se manda al Quest esta perfecto: los gizmos
/// son una capa del editor y no llegan al visor ni al APK.
///
/// El boton de la barra existe, pero es por ventana y se vuelve a encender al cambiar de
/// layout o al abrir otra vista Game, que es justo lo que pasa al conectar el Quest por Link.
/// Por eso se apaga tambien al abrir el proyecto y cada vez que se entra y se sale de Play,
/// en vez de dejarlo a mano.
/// </summary>
[InitializeOnLoad]
public static class MedicalGameViewGizmos
{
    static MedicalGameViewGizmos()
    {
        EditorApplication.delayCall += Silent;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void OnPlayModeChanged(PlayModeStateChange change)
    {
        // Al entrar en Play se recrea la ventana y vuelve con el ajuste de antes.
        if (change == PlayModeStateChange.EnteredPlayMode || change == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += Silent;
        }
    }

    [MenuItem("MedicalViewer/Step78 - Quitar los gizmos de la vista Game")]
    public static void Apply()
    {
        var sb = new StringBuilder();
        int changed = Disable(sb);

        Debug.Log(sb.ToString());
        Debug.Log(changed > 0
            ? "Gizmos apagados en " + changed + " vista(s) Game."
            : "No habia ninguna vista Game abierta; quedara apagada en cuanto se abra.");
        Debug.Log("STEP78_DONE");
    }

    private static void Silent()
    {
        Disable(null);
    }

    /// <summary>
    /// Apaga el interruptor en todas las vistas Game abiertas. Se hace por reflexion porque
    /// UnityEditor.GameView es interno y no hay forma publica de tocarlo.
    /// </summary>
    private static int Disable(StringBuilder sb)
    {
        System.Type gameView = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        if (gameView == null)
        {
            sb?.AppendLine("[AVISO] no encuentro el tipo UnityEditor.GameView");
            return 0;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = gameView.GetProperty("showGizmos", Flags);
        FieldInfo field = gameView.GetField("m_Gizmos", Flags);

        sb?.AppendLine("propiedad showGizmos: " + (property != null && property.CanWrite ? "si" : "no"));
        sb?.AppendLine("campo m_Gizmos: " + (field != null ? "si" : "no"));

        if ((property == null || !property.CanWrite) && field == null)
        {
            sb?.AppendLine("[AVISO] esta version de Unity no expone el interruptor; hay que apagarlo a mano");
            return 0;
        }

        int changed = 0;
        foreach (Object window in Resources.FindObjectsOfTypeAll(gameView))
        {
            if (property != null && property.CanWrite) property.SetValue(window, false);
            else field.SetValue(window, false);

            ((EditorWindow)window).Repaint();
            changed++;
        }

        sb?.AppendLine("vistas Game encontradas: " + changed);
        return changed;
    }
}
