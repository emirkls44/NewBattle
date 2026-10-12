using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Olunce (ikili modda takim arkadasin hayattayken) altta cikan izleme paneli:
    ///   "İZLENİYOR" + izlenen oyuncunun adi, sol/sag oklarla oyuncular arasinda gecis,
    ///   AYRIL ile izlemeyi birakip mac sonu ekranina gecis.
    /// Mantik MatchResultHUD'da; bu sinif sadece gorunumu tasir.
    /// Tools > NewBattle > Menü > Kur ile Game_UI altina "IzlemePaneli" olarak kurulur.
    /// </summary>
    public class SpectatorPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI tagText;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button leaveButton;

        [Header("Etiket Renkleri")]
        [SerializeField] private Color teammateColor = new(0.45f, 0.9f, 1f, 1f);
        [SerializeField] private Color enemyColor = new(1f, 0.55f, 0.45f, 1f);

        /// <summary>-1: onceki oyuncu, +1: sonraki oyuncu.</summary>
        public event Action<int> StepRequested;
        public event Action LeaveRequested;

        public bool IsShown { get; private set; }

        private bool _wired;

        private void Awake()
        {
            Wire();
            if (!IsShown)
                SetVisible(false);
        }

        private void Wire()
        {
            if (_wired)
                return;

            _wired = true;
            if (previousButton != null)
                previousButton.onClick.AddListener(() => StepRequested?.Invoke(-1));
            if (nextButton != null)
                nextButton.onClick.AddListener(() => StepRequested?.Invoke(1));
            if (leaveButton != null)
                leaveButton.onClick.AddListener(() => LeaveRequested?.Invoke());
        }

        public void Show(string playerName, bool teammate, bool canSwitch)
        {
            Wire();

            if (nameText != null)
                nameText.text = playerName;

            if (tagText != null)
            {
                tagText.text = teammate ? "İZLENİYOR · TAKIM ARKADAŞIN" : "İZLENİYOR · RAKİP";
                tagText.color = teammate ? teammateColor : enemyColor;
            }

            if (previousButton != null)
                previousButton.gameObject.SetActive(canSwitch);
            if (nextButton != null)
                nextButton.gameObject.SetActive(canSwitch);

            if (!IsShown)
            {
                SetVisible(true);
                transform.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            if (IsShown)
                SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            IsShown = visible;
            if (group != null)
            {
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }
    }
}
