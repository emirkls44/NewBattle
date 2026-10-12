using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Menudeki her butonun "ne yapacagi" buradan belirlenir.
    ///
    /// Her butonun bir eylem kimligi var (ornek: "magaza", "battle_pass").
    /// Basilinca sirasiyla:
    ///   1. Inspector'daki "On Pressed" olayina bir sey baglanmissa o calisir.
    ///   2. Kodda MenuButton.Register("magaza", ...) ile bir islev kayitliysa o calisir.
    ///   3. Hicbiri yoksa "YAKINDA!" bildirimi cikar.
    ///
    /// Yani bir butonu baglamak icin gorunume dokunmaya gerek yok; ilgili
    /// kimlige bir islev kaydetmek yeterli.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour
    {
        private static readonly Dictionary<string, Action> Handlers = new();

        [Tooltip("Bu butonun eylem kimligi. Kodda bu isimle baglanir.")]
        [SerializeField] private string actionId = "yeni_eylem";

        [Tooltip("Istege bagli: Inspector'dan dogrudan bir islev baglamak icin.")]
        [SerializeField] private UnityEvent onPressed = new();

        /// <summary>Baglanmamis bir butona basildiginda (menu "YAKINDA!" gosterir).</summary>
        public static event Action<MenuButton> Unhandled;

        public string ActionId => actionId;

        /// <summary>Bir eylem kimligine islev baglar. Ayni kimlige tekrar baglamak oncekinin yerine gecer.</summary>
        public static void Register(string actionId, Action handler)
        {
            if (!string.IsNullOrEmpty(actionId) && handler != null)
                Handlers[actionId] = handler;
        }

        public static void Unregister(string actionId, Action handler)
        {
            if (actionId != null && Handlers.TryGetValue(actionId, out Action current) && current == handler)
                Handlers.Remove(actionId);
        }

        public static bool IsRegistered(string actionId) => actionId != null && Handlers.ContainsKey(actionId);

        /// <summary>Kodun icinden bir eylemi calistirir (ornek: "magaza"). Kayitli degilse false.</summary>
        public static bool TryInvoke(string actionId)
        {
            if (actionId == null || !Handlers.TryGetValue(actionId, out Action handler))
                return false;

            handler();
            return true;
        }

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Press);
        }

        public void Press()
        {
            if (onPressed.GetPersistentEventCount() > 0)
            {
                onPressed.Invoke();
                return;
            }

            if (Handlers.TryGetValue(actionId, out Action handler))
            {
                handler();
                return;
            }

            Unhandled?.Invoke(this);
        }
    }
}
