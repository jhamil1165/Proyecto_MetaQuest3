using UnityEngine;

public class AndroidMulticastFix : MonoBehaviour
{
    void Start()
    {
        // #if asegura que este código SOLO se compile y ejecute en el build de Android (Quest 3).
        // En el Editor de PC se ignorará para no causar errores.
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // 1. Accedemos a la actividad principal de Android en la que corre Unity
            using (AndroidJavaClass player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            
            // 2. Obtenemos el administrador del sistema Wi-Fi de Android
            using (AndroidJavaObject wifiManager = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
            {
                // 3. Solicitamos crear un "MulticastLock" con la etiqueta "NDILock"
                AndroidJavaObject lockObj = wifiManager.Call<AndroidJavaObject>("createMulticastLock", "NDILock");
                
                // 4. Activamos el bloqueo para abrir el paso de paquetes UDP/mDNS en tiempo de ejecución
                lockObj.Call("acquire");
                
                Debug.Log("[NDI Fix] Multicast Lock adquirido correctamente.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("[NDI Fix] Error al intentar adquirir Multicast Lock: " + e.Message);
        }
#endif
    }
}