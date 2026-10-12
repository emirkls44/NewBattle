using System.Collections;
using System.Collections.Generic;
using NewBattle.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Sol ustteki OLDURME AKISI: "emir  Tüfek  Dorian".
    /// Senin oldurmen sari, senin olumun kirmizi, takim arkadasin mavi satirla
    /// gorunur. Her olumde kalan oyuncu sayaci ziplar; kalan 10 / 5 / 3 / 2
    /// oyuncuda ekranda "SON 3 OYUNCU!" duyurusu cikar.
    ///
    /// Gorunum "Menü > Kur" ile kurulur; satir sablonunu Hierarchy'den duzenle.
    /// </summary>
    public class KillFeed : MonoBehaviour
    {
        [SerializeField] private RectTransform list;
        [Tooltip("Her olum icin cogaltilan satir. Gorunumunu buradan duzenle.")]
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField, Range(1, 8)] private int maxRows = 4;
        [SerializeField, Min(0.5f)] private float rowSeconds = 5f;

        [Header("Renkler")]
        [SerializeField] private Color localKillColor = new(1f, 0.72f, 0.1f, 0.92f);
        [SerializeField] private Color localDeathColor = new(0.85f, 0.18f, 0.18f, 0.92f);
        [SerializeField] private Color teammateColor = new(0.2f, 0.5f, 0.95f, 0.85f);
        [SerializeField] private Color otherColor = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private string weaponColor = "#D8D0EA";

        [Header("Kalan Oyuncu")]
        [Tooltip("Her olumde ziplayan sayac (sol ustteki KALAN).")]
        [SerializeField] private RectTransform aliveCounter;
        [Tooltip("Kalan oyuncu bu sayilara dusunce ekranda duyuru cikar.")]
        [SerializeField] private int[] announceAt = { 10, 5, 3, 2 };

        private readonly List<(RectTransform row, float bornAt)> _rows = new();
        private int _lastAnnounced = int.MaxValue;
        private Coroutine _punch;

        private void Awake()
        {
            if (rowTemplate != null)
                rowTemplate.gameObject.SetActive(false);
        }

        private void OnEnable() => KillEvents.Killed += OnKilled;

        private void OnDisable() => KillEvents.Killed -= OnKilled;

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                (RectTransform row, float bornAt) = _rows[i];
                if (row == null)
                {
                    _rows.RemoveAt(i);
                    continue;
                }

                float age = now - bornAt;
                if (row.TryGetComponent(out CanvasGroup group))
                    group.alpha = Mathf.Clamp01(Mathf.Min(age / 0.15f, (rowSeconds - age) / 0.4f));

                if (age >= rowSeconds)
                {
                    Destroy(row.gameObject);
                    _rows.RemoveAt(i);
                }
            }
        }

        private void OnKilled(KillInfo info)
        {
            AddRow(info);
            PunchAliveCounter();
            Announce(info.AliveAfter);
        }

        private void AddRow(KillInfo info)
        {
            if (rowTemplate == null || list == null)
                return;

            RectTransform row = Instantiate(rowTemplate, list);
            row.gameObject.SetActive(true);
            row.SetAsFirstSibling(); // en yeni en ustte

            if (!row.TryGetComponent(out CanvasGroup group))
                group = row.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            TextMeshProUGUI text = row.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                text.text = info.ByZone
                    ? $"{info.VictimName}  <size=75%><color={weaponColor}>alanda kaldı</color></size>"
                    : $"{info.KillerName}  <size=75%><color={weaponColor}>{info.Weapon}</color></size>  {info.VictimName}";
            }

            if (row.TryGetComponent(out Image background))
            {
                background.color = info.KillerIsLocal ? localKillColor
                    : info.VictimIsLocal ? localDeathColor
                    : info.KillerIsTeammate || info.VictimIsTeammate ? teammateColor
                    : otherColor;
            }

            _rows.Add((row, Time.unscaledTime));

            while (_rows.Count > maxRows)
            {
                if (_rows[0].row != null)
                    Destroy(_rows[0].row.gameObject);
                _rows.RemoveAt(0);
            }
        }

        private void PunchAliveCounter()
        {
            if (aliveCounter == null)
                return;

            if (_punch != null)
                StopCoroutine(_punch);
            _punch = StartCoroutine(Punch(aliveCounter));
        }

        private void Announce(int alive)
        {
            if (announceAt == null || alive <= 1)
                return;

            foreach (int threshold in announceAt)
            {
                if (alive != threshold || threshold >= _lastAnnounced)
                    continue;

                _lastAnnounced = threshold;
                ScreenAnnouncer.Show($"SON {threshold} OYUNCU!");
                return;
            }
        }

        /// <summary>Kisa bir buyuyup donme (senin ayarladigin olcek temel alinir).</summary>
        private static IEnumerator Punch(RectTransform target)
        {
            Vector3 baseScale = UIBaseScale.Get(target);
            const float seconds = 0.3f;

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Sin(t / seconds * Mathf.PI);
                target.localScale = baseScale * (1f + 0.3f * k);
                yield return null;
            }

            target.localScale = baseScale;
        }
    }
}
