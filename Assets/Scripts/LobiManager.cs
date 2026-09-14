using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class LobiManager : MonoBehaviour
{
    [Header("Að Referanslarý")]
    [SerializeField] private NetworkRunner runnerPrefab;
    private NetworkRunner _runnerInstance;

    public async void PlayOnlineBasildi()
    {
        // 1. UI tepkisini engellemek için butonu pasifize etme mantýðý buraya eklenebilir.
        Debug.Log("Eþleþtirme aranýyor...");

        if (_runnerInstance == null)
        {
            _runnerInstance = Instantiate(runnerPrefab);
        }

        // 2. Sahne yöneticisini runner'a baðla
        _runnerInstance.ProvideInput = true;
        var sceneManager = _runnerInstance.gameObject.AddComponent<NetworkSceneManagerDefault>();

        // 3. Battlelands Royale stili AutoHostOrClient eþleþtirmesi baþlat
        var result = await _runnerInstance.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.AutoHostOrClient,
            SessionName = "BattlelandsArena", // Ýleride rastgele veya versiyon bazlý yapýlabilir
            SceneManager = sceneManager,
            Scene = SceneRef.FromIndex(1) // 1. Index'teki oyun sahnesine geçiþ yap
        });

        if (result.Ok)
        {
            Debug.Log("Odaya baþarýyla baðlanýldý.");
        }
        else
        {
            Debug.LogError($"Baðlantý hatasý: {result.ShutdownReason}");
            // Baþarýsýz olursa Runner'ý temizle
            Destroy(_runnerInstance.gameObject);
        }
    }
}