using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>
    /// Harita secim ekraninin verisi: kare harita resmi, resmin dunyada
    /// kapladigi alan ve isimli bolgeler (Mezarlik, Ciftlik...).
    ///
    /// Gercek haritanin kusbakisi resmi geldiginde sadece "Harita Resmi" alani
    /// degisir; resmi cekerken kameranin gordugu kenar uzunlugunu da
    /// "Dunya Boyutu"na yazarsin. Bolgeler resme gore (0-1) tanimli oldugu icin
    /// harita boyutu degisse de yerinde kalir.
    /// </summary>
    [CreateAssetMenu(fileName = "HaritaVerisi", menuName = "NewBattle/Harita Verisi")]
    public class DropMapData : ScriptableObject
    {
        [Serializable]
        public class Region
        {
            public string id = "yeni_bolge";
            [Tooltip("Haritada isaretcinin yaninda gorunen ad.")]
            public string name = "Yeni Bölge";
            [Tooltip("Resim uzerinde 0-1 arasi alan. (0,0) sol ALT kose. X, Y = sol alt kose; W, H = genislik, yukseklik.")]
            public Rect area = new(0.4f, 0.4f, 0.2f, 0.2f);
        }

        [Tooltip("Kare harita resmi. Ust taraf = kuzey (dunyada +Z).")]
        [SerializeField] private Texture2D mapTexture;

        [Header("Resmin Dunyadaki Karsiligi")]
        [Tooltip("Resmin bir kenarinin dunyadaki uzunlugu (metre). 0 = oynanabilir alandan otomatik hesapla.")]
        [SerializeField, Min(0f)] private float worldSize;
        [Tooltip("Otomatik hesapta: resim, oynanabilir alanin kac kati genisligi gostersin.")]
        [SerializeField, Min(1f)] private float autoSizeMultiplier = 1.15f;
        [Tooltip("Resmin merkezinin dunyadaki X/Z konumu.")]
        [SerializeField] private Vector2 worldCenter;

        [Header("Bolgeler")]
        [SerializeField] private List<Region> regions = new();

        public Texture2D MapTexture => mapTexture;
        public IReadOnlyList<Region> Regions => regions;

        /// <summary>Resmin bir kenarinin dunyadaki uzunlugu.</summary>
        public float WorldSize(float playableExtent)
        {
            return worldSize > 0f ? worldSize : playableExtent * 2f * autoSizeMultiplier;
        }

        /// <summary>Resim uzerindeki noktayi (0-1, sol alt orijin) dunya konumuna cevirir.</summary>
        public Vector3 NormalizedToWorld(Vector2 normalized, float playableExtent)
        {
            float size = WorldSize(playableExtent);
            return new Vector3(
                worldCenter.x + (normalized.x - 0.5f) * size,
                0f,
                worldCenter.y + (normalized.y - 0.5f) * size);
        }

        /// <summary>Noktayi iceren en kucuk bolge; yoksa null.</summary>
        public Region FindRegion(Vector2 normalized)
        {
            Region best = null;
            float bestArea = float.MaxValue;

            foreach (Region region in regions)
            {
                if (region == null || !region.area.Contains(normalized))
                    continue;

                float area = region.area.width * region.area.height;
                if (area < bestArea)
                {
                    best = region;
                    bestArea = area;
                }
            }

            return best;
        }
    }
}
