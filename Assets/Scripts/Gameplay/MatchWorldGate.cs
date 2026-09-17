using Fusion;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Oyun dunyasini, oyuncu bir mod secip baglanti kurulana kadar KAPALI tutar.
    ///
    /// NEDEN GEREKLI: Menu ile oyun ayni sahnede yasiyor. Sahne acilir acilmaz
    /// harita duruyor, cimenler dagiliyor, bot ve loot sistemleri uyaniyor -
    /// oyuncu daha "Oyna"ya bile basmadan. Bunun uc bedeli var:
    ///   - Menude gereksiz yere sahne kuruluyor, acilis yavasliyor
    ///   - Menunun arkasinda oyun akiyor, hangi ekranda oldugun belirsizlesiyor
    ///   - Birden fazla harita eklendiginde HANGI haritanin kurulacagi menude
    ///     belli degil; hepsini birden kurmak sacma olur
    ///
    /// Bu bilesen dunyayi Runner calismaya baslayana kadar kapali tutar. Gercek
    /// cozum her haritayi ayri bir sahneye almaktir (bkz. asagidaki not); bu ara
    /// adim, sahne ayrimi yapilana kadar dogru davranisi verir.
    ///
    /// SAHNE MIMARISI NOTU: Birden fazla harita icin dogru yapi sudur -
    /// "Menu" sahnesi acilir, oyuncu mod ve harita secer, NetworkBootstrap
    /// StartGame'e o haritanin SceneRef'ini verir, Fusion herkesi o sahneye
    /// tasir. O noktada bu bilesene gerek kalmaz: harita sahnesi zaten yalnizca
    /// mac basladiginda yuklenir.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class MatchWorldGate : MonoBehaviour
    {
        [Header("Mac Baslayinca Acilacaklar")]
        [Tooltip("Harita kokleri, cimen uretici, prop'lar... Mac baslayana kadar kapali durur.")]
        [SerializeField] private GameObject[] worldRoots;

        [Header("Menude Gorunecekler")]
        [Tooltip("Sadece menude acik kalacak nesneler (ornegin menu arka plani).")]
        [SerializeField] private GameObject[] menuOnlyRoots;

        private NetworkRunner _runner;
        private bool _worldOpened;

        private void Awake()
        {
            SetActive(worldRoots, false);
            SetActive(menuOnlyRoots, true);
        }

        private void Update()
        {
            if (_worldOpened)
                return;

            if (_runner == null)
                _runner = FindFirstObjectByType<NetworkRunner>();

            // Runner calisiyorsa oyuncu modu secmis ve odaya baglanmistir.
            // Inis haritasi onizlemesi de araziyi gormek zorunda oldugu icin
            // dunyayi burada aciyoruz, "oyun basladi" aninda degil.
            if (_runner == null || !_runner.IsRunning)
                return;

            OpenWorld();
        }

        private void OpenWorld()
        {
            _worldOpened = true;
            SetActive(worldRoots, true);
            SetActive(menuOnlyRoots, false);
        }

        /// <summary>Editor kurulumu icin: listeye nesne ekler, kopya yaratmaz.</summary>
        public void RegisterWorldRoot(GameObject root)
        {
            if (root == null)
                return;

            if (worldRoots != null)
            {
                for (int i = 0; i < worldRoots.Length; i++)
                {
                    if (worldRoots[i] == root)
                        return;
                }
            }

            int length = worldRoots != null ? worldRoots.Length : 0;
            GameObject[] expanded = new GameObject[length + 1];

            for (int i = 0; i < length; i++)
                expanded[i] = worldRoots[i];

            expanded[length] = root;
            worldRoots = expanded;
        }

        private static void SetActive(GameObject[] objects, bool active)
        {
            if (objects == null)
                return;

            foreach (GameObject target in objects)
            {
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
            }
        }
    }
}
