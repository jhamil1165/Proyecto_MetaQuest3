using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Al abrir el proyecto, si faltan las texturas 3D del volumen las genera a partir de los
/// ficheros comprimidos de Assets/Volumes.
///
/// Por qué: una textura 3D de 512x512x267 ocupa 134 MB en disco y GitHub no admite ficheros
/// de más de 100 MB, así que las texturas no se suben. Lo que se sube son los datos
/// comprimidos (26 MB entre los dos) y cada ordenador se fabrica las suyas. Quien clone el
/// repositorio no tiene que hacer nada: abre Unity y aparecen.
/// </summary>
[InitializeOnLoad]
public static class MedicalVolumeAutoBuild
{
    private const string Marker = "Assets/Volumes/Tex_ct_densidad.asset";

    static MedicalVolumeAutoBuild()
    {
        // Se espera a que el editor termine de importar: durante la carga la base de datos
        // de assets todavía no está en condiciones de crear nada.
        EditorApplication.delayCall += Check;
    }

    private static void Check()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (AssetDatabase.LoadAssetAtPath<Texture3D>(Marker) != null) return;
        if (!System.IO.Directory.Exists("Assets/Volumes")) return;

        var sb = new StringBuilder();
        sb.AppendLine("Faltaban las texturas del volumen 3D; se generan desde Assets/Volumes.");

        if (MedicalVolumeSetup.RebuildTextures(sb))
        {
            Debug.Log(sb.ToString());
        }
        else
        {
            Debug.LogWarning(sb + "No se han podido generar. Revisa que estén los ficheros " +
                             "ct_densidad_*.bytes.gz y ct_organos_*.bytes.gz.");
        }
    }
}
