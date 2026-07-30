using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Oyun Durumu")]
    // DEÐÝÞÝKLÝK: Að üzerinden senkronize edilecek deðerler [Networked] olarak iþaretlendi.
    [Networked] public int activeEnemyCount { get; set; }
    [Networked] public NetworkBool gameEnded { get; set; }

    // UI ve görsel deðiþiklikleri GC (Garbage Collector) tetiklemeden yakalamak için.
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    public override void FixedUpdateNetwork()
    {
        // Sadece yetkili sunucu oyun mantýðýný iþleyebilir. Oyun bittiyse iþlem yapma.
        if (!HasStateAuthority || gameEnded) return;

        // ÖRNEK KONTROL: Düþman sayýsý sýfýrlandýðýnda oyunu bitir.
        // Kendi hayatta kalma mantýðýna göre burayý geniþletebilirsin.
        if (activeEnemyCount <= 0)
        {
            gameEnded = true; // State deðiþir, Render döngüsü yakalar.
        }
    }

    // DEÐÝÞÝKLÝK: UI açma/kapama iþlemleri Update yerine að durumunu dinleyen Render içinde yapýldý.
    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(gameEnded):
                    if (gameEnded)
                    {
                        if (victoryPanel != null) victoryPanel.SetActive(true);
                        // Eðer kaybetme koþulu da eklersen ona göre gameOverPanel'i de yönetebilirsin.
                    }
                    break;
            }
        }
    }

    // Düþman öldüðünde EnemyController içinden sadece HasStateAuthority olan (Server) tarafýndan çaðrýlmalýdýr.
    public void OnEnemyDied()
    {
        if (HasStateAuthority && activeEnemyCount > 0)
        {
            activeEnemyCount--;
        }
    }
}