using Fusion;
using UnityEngine;

public class TemelBaglanti : MonoBehaviour
{
    private NetworkRunner _runner;

    async void Start()
    {
        // 1. Objenin içine Fusion'ýn að yöneticisini ekliyoruz
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true; // Karakteri hareket ettirebilmek için girdi yetkisi veriyoruz

        // 2. Odaya baðlanma iþlemini baþlatýyoruz
        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient, // Ýlk giren odayý kurar, sonrakiler katýlýr
            SessionName = "SavasOdasi",           // Odanýn adý
            Scene = 1                             // Yüklenecek sahne numarasý (Build Settings'den ayarlanmalý)
        });

        Debug.Log("Odaya baþarýyla baðlanýldý!");
    }
}