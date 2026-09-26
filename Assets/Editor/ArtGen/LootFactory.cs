using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Yerde duran toplanabilir esyalarin ve silahlarin modelleri.
    ///
    /// Stil: "yumusak low-poly". Kutular pahli, silindir ve kureler yumusak
    /// golgeli; oranlar hafif tombul. Keskin yuzlu eski modellerin yerine
    /// oyuncak gibi okunan, ustten bakinca silueti net sekiller.
    ///
    /// Olcu: gercek metre. Yerdeki boyut pickup prefabindaki olcekle ayarlanir
    /// (bkz. LootVisualSetup); ayni mesh karakterin elinde de kullanildigi icin
    /// modeli burada buyutmek elindeki silahi da buyuturdu.
    ///
    /// Tum silahlar +Z yonune bakar, namlu ucu +Z'dedir. Boylece karakterin
    /// WeaponSocket'ine dogrudan (identity rotation ile) takilabilirler.
    /// </summary>
    public static class LootFactory
    {
        public enum LootKind
        {
            AmmoPack,
            SmallShield,
            BigShield,
            MedKit,
            Pistol,
            Smg,
            Shotgun,
            AssaultRifle,
            Sniper,
            LegendaryRifle,
            AirdropCrate,
            Parachute
        }

        /// <summary>Yumusak yuzeylerin cevre bolumu. Ustten bakista cokgen gorunmeyecek kadar.</summary>
        private const int RoundSides = 14;

        public static Mesh Build(LootKind kind)
        {
            LowPolyMeshBuilder builder = new();

            switch (kind)
            {
                case LootKind.AmmoPack: BuildAmmoPack(builder); break;
                case LootKind.SmallShield: BuildShieldFlask(builder, 0.85f, ToonPalette.ShieldSmall); break;
                case LootKind.BigShield: BuildShieldFlask(builder, 1.15f, ToonPalette.ShieldBig); break;
                case LootKind.MedKit: BuildMedKit(builder); break;
                case LootKind.Pistol: BuildPistol(builder); break;
                case LootKind.Smg: BuildSmg(builder); break;
                case LootKind.Shotgun: BuildShotgun(builder); break;
                case LootKind.AssaultRifle: BuildAssaultRifle(builder); break;
                case LootKind.Sniper: BuildSniper(builder); break;
                case LootKind.LegendaryRifle: BuildLegendaryRifle(builder); break;
                case LootKind.AirdropCrate: BuildAirdropCrate(builder); break;
                case LootKind.Parachute: BuildParachute(builder); break;
            }

            return builder.Build($"Loot_{kind}");
        }

        #region Sarf malzemeleri

        /// <summary>Ayakta duran tombul mermi kumesi - referans gorsellerdeki sari demet.</summary>
        private static void BuildAmmoPack(LowPolyMeshBuilder builder)
        {
            const float radius = 0.036f;
            const float rimHeight = 0.018f;
            const float bodyHeight = 0.1f;

            // On sira 3, arka sira 2 mermi. Arka sira biraz uzun: ustten bakinca
            // kume tek bir blok gibi degil, tek tek mermi olarak okunur.
            (float x, float z, float extra)[] bullets =
            {
                (-0.075f, -0.035f, 0f),
                (0f, -0.035f, 0f),
                (0.075f, -0.035f, 0f),
                (-0.0375f, 0.035f, 0.015f),
                (0.0375f, 0.035f, 0.015f)
            };

            foreach ((float x, float z, float extra) in bullets)
            {
                Vector3 basePoint = new(x, 0f, z);
                float height = bodyHeight + extra;

                // Kovan dibindeki koyu halka formu okunakli kiliyor.
                builder.AddSmoothCylinder(basePoint, radius * 1.1f, radius * 1.1f, rimHeight, RoundSides,
                    ToonPalette.AmmoGoldDark);
                builder.AddSmoothCylinder(basePoint + Vector3.up * rimHeight, radius, radius, height, RoundSides,
                    ToonPalette.AmmoGold, capBottom: false, capTop: false);
                // Yuvarlak uc: kurenin alt yarisi govdenin icinde kalir.
                builder.AddSmoothSphere(basePoint + Vector3.up * (rimHeight + height), radius, RoundSides, 8,
                    ToonPalette.AmmoTip, new Vector3(1f, 1.6f, 1f));
            }
        }

        /// <summary>Tombul kalkan iksiri. scale ile kucuk/buyuk varyanti uretilir.</summary>
        private static void BuildShieldFlask(LowPolyMeshBuilder builder, float scale, Color liquid)
        {
            builder.Push();
            builder.Scale(scale);

            builder.AddSmoothSphere(new Vector3(0f, 0.1f, 0f), 0.1f, 16, 10, liquid, new Vector3(1f, 0.95f, 1f));
            // Parlama noktasi: kamera guneyden (-Z) bakiyor, isik oraya duser.
            builder.AddSmoothSphere(new Vector3(-0.045f, 0.145f, -0.062f), 0.018f, 10, 6, ToonPalette.MedkitWhite);

            builder.AddSmoothCylinder(new Vector3(0f, 0.18f, 0f), 0.036f, 0.033f, 0.06f, RoundSides,
                ToonPalette.ShieldGlass);
            builder.AddSmoothCylinder(new Vector3(0f, 0.235f, 0f), 0.043f, 0.043f, 0.04f, RoundSides,
                ToonPalette.PotionCork);

            builder.Pop();
        }

        private static void BuildMedKit(LowPolyMeshBuilder builder)
        {
            Vector3 size = new(0.28f, 0.16f, 0.2f);

            builder.AddRoundedBox(new Vector3(0f, size.y * 0.5f, 0f), size, 0.04f, ToonPalette.MedkitRed, 3);

            // Kapak cizgisi: govdeyi saran ince, biraz koyu serit.
            builder.AddRoundedBox(new Vector3(0f, size.y * 0.7f, 0f),
                new Vector3(size.x + 0.006f, 0.02f, size.z + 0.006f), 0.01f, ToonPalette.MedkitDark);

            // Ustte ve onde beyaz hac. Ustteki ustten bakista, ondeki kameranin
            // egik bakisinda gorunur.
            AddCross(builder, new Vector3(0f, size.y + 0.004f, 0f), horizontal: true, 0.12f, 0.036f);
            AddCross(builder, new Vector3(0f, size.y * 0.42f, -size.z * 0.5f - 0.004f), horizontal: false,
                0.066f, 0.022f);
        }

        private static void AddCross(LowPolyMeshBuilder builder, Vector3 center, bool horizontal,
            float length, float thickness)
        {
            const float depth = 0.014f;
            float radius = thickness * 0.35f;

            if (horizontal)
            {
                builder.AddRoundedBox(center, new Vector3(length, depth, thickness), radius, ToonPalette.MedkitWhite, 1);
                builder.AddRoundedBox(center, new Vector3(thickness, depth, length), radius, ToonPalette.MedkitWhite, 1);
            }
            else
            {
                builder.AddRoundedBox(center, new Vector3(length, thickness, depth), radius, ToonPalette.MedkitWhite, 1);
                builder.AddRoundedBox(center, new Vector3(thickness, length, depth), radius, ToonPalette.MedkitWhite, 1);
            }
        }

        #endregion

        #region Silahlar

        /// <summary>
        /// Tum silahlarin ortak govde kurgusu. Parametreler degistirilerek
        /// tabancadan keskin nisanci tufegine kadar butun aile uretilir.
        /// </summary>
        private static void BuildGun(LowPolyMeshBuilder builder,
            float bodyLength, float bodyHeight, float barrelLength, float barrelRadius,
            Color body, Color accent, bool hasStock, bool hasMagazine, bool hasScope,
            float magazineLength = 0.14f, float gripAngle = 18f)
        {
            const float bodyWidth = 0.066f;

            // Ana govde (receiver) ve ust kapak
            builder.AddRoundedBox(Vector3.zero, new Vector3(bodyWidth, bodyHeight, bodyLength), 0.02f, body);
            builder.AddRoundedBox(new Vector3(0f, bodyHeight * 0.42f, bodyLength * 0.1f),
                new Vector3(bodyWidth * 0.78f, bodyHeight * 0.34f, bodyLength * 0.55f), 0.012f,
                ToonPalette.GunBodyLight);

            // Namlu ve ucundaki renkli halka - gorsellerdeki turuncu detay.
            float barrelStart = bodyLength * 0.5f - 0.01f;
            builder.Push();
            builder.Translate(0f, bodyHeight * 0.1f, barrelStart);
            builder.RotateEuler(90f, 0f, 0f);
            builder.AddSmoothCylinder(Vector3.zero, barrelRadius, barrelRadius * 0.94f, barrelLength, RoundSides, body);
            builder.AddSmoothCylinder(new Vector3(0f, barrelLength * 0.82f, 0f), barrelRadius * 1.35f,
                barrelRadius * 1.35f, barrelLength * 0.18f + 0.008f, RoundSides, accent);
            builder.Pop();

            // Kabza
            builder.Push();
            builder.Translate(0f, -bodyHeight * 0.45f, -bodyLength * 0.22f);
            builder.RotateEuler(gripAngle, 0f, 0f);
            builder.AddRoundedBox(new Vector3(0f, -0.06f, 0f), new Vector3(bodyWidth * 0.82f, 0.13f, 0.058f), 0.02f,
                ToonPalette.GunGrip);
            builder.Pop();

            // Tetik korugu
            builder.AddRoundedBox(new Vector3(0f, -bodyHeight * 0.55f, -bodyLength * 0.06f),
                new Vector3(bodyWidth * 0.45f, 0.045f, 0.075f), 0.012f, ToonPalette.GunGrip, 1);

            if (hasMagazine)
            {
                builder.Push();
                builder.Translate(0f, -bodyHeight * 0.5f, bodyLength * 0.04f);
                builder.RotateEuler(-8f, 0f, 0f);
                builder.AddRoundedBox(new Vector3(0f, -magazineLength * 0.5f, 0f),
                    new Vector3(bodyWidth * 0.66f, magazineLength, 0.058f), 0.018f, accent);
                builder.Pop();
            }

            if (hasStock)
            {
                builder.AddRoundedBox(new Vector3(0f, -bodyHeight * 0.05f, -bodyLength * 0.5f - 0.075f),
                    new Vector3(bodyWidth * 0.8f, bodyHeight * 1.05f, 0.16f), 0.022f, body);
                builder.AddRoundedBox(new Vector3(0f, -bodyHeight * 0.08f, -bodyLength * 0.5f - 0.16f),
                    new Vector3(bodyWidth * 0.86f, bodyHeight * 1.3f, 0.035f), 0.012f, ToonPalette.GunGrip);
            }

            if (hasScope)
            {
                builder.AddRoundedBox(new Vector3(0f, bodyHeight * 0.72f, bodyLength * 0.12f),
                    new Vector3(bodyWidth * 0.35f, 0.04f, 0.06f), 0.01f, ToonPalette.GunGrip, 1);

                builder.Push();
                builder.Translate(0f, bodyHeight * 1.05f, bodyLength * 0.12f);
                builder.RotateEuler(90f, 0f, 0f);
                builder.AddSmoothCylinder(new Vector3(0f, -0.11f, 0f), 0.034f, 0.034f, 0.22f, RoundSides,
                    ToonPalette.GunGrip);
                // Mercek: durbunun on ucunde acik mavi disk.
                builder.AddSmoothCylinder(new Vector3(0f, 0.108f, 0f), 0.029f, 0.029f, 0.01f, RoundSides,
                    ToonPalette.ScopeLens);
                builder.Pop();
            }
            else
            {
                // Basit nisangah
                builder.AddRoundedBox(new Vector3(0f, bodyHeight * 0.66f, bodyLength * 0.38f),
                    new Vector3(0.016f, 0.036f, 0.018f), 0.006f, ToonPalette.GunGrip, 1);
            }
        }

        private static void BuildPistol(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.2f, 0.078f, 0.1f, 0.022f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: false, hasMagazine: false, hasScope: false, gripAngle: 22f);
        }

        private static void BuildSmg(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.26f, 0.084f, 0.16f, 0.023f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: false, hasMagazine: true, hasScope: false, magazineLength: 0.17f);
        }

        private static void BuildShotgun(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.3f, 0.088f, 0.3f, 0.031f,
                ToonPalette.GunBody, ToonPalette.GunWood,
                hasStock: true, hasMagazine: false, hasScope: false);

            // Cift namlu hissi veren alt tup ve pompa kolu
            builder.Push();
            builder.Translate(0f, -0.028f, 0.15f);
            builder.RotateEuler(90f, 0f, 0f);
            builder.AddSmoothCylinder(Vector3.zero, 0.025f, 0.025f, 0.26f, RoundSides, ToonPalette.GunBodyLight);
            builder.Pop();
            builder.AddRoundedBox(new Vector3(0f, -0.028f, 0.22f), new Vector3(0.062f, 0.054f, 0.1f), 0.02f,
                ToonPalette.GunWood);
        }

        private static void BuildAssaultRifle(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.34f, 0.088f, 0.26f, 0.024f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: true, hasMagazine: true, hasScope: false, magazineLength: 0.16f);

            // On el kundagi
            builder.AddRoundedBox(new Vector3(0f, 0.005f, 0.26f), new Vector3(0.054f, 0.056f, 0.14f), 0.018f,
                ToonPalette.GunBodyLight);
        }

        private static void BuildSniper(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.38f, 0.084f, 0.4f, 0.021f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: true, hasMagazine: true, hasScope: true, magazineLength: 0.11f);

            // Bipod
            foreach (float angle in new[] { 28f, -28f })
            {
                builder.Push();
                builder.Translate(0f, -0.04f, 0.42f);
                builder.RotateEuler(0f, 0f, angle);
                builder.AddSmoothCylinder(new Vector3(0f, -0.09f, 0f), 0.01f, 0.01f, 0.09f, 8, ToonPalette.GunGrip);
                builder.Pop();
            }
        }

        /// <summary>Airdrop kutusundan cikan efsanevi silah: altin govde, daha iri hatlar.</summary>
        private static void BuildLegendaryRifle(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.4f, 0.1f, 0.32f, 0.029f,
                ToonPalette.GunLegendary, ToonPalette.GunLegendaryDark,
                hasStock: true, hasMagazine: true, hasScope: true, magazineLength: 0.2f);

            // Govdeyi saran koyu bantlar, altin uzerinde kontrast yaratir.
            builder.AddRoundedBox(new Vector3(0f, 0f, 0.06f), new Vector3(0.072f, 0.108f, 0.03f), 0.012f,
                ToonPalette.GunGrip, 1);
            builder.AddRoundedBox(new Vector3(0f, 0f, -0.1f), new Vector3(0.072f, 0.108f, 0.03f), 0.012f,
                ToonPalette.GunGrip, 1);
        }

        #endregion

        #region Airdrop

        private static void BuildAirdropCrate(LowPolyMeshBuilder builder)
        {
            Vector3 size = new(0.95f, 0.62f, 0.78f);
            float bodyHeight = size.y * 0.66f;
            float lidHeight = size.y * 0.3f;
            float lidCenter = bodyHeight + lidHeight * 0.4f;

            builder.AddRoundedBox(new Vector3(0f, bodyHeight * 0.5f, 0f),
                new Vector3(size.x, bodyHeight, size.z), 0.07f, ToonPalette.CrateGreen, 3);

            // Kapak: govdeden biraz tasan, yumusak koseli kapak.
            builder.AddRoundedBox(new Vector3(0f, lidCenter, 0f),
                new Vector3(size.x * 1.03f, lidHeight, size.z * 1.03f), 0.07f, ToonPalette.CrateGreenDark, 3);

            // Kayislar ve tokalar
            foreach (float x in new[] { -size.x * 0.28f, size.x * 0.28f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    builder.AddRoundedBox(new Vector3(x, bodyHeight * 0.5f, side * (size.z * 0.5f + 0.01f)),
                        new Vector3(0.1f, bodyHeight * 0.98f, 0.03f), 0.012f, ToonPalette.CrateStrap, 1);
                }

                builder.AddRoundedBox(new Vector3(x, lidCenter + 0.01f, 0f),
                    new Vector3(0.1f, lidHeight * 1.02f, size.z * 1.05f), 0.02f, ToonPalette.CrateStrap);
                builder.AddRoundedBox(new Vector3(x, bodyHeight * 0.6f, -size.z * 0.5f - 0.028f),
                    new Vector3(0.07f, 0.07f, 0.025f), 0.01f, ToonPalette.CrateMetal, 1);
            }

            // Ust yuzeydeki isaret: kalin bir "!" - ustten bakista sandigin
            // tedarik paketi oldugu hemen okunur.
            float top = lidCenter + lidHeight * 0.5f + 0.004f;
            builder.AddRoundedBox(new Vector3(0f, top, 0.04f), new Vector3(0.1f, 0.02f, 0.24f), 0.01f,
                ToonPalette.CrateMark, 1);
            builder.AddRoundedBox(new Vector3(0f, top, -0.17f), new Vector3(0.1f, 0.02f, 0.09f), 0.01f,
                ToonPalette.CrateMark, 1);
        }

        private static void BuildParachute(LowPolyMeshBuilder builder)
        {
            const int segments = 12;
            float radius = 1.35f;
            float height = 0.85f;

            // Kubbe: ust yarim kure, alti acik.
            for (int i = 0; i < segments; i++)
            {
                float a0 = (float)i / segments * Mathf.PI * 2f;
                float a1 = (float)(i + 1) / segments * Mathf.PI * 2f;
                Color panel = i % 2 == 0 ? ToonPalette.ParachuteA : ToonPalette.ParachuteB;

                Vector3 rim0 = new(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius);
                Vector3 rim1 = new(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius);
                Vector3 mid0 = new(Mathf.Cos(a0) * radius * 0.72f, height * 0.62f, Mathf.Sin(a0) * radius * 0.72f);
                Vector3 mid1 = new(Mathf.Cos(a1) * radius * 0.72f, height * 0.62f, Mathf.Sin(a1) * radius * 0.72f);
                Vector3 apex = new(0f, height, 0f);

                builder.AddQuad(rim0, rim1, mid1, mid0, panel);
                builder.AddTriangle(mid0, mid1, apex, panel);

                // Ic yuz: kubbeye asagidan bakildiginda bos gorunmesin.
                builder.AddQuad(rim1, rim0, mid0, mid1, panel * 0.78f);
                builder.AddTriangle(mid1, mid0, apex, panel * 0.78f);

                // Ipler
                if (i % 3 == 0)
                {
                    Vector3 harness = new(0f, -1.15f, 0f);
                    Vector3 direction = (harness - rim0).normalized;
                    Vector3 side = Vector3.Cross(direction, Vector3.up).normalized * 0.012f;
                    builder.AddQuad(rim0 - side, rim0 + side, harness + side, harness - side,
                        ToonPalette.MedkitWhite);
                }
            }
        }

        #endregion
    }
}
