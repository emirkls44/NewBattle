using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Yerde duran toplanabilir esyalarin ve silahlarin low-poly modelleri.
    ///
    /// Tum silahlar +Z yonune bakacak sekilde, namlu ucu +Z'de olacak bicimde uretilir.
    /// Boylece karakterin elindeki WeaponSocket'e dogrudan (identity rotation ile)
    /// takilabilirler ve MuzzlePoint namlu ucuna denk gelir.
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

        public static Mesh Build(LootKind kind)
        {
            LowPolyMeshBuilder builder = new();

            switch (kind)
            {
                case LootKind.AmmoPack: BuildAmmoPack(builder); break;
                case LootKind.SmallShield: BuildShieldFlask(builder, 0.78f, ToonPalette.ShieldSmall); break;
                case LootKind.BigShield: BuildShieldFlask(builder, 1.12f, ToonPalette.ShieldBig); break;
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

        /// <summary>Ayakta duran altin mermi demeti - referans gorsellerdeki sari kume.</summary>
        private static void BuildAmmoPack(LowPolyMeshBuilder builder)
        {
            // 2 sira x 3 mermi, hafif kaydirmali dizilim.
            float bulletRadius = 0.035f;
            float bulletHeight = 0.115f;
            float spacing = 0.075f;

            for (int row = 0; row < 2; row++)
            {
                int columns = row == 0 ? 3 : 2;
                float z = row * spacing * 0.86f - 0.03f;

                for (int column = 0; column < columns; column++)
                {
                    float x = (column - (columns - 1) * 0.5f) * spacing;
                    Vector3 basePoint = new(x, 0f, z);

                    builder.AddCylinder(basePoint, bulletRadius, bulletRadius, bulletHeight, 8,
                        ToonPalette.AmmoGold);
                    builder.AddCylinder(basePoint + Vector3.up * bulletHeight,
                        bulletRadius, bulletRadius * 0.45f, 0.045f, 8, ToonPalette.AmmoTip);
                    // Kovan dibindeki koyu halka, formu okunakli kiliyor.
                    builder.AddCylinder(basePoint, bulletRadius * 1.08f, bulletRadius * 1.08f, 0.016f, 8,
                        ToonPalette.AmmoGoldDark);
                }
            }
        }

        /// <summary>Kalkan sisesi. scale ile kucuk/buyuk varyanti uretilir.</summary>
        private static void BuildShieldFlask(LowPolyMeshBuilder builder, float scale, Color liquid)
        {
            builder.Push();
            builder.Scale(scale);

            // Govde: asagisi genis, yukarisi daralan sise.
            builder.AddCylinder(new Vector3(0f, 0f, 0f), 0.085f, 0.098f, 0.055f, 9, ToonPalette.ShieldGlass);
            builder.AddCylinder(new Vector3(0f, 0.055f, 0f), 0.098f, 0.088f, 0.115f, 9, liquid);
            builder.AddCylinder(new Vector3(0f, 0.17f, 0f), 0.088f, 0.042f, 0.06f, 9, ToonPalette.ShieldGlass);
            // Boyun + mantar
            builder.AddCylinder(new Vector3(0f, 0.23f, 0f), 0.038f, 0.038f, 0.05f, 8, ToonPalette.ShieldGlass);
            builder.AddCylinder(new Vector3(0f, 0.275f, 0f), 0.046f, 0.042f, 0.035f, 8, ToonPalette.PotionCork);
            // Etiket
            builder.AddBox(new Vector3(0f, 0.10f, 0.094f), new Vector3(0.10f, 0.07f, 0.012f),
                ToonPalette.MedkitWhite);

            builder.Pop();
        }

        private static void BuildMedKit(LowPolyMeshBuilder builder)
        {
            Vector3 size = new(0.26f, 0.15f, 0.19f);

            builder.AddBox(new Vector3(0f, size.y * 0.5f, 0f), size, ToonPalette.MedkitRed);
            // Kapak ayrimi
            builder.AddBox(new Vector3(0f, size.y * 0.82f, 0f),
                new Vector3(size.x * 1.02f, 0.035f, size.z * 1.02f), ToonPalette.MedkitDark);
            // Ust ve on yuzdeki beyaz hac
            AddCross(builder, new Vector3(0f, size.y + 0.006f, 0f), Vector3.right, Vector3.forward, 0.10f, 0.03f);
            AddCross(builder, new Vector3(0f, size.y * 0.42f, size.z * 0.5f + 0.006f), Vector3.right, Vector3.up, 0.075f, 0.024f);
            // Sap
            builder.AddBox(new Vector3(0f, size.y + 0.03f, -size.z * 0.18f),
                new Vector3(0.09f, 0.045f, 0.022f), ToonPalette.MedkitDark);
        }

        private static void AddCross(LowPolyMeshBuilder builder, Vector3 center, Vector3 axisA, Vector3 axisB,
            float length, float thickness)
        {
            // Iki eksen boyunca birbirini kesen iki ince kutu = hac.
            Vector3 a = Abs(axisA);
            Vector3 b = Abs(axisB);
            Vector3 depth = Abs(Vector3.Cross(axisA, axisB).normalized) * 0.016f;

            builder.AddBox(center, a * length + b * thickness + depth, ToonPalette.MedkitWhite);
            builder.AddBox(center, a * thickness + b * length + depth, ToonPalette.MedkitWhite);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
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
            float bodyWidth = 0.062f;

            // Ana govde (receiver)
            builder.AddBox(new Vector3(0f, 0f, 0f), new Vector3(bodyWidth, bodyHeight, bodyLength), body);
            builder.AddBox(new Vector3(0f, bodyHeight * 0.42f, bodyLength * 0.1f),
                new Vector3(bodyWidth * 0.75f, bodyHeight * 0.3f, bodyLength * 0.55f), ToonPalette.GunBodyLight);

            // Namlu
            float barrelStart = bodyLength * 0.5f;
            builder.Push();
            builder.Translate(0f, bodyHeight * 0.1f, barrelStart);
            builder.RotateEuler(90f, 0f, 0f);
            builder.AddCylinder(Vector3.zero, barrelRadius, barrelRadius * 0.92f, barrelLength, 8, body);
            builder.Pop();

            // Namlu ucu aksani - gorsellerdeki turuncu detay
            builder.AddBox(new Vector3(0f, bodyHeight * 0.1f, barrelStart + barrelLength * 0.86f),
                new Vector3(barrelRadius * 2.5f, barrelRadius * 2.5f, barrelLength * 0.16f), accent);

            // Kabza
            builder.Push();
            builder.Translate(0f, -bodyHeight * 0.45f, -bodyLength * 0.22f);
            builder.RotateEuler(gripAngle, 0f, 0f);
            builder.AddTaperedBox(new Vector3(0f, -0.065f, 0f),
                new Vector3(bodyWidth * 0.82f, 0.14f, 0.055f),
                new Vector2(bodyWidth * 0.82f, 0.065f), ToonPalette.GunGrip);
            builder.Pop();

            // Tetik korugu
            builder.AddBox(new Vector3(0f, -bodyHeight * 0.55f, -bodyLength * 0.08f),
                new Vector3(bodyWidth * 0.5f, 0.05f, 0.075f), ToonPalette.GunGrip);

            if (hasMagazine)
            {
                builder.Push();
                builder.Translate(0f, -bodyHeight * 0.5f, bodyLength * 0.02f);
                builder.RotateEuler(-8f, 0f, 0f);
                builder.AddTaperedBox(new Vector3(0f, -magazineLength * 0.5f, 0f),
                    new Vector3(bodyWidth * 0.68f, magazineLength, 0.055f),
                    new Vector2(bodyWidth * 0.62f, 0.05f), accent);
                builder.Pop();
            }

            if (hasStock)
            {
                builder.AddTaperedBox(new Vector3(0f, -bodyHeight * 0.05f, -bodyLength * 0.5f - 0.075f),
                    new Vector3(bodyWidth * 0.8f, bodyHeight * 0.95f, 0.15f),
                    new Vector2(bodyWidth * 0.8f, bodyHeight * 1.25f), body);
                builder.AddBox(new Vector3(0f, -bodyHeight * 0.1f, -bodyLength * 0.5f - 0.155f),
                    new Vector3(bodyWidth * 0.85f, bodyHeight * 1.35f, 0.035f), ToonPalette.GunGrip);
            }

            if (hasScope)
            {
                builder.AddBox(new Vector3(0f, bodyHeight * 0.75f, bodyLength * 0.12f),
                    new Vector3(bodyWidth * 0.35f, 0.045f, 0.06f), ToonPalette.GunGrip);
                builder.Push();
                builder.Translate(0f, bodyHeight * 1.05f, bodyLength * 0.12f);
                builder.RotateEuler(90f, 0f, 0f);
                builder.AddCylinder(new Vector3(0f, -0.11f, 0f), 0.034f, 0.034f, 0.22f, 8, ToonPalette.GunGrip);
                builder.Pop();
                // Mercek: durbunun on ucunde mavi disk.
                builder.Push();
                builder.Translate(0f, bodyHeight * 1.05f, bodyLength * 0.12f + 0.105f);
                builder.RotateEuler(90f, 0f, 0f);
                builder.AddCylinder(Vector3.zero, 0.030f, 0.030f, 0.012f, 8, ToonPalette.Window);
                builder.Pop();
            }
            else
            {
                // Basit nisangah
                builder.AddBox(new Vector3(0f, bodyHeight * 0.68f, bodyLength * 0.38f),
                    new Vector3(0.012f, 0.038f, 0.014f), ToonPalette.GunGrip);
            }
        }

        private static void BuildPistol(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.20f, 0.075f, 0.10f, 0.021f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: false, hasMagazine: false, hasScope: false, gripAngle: 22f);
        }

        private static void BuildSmg(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.26f, 0.082f, 0.16f, 0.022f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: false, hasMagazine: true, hasScope: false, magazineLength: 0.17f);
        }

        private static void BuildShotgun(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.30f, 0.085f, 0.30f, 0.030f,
                ToonPalette.GunBody, ToonPalette.WallWood,
                hasStock: true, hasMagazine: false, hasScope: false);

            // Cift namlu hissi veren alt tup
            builder.Push();
            builder.Translate(0f, -0.028f, 0.15f);
            builder.RotateEuler(90f, 0f, 0f);
            builder.AddCylinder(Vector3.zero, 0.024f, 0.024f, 0.26f, 8, ToonPalette.GunBodyLight);
            builder.Pop();
            // Pompa kolu
            builder.AddBox(new Vector3(0f, -0.028f, 0.22f), new Vector3(0.058f, 0.05f, 0.09f), ToonPalette.WallWood);
        }

        private static void BuildAssaultRifle(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.34f, 0.085f, 0.26f, 0.023f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: true, hasMagazine: true, hasScope: false, magazineLength: 0.16f);

            // On el kundagi
            builder.AddBox(new Vector3(0f, 0.005f, 0.26f), new Vector3(0.05f, 0.052f, 0.14f),
                ToonPalette.GunBodyLight);
        }

        private static void BuildSniper(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.38f, 0.082f, 0.40f, 0.020f,
                ToonPalette.GunBody, ToonPalette.GunAccent,
                hasStock: true, hasMagazine: true, hasScope: true, magazineLength: 0.11f);

            // Bipod
            builder.Push();
            builder.Translate(0f, -0.04f, 0.42f);
            builder.RotateEuler(0f, 0f, 28f);
            builder.AddCylinder(new Vector3(0f, -0.09f, 0f), 0.009f, 0.009f, 0.09f, 6, ToonPalette.Metal);
            builder.Pop();
            builder.Push();
            builder.Translate(0f, -0.04f, 0.42f);
            builder.RotateEuler(0f, 0f, -28f);
            builder.AddCylinder(new Vector3(0f, -0.09f, 0f), 0.009f, 0.009f, 0.09f, 6, ToonPalette.Metal);
            builder.Pop();
        }

        /// <summary>Airdrop kutusundan cikan efsanevi silah: altin govde, daha iri hatlar.</summary>
        private static void BuildLegendaryRifle(LowPolyMeshBuilder builder)
        {
            BuildGun(builder, 0.40f, 0.098f, 0.32f, 0.028f,
                ToonPalette.GunLegendary, ToonPalette.GunLegendaryDark,
                hasStock: true, hasMagazine: true, hasScope: true, magazineLength: 0.20f);

            // Govdeyi saran koyu bantlar, altin uzerinde kontrast yaratir.
            builder.AddBox(new Vector3(0f, 0f, 0.06f), new Vector3(0.07f, 0.105f, 0.03f), ToonPalette.GunGrip);
            builder.AddBox(new Vector3(0f, 0f, -0.10f), new Vector3(0.07f, 0.105f, 0.03f), ToonPalette.GunGrip);
        }

        #endregion

        #region Airdrop

        private static void BuildAirdropCrate(LowPolyMeshBuilder builder)
        {
            Vector3 size = new(0.95f, 0.62f, 0.78f);
            float bodyHeight = size.y * 0.66f;

            // Govde
            builder.AddBox(new Vector3(0f, bodyHeight * 0.5f, 0f),
                new Vector3(size.x, bodyHeight, size.z), ToonPalette.CrateGreen);

            // Kapak: hafif kubbeli gorunsun diye iki kademeli kutu.
            builder.AddTaperedBox(new Vector3(0f, bodyHeight + size.y * 0.13f, 0f),
                new Vector3(size.x * 1.02f, size.y * 0.26f, size.z * 1.02f),
                new Vector2(size.x * 0.86f, size.z * 0.86f), ToonPalette.CrateGreenDark);

            // Kayislar
            foreach (float x in new[] { -size.x * 0.28f, size.x * 0.28f })
            {
                builder.AddBox(new Vector3(x, bodyHeight * 0.5f, size.z * 0.5f + 0.012f),
                    new Vector3(0.10f, bodyHeight * 1.02f, 0.03f), ToonPalette.CrateStrap);
                builder.AddBox(new Vector3(x, bodyHeight * 0.5f, -size.z * 0.5f - 0.012f),
                    new Vector3(0.10f, bodyHeight * 1.02f, 0.03f), ToonPalette.CrateStrap);
                builder.AddBox(new Vector3(x, bodyHeight + size.y * 0.13f, 0f),
                    new Vector3(0.10f, size.y * 0.30f, size.z * 1.04f), ToonPalette.CrateStrap);
                // Toka
                builder.AddBox(new Vector3(x, bodyHeight * 0.62f, size.z * 0.5f + 0.028f),
                    new Vector3(0.07f, 0.07f, 0.025f), ToonPalette.CrateMetal);
            }

            // Kose demirleri
            foreach (float x in new[] { -1f, 1f })
            {
                foreach (float z in new[] { -1f, 1f })
                {
                    builder.AddBox(new Vector3(x * size.x * 0.48f, bodyHeight * 0.5f, z * size.z * 0.48f),
                        new Vector3(0.06f, bodyHeight * 1.01f, 0.06f), ToonPalette.CrateMetal);
                }
            }

            // Ust yuzeydeki isaret: ucgen uc + govde = stilize "!" / hedef isareti.
            builder.AddBox(new Vector3(0f, bodyHeight + size.y * 0.27f, 0.02f),
                new Vector3(0.10f, 0.02f, 0.26f), ToonPalette.CrateMark);
            builder.AddBox(new Vector3(0f, bodyHeight + size.y * 0.27f, -0.20f),
                new Vector3(0.10f, 0.02f, 0.09f), ToonPalette.CrateMark);
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
