using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>Atilabilir esyalar. Sira aga giden kimliktir; yenisini SONA ekle.</summary>
    public enum ThrowableKind
    {
        Apricot = 0,
        Hazelnut = 1
    }

    public enum ThrowEffect
    {
        [Tooltip("Alandaki takim arkadaslari (ve atan) iyilesir.")]
        HealTeam,
        [Tooltip("Alandaki rakipler neredeyse durana kadar yavaslar.")]
        SlowEnemies
    }

    /// <summary>
    /// Atilabilir esya ayarlari: Resources/KayisiAyarlari (iyilestirme) ve
    /// Resources/FindikAyarlari (yavaslatma). Asagidaki aciklama kayisi icin.
    ///
    /// Kayisi (atilabilir iyilestirme) ayarlari. Dosya: Assets/Resources/KayisiAyarlari
    /// (Tools > NewBattle > Menü > Kur olusturur). Buradaki her deger oyunda aninda gecerlidir.
    ///
    /// Oyuncu yerden kayisi toplar, sag ustteki kayisi butonuyla eline alir, saldiri
    /// joystick'iyle nisan alip birakinca atar. Dustugu yerde yesil bir iyilestirme
    /// alani olusur: icine giren takim arkadaslari ve atan oyuncu iyilesir, rakip iyilesmez.
    /// </summary>
    [CreateAssetMenu(menuName = "NewBattle/Kayisi Ayarlari", fileName = "KayisiAyarlari")]
    public class ApricotSettings : ScriptableObject
    {
        public const string ResourcePath = "KayisiAyarlari";
        public const string HazelnutResourcePath = "FindikAyarlari";

        [Header("Tur")]
        public ThrowableKind kind = ThrowableKind.Apricot;
        public ThrowEffect effect = ThrowEffect.HealTeam;
        [Tooltip("SlowEnemies: alandaki rakibin hiz carpani (0.15 = neredeyse durur).")]
        [Range(0.02f, 1f)] public float slowMultiplier = 0.15f;
        [Tooltip("SlowEnemies: alandan cikinca yavaslik bu kadar daha surer (saniye).")]
        [Min(0.05f)] public float slowLingerSeconds = 0.5f;

        [Header("Gorseller (Kur baglar)")]
        [Tooltip("Elde, havada ucarken gorunen model (boyu 1 metreye olceklenmis).")]
        public GameObject model;
        public Sprite icon;
        [Tooltip("Alandan yukari suzulen beyaz kayisi silueti (yesile boyanir).")]
        public Sprite silhouette;
        [Tooltip("Alandaki yumusak duman bulutu (yesile boyanir).")]
        public Sprite smoke;
        [Tooltip("Iyilestirme alani ag prefabi.")]
        public NetworkPrefabRef zonePrefab;

        [Header("Toplama")]
        [Min(1)] public int maxCarry = 3;
        [Min(1)] public int pickupAmount = 1;

        [Header("Atis")]
        [Tooltip("Joystick sonuna kadar cekilince en uzak atis (metre).")]
        [Min(1f)] public float throwDistance = 7f;
        [Tooltip("Joystick az cekilince en yakin atis (metre).")]
        [Min(0.5f)] public float minThrowDistance = 2f;
        [Tooltip("Kayisinin havada kalma suresi (saniye).")]
        [Min(0.1f)] public float flightSeconds = 0.6f;
        [Tooltip("Iki atis arasi en az sure.")]
        [Min(0f)] public float throwCooldown = 0.6f;
        [Tooltip("Ucus egrisinin tepe yuksekligi (atis uzakliginin orani).")]
        [Range(0.1f, 1f)] public float arcHeight = 0.35f;
        public LayerMask groundMask = ~0;

        [Header("Iyilestirme Alani")]
        [Min(0.5f)] public float radius = 2.5f;
        [Tooltip("Alan kac saniye kalir.")]
        [Min(0.5f)] public float duration = 6f;
        [Tooltip("Alandaki takim arkadasinin saniyede kazandigi can.")]
        [Min(0f)] public float healPerSecond = 12f;

        [Header("Gorunum")]
        public Color ringColor = new(0.35f, 1f, 0.3f, 0.95f);
        public Color fillColor = new(0.3f, 0.95f, 0.3f, 0.22f);
        public Color smokeColor = new(0.45f, 0.95f, 0.45f, 0.38f);
        public Color silhouetteColor = new(0.6f, 1f, 0.55f, 0.9f);
        [Tooltip("Havada ve elde kayisinin boyu (metre).")]
        [Min(0.05f)] public float modelSize = 0.35f;
        [Tooltip("Eldeki kayisinin el kemigine gore konumu (metre, kemigin yonunde).")]
        public Vector3 heldOffset = new(0f, 0.08f, 0.04f);

        private static ApricotSettings _instance;
        private static ApricotSettings _hazelnut;

        /// <summary>Kayisi ayarlari.</summary>
        public static ApricotSettings Instance =>
            _instance != null ? _instance : _instance = Resources.Load<ApricotSettings>(ResourcePath);

        public static ApricotSettings Get(ThrowableKind kind)
        {
            if (kind == ThrowableKind.Hazelnut)
                return _hazelnut != null ? _hazelnut : _hazelnut = Resources.Load<ApricotSettings>(HazelnutResourcePath);
            return Instance;
        }

        /// <summary>
        /// Nisandan dusus noktasi. Joystick ne kadar cekildiyse o kadar uzak; arada duvar
        /// varsa duvarin onune duser. Hem nisan cizgisi hem sunucu ayni hesabi kullanir.
        /// </summary>
        public Vector3 ResolveLanding(Vector3 from, Vector2 aim, Transform self)
        {
            Vector3 direction = new(aim.x, 0f, aim.y);
            float strength = Mathf.Clamp01(direction.magnitude);
            if (direction.sqrMagnitude < 0.0001f)
                direction = self != null ? self.forward : Vector3.forward;
            direction.y = 0f;
            direction.Normalize();

            float distance = Mathf.Lerp(minThrowDistance, throwDistance, strength);
            Vector3 chest = from + Vector3.up * 1f;

            if (Physics.Raycast(chest, direction, out RaycastHit wall, distance, groundMask, QueryTriggerInteraction.Ignore) &&
                (self == null || !wall.transform.IsChildOf(self)) &&
                wall.collider.GetComponentInParent<HealthController>() == null)
            {
                distance = Mathf.Max(0.5f, wall.distance - 0.4f);
            }

            Vector3 point = from + direction * distance;
            float groundY = from.y;
            float best = float.NegativeInfinity;

            // Agac tepesine / cati ustune degil, ayak hizasina yakin zemine dussun.
            RaycastHit[] hits = Physics.RaycastAll(point + Vector3.up * 6f, Vector3.down, 20f, groundMask, QueryTriggerInteraction.Ignore);
            foreach (RaycastHit hit in hits)
            {
                if (hit.point.y > from.y + 1.5f || hit.point.y <= best)
                    continue;
                if (hit.collider.GetComponentInParent<HealthController>() != null)
                    continue;
                best = hit.point.y;
            }

            if (best > float.NegativeInfinity)
                groundY = best;

            point.y = groundY;
            return point;
        }

        /// <summary>Ucus egrisi uzerindeki nokta (t: 0 baslangic, 1 dusus).</summary>
        public Vector3 ArcPoint(Vector3 start, Vector3 end, float t)
        {
            float height = Mathf.Max(0.8f, Vector3.Distance(start, end) * arcHeight);
            return Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
        }
    }
}
