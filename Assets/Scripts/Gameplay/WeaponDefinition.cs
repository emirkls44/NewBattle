using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Tek bir silahin tum davranisi. Yeni silah eklemek = PlayerShooting'deki
    /// diziye yeni bir eleman eklemek; kod yazmak gerekmez.
    ///
    /// Neden ScriptableObject degil de prefab uzerinde dizi:
    /// Silah kimligi ag uzerinden sadece bir int olarak gider (WeaponId = dizideki
    /// indeks). Dizi oyuncu prefabinda durdugu icin her istemcide birebir ayni
    /// tanimlar bulunur - ayri bir asset'in tum istemcilerde yuklu olmasina
    /// guvenmek zorunda kalmayiz.
    ///
    /// ONEMLI: Dizinin SIRASI aga giden kimliktir. Ortadan bir silah silersen
    /// sonrakilerin kimligi kayar. Yeni silahlari her zaman SONA ekle.
    /// </summary>
    [System.Serializable]
    public class WeaponDefinition
    {
        [Tooltip("Sadece Inspector'da okumak icin; oyun icinde kullanilmaz.")]
        public string label = "Silah";

        [Header("Tur")]
        [Tooltip("Acikken menzilli atis yerine yakin dovus kuresi kullanilir.")]
        public bool isMelee;

        [Header("Hasar")]
        [Min(0f)] public float damage = 15f;

        [Tooltip("Iki atis arasindaki saniye. Kucuk = hizli.")]
        [Min(0.02f)] public float fireInterval = 0.18f;

        [Min(0.5f)] public float range = 35f;

        [Header("Sacma")]
        [Tooltip("Tek atista cikan mermi sayisi. Pompali icin 5-8, tufek icin 1.")]
        [Min(1)] public int pellets = 1;

        [Tooltip("Sacma yayilma acisi (derece). pellets = 1 iken de hafif sapma verir.")]
        [Min(0f)] public float spreadAngle;

        [Header("Cephane")]
        [Tooltip("Bu silahin tasiyabilecegi azami mermi.")]
        [Min(1)] public int maxAmmo = 120;

        [Tooltip("Acikken atis mermi harcamaz (yumruk gibi).")]
        public bool infiniteAmmo;

        [Header("Gorsel")]
        [Tooltip("Iz efektinin rengi. Airdrop silahi icin farkli bir renk secmek iyi olur.")]
        public Color tracerColor = new(1f, 0.93f, 0.55f, 1f);

        /// <summary>
        /// Bu silah ates ettiginde kameraya eklenen sarsinti (0..1).
        ///
        /// Atis hizina gore ayarlanmali: seri ates eden bir silahta yuksek
        /// deger sarsintiyi biriktirip ekrani okunmaz hale getirir. Kabaca
        /// "tek atista hissedilir ama rahatsiz etmeyen" bir deger dogru;
        /// seri ateste birikme zaten siddeti kendisi buyutuyor.
        /// </summary>
        [Tooltip("Ates ederken kameranin ne kadar sarsilacagi. Seri ateste birikir.")]
        [Range(0f, 1f)] public float shakeStrength = 0.18f;

        /// <summary>
        /// Varsayilan silah seti. Prefab'da dizi bos birakilirsa bu kullanilir,
        /// boylece proje hicbir Inspector ayari yapilmadan calisir.
        ///
        /// Sira = ag kimligi: 0 yumruk, 1 normal tufek, 2 airdrop silahi.
        /// </summary>
        public static WeaponDefinition[] CreateDefaultSet()
        {
            return new[]
            {
                new WeaponDefinition
                {
                    label = "Yumruk",
                    isMelee = true,
                    damage = 25f,
                    fireInterval = 0.5f,
                    range = 1.5f,
                    infiniteAmmo = true,
                    maxAmmo = 1
                },
                new WeaponDefinition
                {
                    label = "Tufek",
                    damage = 15f,
                    fireInterval = 0.18f,
                    range = 35f,
                    pellets = 1,
                    spreadAngle = 1.5f,
                    maxAmmo = 120
                },
                new WeaponDefinition
                {
                    label = "Airdrop Tufegi",
                    damage = 31f,
                    fireInterval = 0.16f,
                    range = 42f,
                    pellets = 1,
                    spreadAngle = 1f,
                    maxAmmo = 120,
                    tracerColor = new Color(1f, 0.55f, 0.15f, 1f)
                }
            };
        }
    }
}
