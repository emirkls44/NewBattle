using Fusion;
using UnityEngine;

public class LobiManager : MonoBehaviour
{
    [Header("Að Referanslarý")]
    [SerializeField] private NetworkRunner runner; // Arama yapmak yerine referans Inspector'dan veya bir baþlatýcýdan verilmeli.

    public void PlayOnlineBasildi()
    {
        if (runner != null && runner.IsServer)
        {
            runner.LoadScene(SceneRef.FromIndex(1));
        }
        else
        {
            Debug.LogWarning("Runner bulunamadý veya bu istemci Server yetkisine sahip deðil.");
        }
    }
}