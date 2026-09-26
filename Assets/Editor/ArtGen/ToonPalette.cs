using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Oyunun tum renk kimligi tek yerde. Uretecler asla elle renk yazmaz, hep buradan alir.
    /// Boylece "cimenler biraz daha sari olsun" gibi bir istek tek satirlik degisiklige iner.
    ///
    /// Palet, referans gorsellerdeki parlak-doygun toon paletine gore ayarlandi:
    /// sicak sari-yesil cimen, gri asfalt, krem duvar, koyu kiremit cati, turkuaz su.
    /// </summary>
    public static class ToonPalette
    {
        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }

        // --- Zemin ---
        public static readonly Color GrassLight = Hex("#9FD356");
        public static readonly Color Grass = Hex("#7FC13C");
        public static readonly Color GrassDark = Hex("#68A82F");
        public static readonly Color GrassShade = Hex("#579324");

        public static readonly Color Dirt = Hex("#C9A96B");
        public static readonly Color DirtDark = Hex("#AE8E53");
        public static readonly Color Sand = Hex("#E3CE9B");
        public static readonly Color Asphalt = Hex("#6F757C");
        public static readonly Color AsphaltLine = Hex("#E8E8E0");
        public static readonly Color Water = Hex("#2E8FC4");
        public static readonly Color WaterDeep = Hex("#1F6E9C");

        // --- Doga ---
        public static readonly Color LeafLight = Hex("#7CC53F");
        public static readonly Color Leaf = Hex("#5FA82C");
        public static readonly Color LeafDark = Hex("#3F7C1E");
        public static readonly Color BushLeaf = Hex("#4E8F27");
        public static readonly Color BushLeafDark = Hex("#3A6E1B");
        public static readonly Color Trunk = Hex("#8A5A33");
        public static readonly Color TrunkDark = Hex("#6B4426");

        public static readonly Color RockOrange = Hex("#D08453");
        public static readonly Color RockOrangeDark = Hex("#B26B3F");
        public static readonly Color RockGrey = Hex("#9BA3AA");
        public static readonly Color RockGreyDark = Hex("#7C858C");

        // --- Yapi ---
        public static readonly Color WallCream = Hex("#E0D2AC");
        public static readonly Color WallWood = Hex("#A9784C");
        public static readonly Color WallConcrete = Hex("#BFC3C7");
        public static readonly Color RoofRed = Hex("#8E3A32");
        public static readonly Color RoofSlate = Hex("#3C4046");
        public static readonly Color RoofBrown = Hex("#6B4A32");
        public static readonly Color Window = Hex("#4E7C96");
        public static readonly Color WindowLit = Hex("#F2C14E");
        public static readonly Color DoorWood = Hex("#7A4E2E");
        public static readonly Color FenceWood = Hex("#D8C89C");
        public static readonly Color Metal = Hex("#8D959C");

        // --- Loot ---
        // Yumusak stil: doygun ama "temiz" renkler. Koyu tonlar siyaha degil
        // ayni rengin daha koyusuna kayar; boylece toon golgesinde kirli durmaz.
        public static readonly Color AmmoGold = Hex("#FFC53D");
        public static readonly Color AmmoGoldDark = Hex("#E39B2B");
        public static readonly Color AmmoTip = Hex("#F08A3E");
        public static readonly Color MedkitRed = Hex("#FF5E57");
        public static readonly Color MedkitDark = Hex("#E0443F");
        public static readonly Color MedkitWhite = Hex("#FFFFFF");
        public static readonly Color ShieldSmall = Hex("#5CCBFF");
        public static readonly Color ShieldBig = Hex("#2F8CFF");
        public static readonly Color ShieldGlass = Hex("#DDF4FF");
        public static readonly Color PotionCork = Hex("#C98A55");
        public static readonly Color ScopeLens = Hex("#7FD4FF");

        public static readonly Color GunBody = Hex("#4A515B");
        public static readonly Color GunBodyLight = Hex("#6A737E");
        public static readonly Color GunAccent = Hex("#FF9F1C");
        public static readonly Color GunGrip = Hex("#30353C");
        public static readonly Color GunWood = Hex("#C98A55");
        public static readonly Color GunLegendary = Hex("#FFCE4A");
        public static readonly Color GunLegendaryDark = Hex("#E0A22A");

        public static readonly Color CrateGreen = Hex("#6FAF6A");
        public static readonly Color CrateGreenDark = Hex("#58935A");
        public static readonly Color CrateStrap = Hex("#C28A55");
        public static readonly Color CrateMetal = Hex("#E6EAEE");
        public static readonly Color CrateMark = Hex("#FFC53D");
        public static readonly Color ParachuteA = Hex("#A66BE6");
        public static readonly Color ParachuteB = Hex("#FFD35C");

        // --- Karakter ---
        public static readonly Color SkinLight = Hex("#F3C6A0");
        public static readonly Color SkinMid = Hex("#C68B5E");
        public static readonly Color SkinDark = Hex("#8D5A33");
        public static readonly Color HairBrown = Hex("#4E3423");
        public static readonly Color HairBlack = Hex("#25211E");
        public static readonly Color HairBlonde = Hex("#D9A64E");

        public static readonly Color ShirtWhite = Hex("#F2F2EE");
        public static readonly Color ShirtRed = Hex("#D8433A");
        public static readonly Color ShirtBlue = Hex("#3B76C4");
        public static readonly Color ShirtYellow = Hex("#EFC03B");
        public static readonly Color ShirtGreen = Hex("#5B8C3A");
        public static readonly Color PantsDark = Hex("#33373D");
        public static readonly Color PantsBlue = Hex("#3A4A63");
        public static readonly Color PantsKhaki = Hex("#8A7A52");
        public static readonly Color ShoeDark = Hex("#2A2D31");
        public static readonly Color VestGreen = Hex("#4F6138");
        public static readonly Color CapRed = Hex("#C6413A");
        public static readonly Color EyeDark = Hex("#2A2724");

        // --- UI / efekt ---
        public static readonly Color TeamBlue = Hex("#3FA9F5");
        public static readonly Color EnemyRed = Hex("#F2453D");
        public static readonly Color SelfWhite = Hex("#FFFFFF");
        public static readonly Color TracerYellow = Hex("#FFE066");
        public static readonly Color FootprintDark = Hex("#4A3D2A");

        /// <summary>Ayni prefab'in her kopyasi birbirinin tipatip ayni olmasin diye hafif renk kaydirma.</summary>
        public static Color Vary(Color color, float amount, System.Random random)
        {
            float delta = ((float)random.NextDouble() - 0.5f) * 2f * amount;
            Color.RGBToHSV(color, out float h, out float s, out float v);
            v = Mathf.Clamp01(v + delta);
            s = Mathf.Clamp01(s + delta * 0.35f);
            return Color.HSVToRGB(h, s, v);
        }
    }
}
