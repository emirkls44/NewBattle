using System;
using System.Collections;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// Odullu reklam. Henuz reklam SDK'si (AdMob, Unity Ads...) yok: simdilik
    /// kisa bir bekleme yapip reklam izlenmis sayar. SDK eklenince sadece
    /// Show'un ici degisir; IKI KAT XP butonlari ayni kalir.
    /// </summary>
    public static class RewardedAds
    {
        /// <summary>Test modunda "reklamin" suresi (saniye).</summary>
        public const float SimulatedSeconds = 1.5f;

        private static float _busyUntil;

        // Zamana bagli: reklami acan ekran yok olsa bile kilit kendiliginden acilir.
        public static bool IsShowing => Time.realtimeSinceStartup < _busyUntil;

        /// <summary>Reklami gosterir; bitince onFinished(izlendi mi) cagrilir.</summary>
        public static void Show(MonoBehaviour host, Action<bool> onFinished)
        {
            if (IsShowing || host == null || !host.isActiveAndEnabled)
            {
                onFinished?.Invoke(false);
                return;
            }

            _busyUntil = Time.realtimeSinceStartup + SimulatedSeconds + 0.5f;
            host.StartCoroutine(Simulate(onFinished));
        }

        private static IEnumerator Simulate(Action<bool> onFinished)
        {
            yield return new WaitForSecondsRealtime(SimulatedSeconds);
            _busyUntil = 0f;
            onFinished?.Invoke(true);
        }
    }
}
