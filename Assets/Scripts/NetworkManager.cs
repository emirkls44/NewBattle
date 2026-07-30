using Fusion;
using UnityEngine;

// 1. NEÞTER: Sýnýf adý dosya adýyla eþleþecek þekilde NetworkManager olarak düzeltildi.
public class NetworkManager : MonoBehaviour
{
    private NetworkRunner _runner;

    async void Start()
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        // 2. NEÞTER: Fusion 2'nin sahne senkronizasyonu için zorunlu bileþen
        gameObject.AddComponent<NetworkSceneManagerDefault>();

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "SavasOdasi",
            // Scene = 1 parametresi silindi. Oyun doðrudan aktif sahnede (SampleScene) baþlayacak.
        });

        Debug.Log("Odaya baþarýyla baðlanýldý!");
    }
}