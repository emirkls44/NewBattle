using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Haritayi dolduran cevre nesneleri: agaclar, calilar, uzun otlar, kayalar,
    /// binalar, citler ve varil/sandik gibi kucuk prop'lar.
    ///
    /// Her uretec bir "seed" alir; ayni prop'un farkli kopyalari birbirinden
    /// hafifce farkli olur, boylece harita kopyala-yapistir gorunmez.
    /// </summary>
    public static class PropFactory
    {
        #region Agaclar

        /// <summary>Yuvarlak tepeli low-poly agac. Referans gorsellerdeki ana agac tipi.</summary>
        public static Mesh BuildRoundTree(int seed, float scale = 1f)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            float trunkHeight = Mathf.Lerp(1.1f, 1.7f, (float)random.NextDouble()) * scale;
            float trunkRadius = 0.15f * scale;

            builder.AddCylinder(Vector3.zero, trunkRadius * 1.25f, trunkRadius, trunkHeight, 7,
                ToonPalette.Trunk);
            // Kok genislemesi: agacin zemine oturmasini inandirici kilar.
            builder.AddCylinder(Vector3.zero, trunkRadius * 1.8f, trunkRadius * 1.25f, 0.14f * scale, 7,
                ToonPalette.TrunkDark);

            int blobCount = random.Next(2, 4);
            float canopyBase = trunkHeight * 0.82f;

            for (int i = 0; i < blobCount; i++)
            {
                float radius = Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble()) * scale;
                float height = canopyBase + i * radius * 0.52f;
                float offsetX = ((float)random.NextDouble() - 0.5f) * 0.42f * scale;
                float offsetZ = ((float)random.NextDouble() - 0.5f) * 0.42f * scale;

                Color leaf = i == blobCount - 1
                    ? ToonPalette.Vary(ToonPalette.LeafLight, 0.08f, random)
                    : ToonPalette.Vary(ToonPalette.Leaf, 0.10f, random);

                builder.AddSphere(new Vector3(offsetX, height, offsetZ), radius * (1f - i * 0.16f),
                    8, 6, leaf, 0.12f, seed * 31 + i, new Vector3(1.05f, 0.88f, 1.05f));
            }

            return builder.Build($"Prop_Tree_{seed}");
        }

        /// <summary>Ince, uzun, konik agac (cam). Harita silueti icin cesitlilik.</summary>
        public static Mesh BuildPineTree(int seed, float scale = 1f)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            float trunkHeight = Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble()) * scale;
            builder.AddCylinder(Vector3.zero, 0.16f * scale, 0.11f * scale, trunkHeight, 6, ToonPalette.Trunk);

            int tiers = random.Next(3, 5);
            float tierHeight = 0.85f * scale;

            for (int i = 0; i < tiers; i++)
            {
                float t = i / (float)tiers;
                float radius = Mathf.Lerp(0.95f, 0.35f, t) * scale;
                float y = trunkHeight * 0.55f + i * tierHeight * 0.58f;
                Color leaf = ToonPalette.Vary(Color.Lerp(ToonPalette.LeafDark, ToonPalette.Leaf, t), 0.07f, random);
                builder.AddCone(new Vector3(0f, y, 0f), radius, tierHeight, 8, leaf);
            }

            return builder.Build($"Prop_Pine_{seed}");
        }

        #endregion

        #region Cali ve uzun ot (gizlenme)

        /// <summary>
        /// Gizlenme calisi: govdeden disari dogru acilan yaprak dilimleri.
        /// Referans gorseldeki, oyuncunun icine girip kaybolabildigi koyu yesil kume.
        /// </summary>
        public static Mesh BuildBush(int seed, float radius = 1.6f)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            int leafCount = Mathf.RoundToInt(radius * 11f);

            for (int i = 0; i < leafCount; i++)
            {
                float angle = (float)i / leafCount * Mathf.PI * 2f + (float)random.NextDouble() * 0.35f;
                float distance = radius * Mathf.Lerp(0.15f, 0.92f, (float)random.NextDouble());
                float height = Mathf.Lerp(0.18f, 0.55f, 1f - distance / radius);
                float leafLength = Mathf.Lerp(0.42f, 0.78f, (float)random.NextDouble());
                float tilt = Mathf.Lerp(8f, 38f, (float)random.NextDouble());

                builder.Push();
                builder.Translate(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
                builder.RotateEuler(0f, -angle * Mathf.Rad2Deg, 0f);
                builder.RotateEuler(-tilt, 0f, 0f);

                Color leafColor = ToonPalette.Vary(
                    distance > radius * 0.6f ? ToonPalette.BushLeaf : ToonPalette.BushLeafDark, 0.11f, random);

                // Tek yaprak: sivri uclu, hafif kalinligi olan dilim.
                float halfWidth = leafLength * 0.30f;
                builder.AddTriangle(new Vector3(-halfWidth, 0f, 0f), new Vector3(halfWidth, 0f, 0f),
                    new Vector3(0f, 0.02f, leafLength), leafColor);
                builder.AddTriangle(new Vector3(halfWidth, 0f, 0f), new Vector3(-halfWidth, 0f, 0f),
                    new Vector3(0f, -0.02f, leafLength), leafColor * 0.85f);

                builder.Pop();
            }

            // Govde golgesi: ortadaki koyu kume, calinin hacimli gorunmesini saglar.
            builder.AddSphere(new Vector3(0f, 0.22f, 0f), radius * 0.42f, 7, 5,
                ToonPalette.BushLeafDark, 0.15f, seed, new Vector3(1f, 0.6f, 1f));

            return builder.Build($"Prop_Bush_{seed}");
        }

        /// <summary>Uzun ot obegi: ince, yukari uzanan bicaklar. Gizlenme alanini isaretler.</summary>
        public static Mesh BuildTallGrass(int seed, float radius = 2.2f)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            int bladeCount = Mathf.RoundToInt(radius * radius * 9f);

            for (int i = 0; i < bladeCount; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = radius * Mathf.Sqrt((float)random.NextDouble());
                float height = Mathf.Lerp(0.55f, 1.05f, (float)random.NextDouble());
                float width = Mathf.Lerp(0.07f, 0.13f, (float)random.NextDouble());
                float lean = Mathf.Lerp(-16f, 16f, (float)random.NextDouble());

                builder.Push();
                builder.Translate(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                builder.RotateEuler(0f, (float)random.NextDouble() * 360f, lean);

                Color blade = ToonPalette.Vary(ToonPalette.GrassDark, 0.14f, random);
                Color tip = ToonPalette.Vary(ToonPalette.LeafLight, 0.10f, random);

                // Bicak: tabandan uca daralan, hafif kivrimli iki ucgen.
                builder.AddTriangle(new Vector3(-width, 0f, 0f), new Vector3(width, 0f, 0f),
                    new Vector3(width * 0.25f, height, 0.05f), blade);
                builder.AddTriangle(new Vector3(width, 0f, 0f), new Vector3(width * 0.25f, height, 0.05f),
                    new Vector3(-width, 0f, 0f), tip);

                builder.Pop();
            }

            return builder.Build($"Prop_TallGrass_{seed}");
        }

        #endregion

        #region Kayalar

        public static Mesh BuildRock(int seed, float size = 1f, bool orange = true)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            Color main = orange ? ToonPalette.RockOrange : ToonPalette.RockGrey;
            Color dark = orange ? ToonPalette.RockOrangeDark : ToonPalette.RockGreyDark;

            int blobs = random.Next(1, 4);
            for (int i = 0; i < blobs; i++)
            {
                float radius = size * Mathf.Lerp(0.45f, 0.85f, (float)random.NextDouble());
                Vector3 center = new(
                    ((float)random.NextDouble() - 0.5f) * size * 0.7f,
                    radius * 0.55f,
                    ((float)random.NextDouble() - 0.5f) * size * 0.7f);

                builder.AddSphere(center, radius, 6, 4,
                    ToonPalette.Vary(i == 0 ? main : dark, 0.08f, random),
                    0.42f, seed * 17 + i, new Vector3(1.15f, 0.82f, 1.0f));
            }

            return builder.Build($"Prop_Rock_{seed}");
        }

        /// <summary>Harita kenarindaki buyuk kayalik: dik, kose hatli bloklar.</summary>
        public static Mesh BuildCliff(int seed, float width = 6f, float height = 4f, float depth = 5f)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            int blocks = random.Next(3, 6);
            for (int i = 0; i < blocks; i++)
            {
                float blockWidth = width * Mathf.Lerp(0.42f, 0.8f, (float)random.NextDouble());
                float blockDepth = depth * Mathf.Lerp(0.42f, 0.8f, (float)random.NextDouble());
                float blockHeight = height * Mathf.Lerp(0.55f, 1f, (float)random.NextDouble());

                Vector3 center = new(
                    ((float)random.NextDouble() - 0.5f) * width * 0.55f,
                    blockHeight * 0.5f,
                    ((float)random.NextDouble() - 0.5f) * depth * 0.55f);

                builder.Push();
                builder.Translate(center);
                builder.RotateEuler(0f, (float)random.NextDouble() * 360f, 0f);
                builder.AddTaperedBox(Vector3.zero,
                    new Vector3(blockWidth, blockHeight, blockDepth),
                    new Vector2(blockWidth * Mathf.Lerp(0.55f, 0.85f, (float)random.NextDouble()),
                                blockDepth * Mathf.Lerp(0.55f, 0.85f, (float)random.NextDouble())),
                    ToonPalette.Vary(i % 2 == 0 ? ToonPalette.RockOrange : ToonPalette.RockOrangeDark, 0.07f, random));
                builder.Pop();
            }

            return builder.Build($"Prop_Cliff_{seed}");
        }

        #endregion

        #region Yapilar

        public struct BuildingSpec
        {
            public float Width;
            public float Depth;
            public float WallHeight;
            public Color Wall;
            public Color Roof;
            public bool TwoStorey;
        }

        /// <summary>
        /// Icine girilebilir basit ev: dort duvar, kapi boslugu, pencereler, besik cati.
        /// Duvarlar ayri parcalar halinde uretilir ki kapi boslugu gercek bir delik olsun
        /// (collider'lar da bu parcalara gore kurulur, oyuncu iceri girebilir).
        /// </summary>
        public static Mesh BuildHouse(int seed, BuildingSpec spec)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            float halfWidth = spec.Width * 0.5f;
            float halfDepth = spec.Depth * 0.5f;
            float wallThickness = 0.22f;
            float height = spec.WallHeight * (spec.TwoStorey ? 2f : 1f);

            const float doorWidth = 1.25f;
            const float doorHeight = 2.15f;

            // Zemin
            builder.AddBox(new Vector3(0f, 0.06f, 0f),
                new Vector3(spec.Width, 0.12f, spec.Depth), ToonPalette.WallConcrete);

            // Arka ve yan duvarlar (tam parca)
            builder.AddBox(new Vector3(0f, height * 0.5f, -halfDepth),
                new Vector3(spec.Width, height, wallThickness), spec.Wall);
            builder.AddBox(new Vector3(-halfWidth, height * 0.5f, 0f),
                new Vector3(wallThickness, height, spec.Depth), spec.Wall);
            builder.AddBox(new Vector3(halfWidth, height * 0.5f, 0f),
                new Vector3(wallThickness, height, spec.Depth), spec.Wall);

            // On duvar: kapi boslugu birakilarak uc parcaya bolunur.
            float sidePanel = (spec.Width - doorWidth) * 0.5f;
            builder.AddBox(new Vector3(-(doorWidth + sidePanel) * 0.5f, height * 0.5f, halfDepth),
                new Vector3(sidePanel, height, wallThickness), spec.Wall);
            builder.AddBox(new Vector3((doorWidth + sidePanel) * 0.5f, height * 0.5f, halfDepth),
                new Vector3(sidePanel, height, wallThickness), spec.Wall);
            builder.AddBox(new Vector3(0f, (doorHeight + height) * 0.5f, halfDepth),
                new Vector3(doorWidth, height - doorHeight, wallThickness), spec.Wall);

            // Kapi cercevesi
            builder.AddBox(new Vector3(0f, doorHeight, halfDepth + 0.02f),
                new Vector3(doorWidth + 0.16f, 0.14f, wallThickness + 0.06f), ToonPalette.DoorWood);

            // Pencereler
            AddWindow(builder, new Vector3(-halfWidth - 0.01f, spec.WallHeight * 0.58f, halfDepth * 0.45f),
                new Vector3(0.1f, 0.85f, 1.05f));
            AddWindow(builder, new Vector3(halfWidth + 0.01f, spec.WallHeight * 0.58f, -halfDepth * 0.45f),
                new Vector3(0.1f, 0.85f, 1.05f));
            AddWindow(builder, new Vector3(halfWidth * 0.45f, spec.WallHeight * 0.58f, -halfDepth - 0.01f),
                new Vector3(1.05f, 0.85f, 0.1f));

            if (spec.TwoStorey)
            {
                AddWindow(builder, new Vector3(-halfWidth - 0.01f, spec.WallHeight * 1.58f, 0f),
                    new Vector3(0.1f, 0.8f, 1.0f));
                AddWindow(builder, new Vector3(0f, spec.WallHeight * 1.58f, halfDepth + 0.01f),
                    new Vector3(1.0f, 0.8f, 0.1f));
            }

            // Cati: sacakli besik cati.
            float roofOverhang = 0.45f;
            builder.AddPitchedRoof(new Vector3(0f, height + spec.Width * 0.22f, 0f),
                new Vector3(spec.Width + roofOverhang * 2f, spec.Width * 0.44f, spec.Depth + roofOverhang * 2f),
                ToonPalette.Vary(spec.Roof, 0.05f, random));

            return builder.Build($"Prop_House_{seed}");
        }

        private static void AddWindow(LowPolyMeshBuilder builder, Vector3 center, Vector3 size)
        {
            builder.AddBox(center, size * 1.18f, ToonPalette.WallWood);
            builder.AddBox(center, size, ToonPalette.Window);
        }

        /// <summary>Krem renkli tas cit: referans gorsellerdeki harita siniri.</summary>
        public static Mesh BuildFenceSegment(float length = 4f)
        {
            LowPolyMeshBuilder builder = new();

            builder.AddBox(new Vector3(0f, 0.42f, 0f), new Vector3(length, 0.84f, 0.30f),
                ToonPalette.FenceWood);
            // Ust kapak tasi
            builder.AddBox(new Vector3(0f, 0.90f, 0f), new Vector3(length, 0.14f, 0.40f),
                ToonPalette.WallCream);
            // Direkler
            foreach (float x in new[] { -length * 0.5f, length * 0.5f })
            {
                builder.AddBox(new Vector3(x, 0.56f, 0f), new Vector3(0.40f, 1.12f, 0.44f),
                    ToonPalette.WallCream);
            }

            return builder.Build("Prop_Fence");
        }

        public static Mesh BuildCrate(int seed)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);
            float size = Mathf.Lerp(0.85f, 1.15f, (float)random.NextDouble());

            builder.AddBox(new Vector3(0f, size * 0.5f, 0f), Vector3.one * size,
                ToonPalette.Vary(ToonPalette.WallWood, 0.08f, random));

            // Cerceve cubuklari
            float edge = size * 0.5f + 0.015f;
            foreach (float y in new[] { size * 0.12f, size * 0.88f })
            {
                builder.AddBox(new Vector3(0f, y, edge), new Vector3(size, size * 0.12f, 0.04f), ToonPalette.TrunkDark);
                builder.AddBox(new Vector3(0f, y, -edge), new Vector3(size, size * 0.12f, 0.04f), ToonPalette.TrunkDark);
                builder.AddBox(new Vector3(edge, y, 0f), new Vector3(0.04f, size * 0.12f, size), ToonPalette.TrunkDark);
                builder.AddBox(new Vector3(-edge, y, 0f), new Vector3(0.04f, size * 0.12f, size), ToonPalette.TrunkDark);
            }

            return builder.Build($"Prop_Crate_{seed}");
        }

        public static Mesh BuildBarrel(int seed)
        {
            LowPolyMeshBuilder builder = new();
            System.Random random = new(seed);

            Color body = random.Next(2) == 0 ? ToonPalette.MedkitRed : ToonPalette.VestGreen;
            builder.AddCylinder(Vector3.zero, 0.38f, 0.38f, 1.05f, 10, ToonPalette.Vary(body, 0.06f, random));
            builder.AddCylinder(new Vector3(0f, 0.22f, 0f), 0.41f, 0.41f, 0.09f, 10, ToonPalette.Metal);
            builder.AddCylinder(new Vector3(0f, 0.72f, 0f), 0.41f, 0.41f, 0.09f, 10, ToonPalette.Metal);
            builder.AddCylinder(new Vector3(0f, 1.05f, 0f), 0.36f, 0.34f, 0.05f, 10, ToonPalette.Metal);

            return builder.Build($"Prop_Barrel_{seed}");
        }

        /// <summary>Kopru/iskele tahtasi, liman POI'si icin.</summary>
        public static Mesh BuildDock(float length = 8f, float width = 3f)
        {
            LowPolyMeshBuilder builder = new();

            builder.AddBox(new Vector3(0f, 0f, 0f), new Vector3(width, 0.22f, length), ToonPalette.WallWood);

            int postPairs = Mathf.Max(2, Mathf.RoundToInt(length / 2.5f));
            for (int i = 0; i < postPairs; i++)
            {
                float z = Mathf.Lerp(-length * 0.45f, length * 0.45f, i / (float)(postPairs - 1));
                foreach (float x in new[] { -width * 0.38f, width * 0.38f })
                {
                    builder.AddCylinder(new Vector3(x, -1.4f, z), 0.14f, 0.14f, 1.5f, 6, ToonPalette.TrunkDark);
                }
            }

            return builder.Build("Prop_Dock");
        }

        #endregion
    }
}
