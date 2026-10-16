using NewBattle.Progression;
using TMPro;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// Bir yaziya oyuncunun adini ya da parasini yazar ve degisince gunceller
    /// (oyun modu penceresinin ustundeki isim ve para gibi).
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class ProgressLabel : MonoBehaviour
    {
        public enum Value
        {
            PlayerName,
            Coins
        }

        [SerializeField] private Value value = Value.PlayerName;

        private TextMeshProUGUI _text;

        private void OnEnable()
        {
            _text = GetComponent<TextMeshProUGUI>();
            PlayerProgress.Changed += Refresh;
            Refresh();
        }

        private void OnDisable() => PlayerProgress.Changed -= Refresh;

        private void Refresh()
        {
            if (_text != null)
                _text.text = value == Value.Coins ? PlayerProgress.Coins.ToString() : PlayerProgress.PlayerName;
        }
    }
}
