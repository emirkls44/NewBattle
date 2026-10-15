using NewBattle.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Silah gostergesinin yanindaki kayisi butonu: kayisi resmi, sol altta kac tane
    /// oldugu. Basinca kayisi ele alinir (cerceve parlar); tekrar basinca ya da silah
    /// kutusuna basinca silaha / yumruga donulur. Kayisi yoksa gizlenir.
    /// Tools > NewBattle > Menü > Kur, Game_UI altina "KayisiYuvasi" olarak kurar.
    /// </summary>
    public class ApricotSlotHUD : MonoBehaviour
    {
        [Tooltip("Bu buton hangi atilabilir icin: kayisi ya da findik.")]
        [SerializeField] private ThrowableKind kind = ThrowableKind.Apricot;
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private Image frame;
        [SerializeField] private TextMeshProUGUI countText;

        [Header("Renkler")]
        [SerializeField] private Color normalFrame = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color selectedFrame = new(1f, 0.85f, 0.2f, 1f);

        private CanvasGroup _group;
        private PlayerLoadout _loadout;
        private DropPhaseController _dropPhase;
        private float _nextSearchTime;
        private int _shownCount = -1;
        private float _popTime = -10f;
        private Vector3 _iconBaseScale = Vector3.one;

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null)
                _group = gameObject.AddComponent<CanvasGroup>();
            SetShown(false);

            if (icon != null)
            {
                _iconBaseScale = icon.rectTransform.localScale;
                ApricotSettings settings = ApricotSettings.Get(kind);
                if (settings != null && settings.icon != null)
                    icon.sprite = settings.icon;
            }

            if (button != null)
                button.onClick.AddListener(Toggle);
        }

        private void Toggle()
        {
            if (Ready(_loadout))
            {
                _loadout.RequestEquip(_loadout.IsEquipped(kind) ? null : kind);
                NewBattle.Audio.Sfx.PlayUI("buton");
            }
        }

        private void Update()
        {
            if (!Ready(_loadout) && Time.unscaledTime >= _nextSearchTime)
            {
                _loadout = FindLocalLoadout();
                if (_dropPhase == null)
                    _dropPhase = FindFirstObjectByType<DropPhaseController>();
                _nextSearchTime = Time.unscaledTime + 0.25f;
            }

            bool playing = Ready(_loadout) && _dropPhase != null && _dropPhase.Object != null && _dropPhase.Object.IsValid &&
                           _dropPhase.GameplayStarted && !MatchResultHUD.LocalGameplayStopped;
            int count = playing ? _loadout.CountOf(kind) : 0;
            SetShown(playing && count > 0);
            if (!playing || count <= 0)
            {
                _shownCount = count;
                return;
            }

            if (count != _shownCount)
            {
                if (count > _shownCount && _shownCount >= 0)
                    _popTime = Time.unscaledTime;
                _shownCount = count;
                if (countText != null)
                    countText.text = count.ToString();
            }

            if (frame != null)
                frame.color = _loadout.IsEquipped(kind) ? selectedFrame : normalFrame;

            if (icon != null)
            {
                float t = (Time.unscaledTime - _popTime) / 0.28f;
                float bump = t is >= 0f and < 1f ? Mathf.Sin(t * Mathf.PI) * 0.2f : 0f;
                float held = _loadout.IsEquipped(kind) ? 1.08f : 1f;
                icon.rectTransform.localScale = _iconBaseScale * (held + bump);
            }
        }

        private void SetShown(bool shown)
        {
            _group.alpha = shown ? 1f : 0f;
            _group.interactable = shown;
            _group.blocksRaycasts = shown;
        }

        private static bool Ready(PlayerLoadout loadout) =>
            loadout != null && loadout.Object != null && loadout.Object.IsValid;

        private static PlayerLoadout FindLocalLoadout()
        {
            foreach (PlayerLoadout candidate in FindObjectsByType<PlayerLoadout>(FindObjectsSortMode.None))
            {
                if (Ready(candidate) && candidate.HasInputAuthority)
                    return candidate;
            }

            return null;
        }
    }
}
