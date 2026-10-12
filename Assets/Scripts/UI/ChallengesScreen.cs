using System;
using System.Collections.Generic;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// MÜCADELELER: solda gunluk, sagda haftalik gorevler ve yenilenmeye
    /// kalan sure. Menudeki "mucadeleler" karti acar. Gorevlerin kendisi
    /// IlerlemeAyarlari'nda.
    /// </summary>
    public class ChallengesScreen : MonoBehaviour
    {
        [Header("Listeler")]
        [SerializeField] private RectTransform dailyList;
        [SerializeField] private RectTransform weeklyList;
        [Tooltip("Her gorev icin cogaltilan satir. Gorunumunu buradan duzenle.")]
        [SerializeField] private ChallengeRow rowTemplate;

        [Header("Kalan Sure")]
        [SerializeField] private TextMeshProUGUI dailyTimerText;
        [SerializeField] private TextMeshProUGUI weeklyTimerText;

        [SerializeField] private Button closeButton;
        [SerializeField] private MainMenuScreen menu;

        private readonly List<ChallengeRow> _rows = new();
        private bool _initialized;
        private bool _opening;
        private float _nextTimerUpdate;

        private void Awake()
        {
            Initialize();

            if (!_opening)
                gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            if (closeButton != null)
                closeButton.onClick.AddListener(Close);
            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);
        }

        public void Open()
        {
            Initialize();

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            Build();
            PlayerProgress.Changed -= Build;
            PlayerProgress.Changed += Build;
        }

        public void Close()
        {
            PlayerProgress.Changed -= Build;
            gameObject.SetActive(false);
        }

        private void OnDestroy() => PlayerProgress.Changed -= Build;

        private void Update()
        {
            if (Time.unscaledTime < _nextTimerUpdate)
                return;

            _nextTimerUpdate = Time.unscaledTime + 1f;
            SetTimer(dailyTimerText, Challenges.TimeUntilReset(daily: true));
            SetTimer(weeklyTimerText, Challenges.TimeUntilReset(daily: false));
        }

        private void Build()
        {
            foreach (ChallengeRow row in _rows)
            {
                if (row != null)
                    Destroy(row.gameObject);
            }

            _rows.Clear();
            Fill(dailyList, Challenges.Daily);
            Fill(weeklyList, Challenges.Weekly);
            _nextTimerUpdate = 0f;
        }

        private void Fill(RectTransform list, List<ChallengeStatus> statuses)
        {
            if (list == null || rowTemplate == null)
                return;

            foreach (ChallengeStatus status in statuses)
            {
                ChallengeRow row = Instantiate(rowTemplate, list);
                row.name = $"Gorev_{status.Def.id}";
                row.gameObject.SetActive(true);
                row.Bind(status);

                ChallengeStatus captured = status;
                row.Button.onClick.AddListener(() => OnRowPressed(captured));
                _rows.Add(row);
            }
        }

        private void OnRowPressed(ChallengeStatus status)
        {
            if (menu == null)
                return;

            menu.ShowToast(status.Done
                ? "TAMAMLANDI!"
                : $"{status.Def.target - status.Progress} KALDI");
        }

        private static void SetTimer(TextMeshProUGUI target, TimeSpan left)
        {
            if (target == null)
                return;

            target.text = left.TotalDays >= 1
                ? $"Yenilenme: {(int)left.TotalDays} gün {left.Hours} sa"
                : $"Yenilenme: {left.Hours} sa {left.Minutes} dk";
        }
    }
}
