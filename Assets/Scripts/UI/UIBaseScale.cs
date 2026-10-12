using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// Ziplama / acilis animasyonlari icin nesnenin ASIL olcegini hatirlar.
    ///
    /// Animasyonlar olcegi gecici olarak degistirir; bitince "1"e degil, senin
    /// Hierarchy'de ayarladigin olcege donmeleri gerekir. Ilk sorgulandigi
    /// andaki olcek (animasyon baslamadan once) asil olcek kabul edilir.
    /// </summary>
    public static class UIBaseScale
    {
        private static readonly Dictionary<int, Vector3> Scales = new();

        public static Vector3 Get(Transform target)
        {
            int id = target.GetInstanceID();
            if (!Scales.TryGetValue(id, out Vector3 scale))
            {
                scale = target.localScale;
                Scales[id] = scale;
            }

            return scale;
        }
    }
}
