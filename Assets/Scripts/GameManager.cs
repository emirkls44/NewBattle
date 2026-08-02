using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    [Header("UI Panelleri")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject gameOverPanel;

    [Networked] public int activeEnemyCount { get; set; }
    [Networked] public NetworkBool gameEnded { get; set; }

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            switch (change)
            {
                case nameof(gameEnded):
                    if (gameEnded && victoryPanel != null) victoryPanel.SetActive(true);
                    break;
            }
        }
    }

    public void RegisterEnemyDeath()
    {
        if (!HasStateAuthority || gameEnded) return;

        activeEnemyCount--;
        if (activeEnemyCount <= 0)
        {
            gameEnded = true;
        }
    }
}