using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// "Bu noktanin altinda zemin var mi, yoksa en yakin zemin nerede?"
    ///
    /// NEDEN AYRI BIR SINIF: Inis noktasi, airdrop sandigi ve parasut inisi ayni
    /// soruyu soruyor ve uc yerde de ayni sessiz hata vardi - isin hicbir seye
    /// carpmazsa y = 0 donduruluyordu. Duz prototip arenada y = 0 zemindi, dolayisiyla
    /// hata gorunmuyordu. Gercek haritada y = 0 zeminin ALTI: oyuncu haritanin
    /// disini secince yerin altina konuluyor ve yercekimiyle sonsuza kadar dusuyor.
    ///
    /// Burada zemin bulunamazsa vazgecmiyoruz; cevreyi spiral tarayip EN YAKIN
    /// gecerli zemini buluyoruz.
    /// </summary>
    public static class GroundSampler
    {
        /// <summary>Isinin baslayacagi yukseklik. Haritanin en yuksek noktasindan yukarida olmali.</summary>
        private const float RayStartHeight = 500f;

        private const float RayLength = 1200f;

        /// <summary>Spiral aramada denenecek halka sayisi ve halka basina aci sayisi.</summary>
        private const int SearchRings = 12;
        private const int SearchAngles = 12;

        /// <summary>
        /// Verilen XZ noktasinin altindaki zemini bulur.
        /// Bulamazsa cevreyi tarar ve en yakin zemini dondurur.
        /// </summary>
        /// <param name="desired">Istenen konum (Y degeri onemsiz).</param>
        /// <param name="searchRadius">Zemin yoksa ne kadar genis aranacagi.</param>
        /// <param name="mask">Zemin sayilan katmanlar.</param>
        /// <param name="ground">Bulunan zemin noktasi.</param>
        /// <returns>Herhangi bir zemin bulunduysa true.</returns>
        public static bool TryFindGround(Vector3 desired, float searchRadius, LayerMask mask,
            out Vector3 ground)
        {
            if (TrySampleAt(desired.x, desired.z, mask, out ground))
                return true;

            // Istenen noktada zemin yok (harita disi, delik, su). Disari dogru
            // genisleyen halkalar halinde tarayip en yakin zemini buluyoruz.
            // Halkalar icten disa gidiyor, yani ilk bulunan ayni zamanda en yakini.
            for (int ring = 1; ring <= SearchRings; ring++)
            {
                float radius = searchRadius * ring / SearchRings;

                for (int step = 0; step < SearchAngles; step++)
                {
                    // Her halkada acilari kaydiriyoruz ki tarama izgara gibi
                    // duzenli olup kor noktalar birakmasin.
                    float angle = (step + ring * 0.5f) * Mathf.PI * 2f / SearchAngles;
                    float x = desired.x + Mathf.Cos(angle) * radius;
                    float z = desired.z + Mathf.Sin(angle) * radius;

                    if (TrySampleAt(x, z, mask, out ground))
                        return true;
                }
            }

            ground = desired;
            return false;
        }

        /// <summary>Tek nokta sorgusu; zemin yoksa false.</summary>
        public static bool TrySampleAt(float x, float z, LayerMask mask, out Vector3 ground)
        {
            Vector3 origin = new(x, RayStartHeight, z);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, RayLength, mask,
                    QueryTriggerInteraction.Ignore))
            {
                ground = hit.point;
                return true;
            }

            ground = new Vector3(x, 0f, z);
            return false;
        }

        /// <summary>
        /// Zemin yuksekligi; bulunamazsa fallback doner.
        /// Cagiranin "zemin var miydi" bilgisine ihtiyaci yoksa bu yeterli.
        /// </summary>
        public static float HeightAt(float x, float z, LayerMask mask, float fallback = 0f)
        {
            return TrySampleAt(x, z, mask, out Vector3 ground) ? ground.y : fallback;
        }
    }
}
