using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Takim odasinda bir takim karti: baslik + oyuncu satirlari. Karta basan oyuncu
    /// o takima gecer. TeamRoomScreen sablondan (TakimSablonu) gerektigi kadar kopyalar.
    /// </summary>
    public class TeamRoomCard : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            public GameObject root;
            public TextMeshProUGUI nameText;
            [Tooltip("Oyuncu hazirsa yanar.")]
            public Image readyIcon;
            [Tooltip("Odayi kuran oyuncunun yaninda gorunur.")]
            public GameObject hostIcon;
        }

        public Button button;
        public Image background;
        public TextMeshProUGUI title;
        public Slot[] slots;
    }
}
