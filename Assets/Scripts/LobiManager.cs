using UnityEngine;
using Fusion;

public class LobiManager : MonoBehaviour
{
    public void PlayOnlineBasildi()
    {
        // DEÐÝÞÝKLÝK: Sahne deðiþimi standart Unity yerine Fusion að yöneticisiyle senkronize edildi.
        NetworkRunner runner = FindFirstObjectByType<NetworkRunner>();
        if (runner != null && runner.IsServer)
        {
            runner.LoadScene(SceneRef.FromIndex(1));
        }
    }
}