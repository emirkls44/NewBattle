using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Battle royale adasini kuran ana uretec.
    ///
    /// Akis:
    ///   1. Yukseklik alani hesaplanir (ada kubbesi + gurultu, kiyida suya iner).
    ///   2. Yollar ve POI zeminleri arazi yuksekligini duzler, boylece binalar
    ///      egimli zemine oturmaz ve yollar gercekten duz gorunur.
    ///   3. Arazi parcalar (chunk) halinde mesh'lenir - her parca ayri mesh olunca
    ///      kamera disinda kalanlar culling ile elenir ve 65k vertex limiti asilmaz.
    ///   4. POI'ler (koy, ciftlik, liman, kayalik) yerlestirilir.
    ///   5. Kalan alana agac / cali / uzun ot / kaya serpistirilir.
    /// </summary>
    public static class IslandFactory
    {
        public class IslandSettings
        {
            public int Seed = 20260915;
            public float PlayableRadius = 180f;   // Oyuncularin dolasabildigi alan
            public float BeachWidth = 22f;        // Kum seridi
            public float WaterMargin = 60f;       // Kiyi disindaki su alani
            public float MaxHeight = 9f;          // Tepe yuksekligi
            public int ChunkCount = 6;            // ChunkCount x ChunkCount parca
            public int CellsPerChunk = 26;        // Parca basina izgara cozunurlugu
            public float TreeDensity = 1f;
            public float BushDensity = 1f;
            public float RockDensity = 1f;
        }

        private class Road
        {
            public Vector2 A;
            public Vector2 B;
            public float Width;
        }

        private class Poi
        {
            public Vector2 Center;
            public float Radius;
            public float GroundHeight;

            /// <summary>Ayni seed ile ayni adanin cikmasi icin POI'ye ozgu sabit tohum.</summary>
            public int Seed;
        }

        private static IslandSettings _settings;
        private static List<Road> _roads;
        private static List<Poi> _pois;
        private static float[,] _noiseOffsets;

        public static GameObject Generate(IslandSettings settings, Material toonMaterial)
        {
            _settings = settings;
            System.Random random = new(settings.Seed);

            PrepareNoise(random);
            LayoutPoisAndRoads(random);

            GameObject root = new("Island");

            GameObject terrainRoot = new("Terrain");
            terrainRoot.transform.SetParent(root.transform, false);
            BuildTerrainChunks(terrainRoot.transform, toonMaterial);
            BuildWaterPlane(root.transform, toonMaterial);

            GameObject propsRoot = new("Props");
            propsRoot.transform.SetParent(root.transform, false);

            BuildPois(propsRoot.transform, toonMaterial, random);
            ScatterNature(propsRoot.transform, toonMaterial, random);
            BuildBorderCliffs(propsRoot.transform, toonMaterial, random);

            return root;
        }

        #region Yukseklik alani

        private static void PrepareNoise(System.Random random)
        {
            // Birkac oktav icin rastgele ofsetler. Perlin'i sabit tohumla kaydirarak
            // her ada uretiminde farkli bir sekil elde ediyoruz.
            _noiseOffsets = new float[4, 2];
            for (int i = 0; i < 4; i++)
            {
                _noiseOffsets[i, 0] = (float)random.NextDouble() * 1000f;
                _noiseOffsets[i, 1] = (float)random.NextDouble() * 1000f;
            }
        }

        public static float SampleHeight(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            float shoreRadius = _settings.PlayableRadius + _settings.BeachWidth;

            // Ada profili: merkezde yuksek, kiyida sifir, kiyinin disinda su altinda.
            float falloff = 1f - Mathf.Clamp01(distance / shoreRadius);
            falloff = Mathf.SmoothStep(0f, 1f, falloff);

            float noise = 0f;
            float amplitude = 1f;
            float frequency = 0.0055f;
            float totalAmplitude = 0f;

            for (int octave = 0; octave < 4; octave++)
            {
                noise += amplitude * Mathf.PerlinNoise(
                    x * frequency + _noiseOffsets[octave, 0],
                    z * frequency + _noiseOffsets[octave, 1]);
                totalAmplitude += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.1f;
            }

            noise = noise / totalAmplitude;
            float height = falloff * _settings.MaxHeight * Mathf.Lerp(0.35f, 1f, noise);

            // Kiyinin disinda deniz tabanina in.
            if (distance > shoreRadius)
            {
                float submerge = (distance - shoreRadius) / _settings.WaterMargin;
                height = -Mathf.Lerp(0.6f, 5f, Mathf.Clamp01(submerge));
            }

            // Yollar ve POI zeminleri araziyi duzler.
            height = FlattenForRoads(x, z, height);
            height = FlattenForPois(x, z, height);

            return height;
        }

        private static float FlattenForRoads(float x, float z, float height)
        {
            Vector2 point = new(x, z);

            foreach (Road road in _roads)
            {
                float distance = DistanceToSegment(point, road.A, road.B);
                if (distance > road.Width * 1.9f)
                    continue;

                // Yol ekseninde tam duz, kenara dogru araziye yumusak gecis.
                float roadHeight = Mathf.Lerp(
                    RawHeight(road.A.x, road.A.y),
                    RawHeight(road.B.x, road.B.y),
                    ProjectOnSegment(point, road.A, road.B));

                float blend = 1f - Mathf.SmoothStep(road.Width * 0.75f, road.Width * 1.9f, distance);
                height = Mathf.Lerp(height, roadHeight, blend);
            }

            return height;
        }

        private static float FlattenForPois(float x, float z, float height)
        {
            Vector2 point = new(x, z);

            foreach (Poi poi in _pois)
            {
                float distance = Vector2.Distance(point, poi.Center);
                if (distance > poi.Radius * 1.6f)
                    continue;

                float blend = 1f - Mathf.SmoothStep(poi.Radius, poi.Radius * 1.6f, distance);
                height = Mathf.Lerp(height, poi.GroundHeight, blend);
            }

            return height;
        }

        /// <summary>Yol/POI duzlestirmesi uygulanmamis ham yukseklik - referans kotu icin.</summary>
        private static float RawHeight(float x, float z)
        {
            float distance = Mathf.Sqrt(x * x + z * z);
            float shoreRadius = _settings.PlayableRadius + _settings.BeachWidth;
            float falloff = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(distance / shoreRadius));

            float noise = Mathf.PerlinNoise(x * 0.0055f + _noiseOffsets[0, 0], z * 0.0055f + _noiseOffsets[0, 1]);
            return falloff * _settings.MaxHeight * Mathf.Lerp(0.35f, 1f, noise);
        }

        #endregion

        #region Zemin rengi

        private static Color SampleGroundColor(float x, float z)
        {
            Vector2 point = new(x, z);
            float distance = point.magnitude;

            // Yol
            foreach (Road road in _roads)
            {
                float roadDistance = DistanceToSegment(point, road.A, road.B);
                if (roadDistance < road.Width * 0.5f)
                    return ToonPalette.Asphalt;
                if (roadDistance < road.Width * 0.72f)
                    return ToonPalette.DirtDark;
            }

            // POI zemini (koy meydani, ciftlik topragi)
            foreach (Poi poi in _pois)
            {
                if (Vector2.Distance(point, poi.Center) < poi.Radius * 0.92f)
                    return ToonPalette.Dirt;
            }

            float shoreRadius = _settings.PlayableRadius + _settings.BeachWidth;

            if (distance > shoreRadius - 2f)
                return ToonPalette.Sand;

            if (distance > _settings.PlayableRadius)
                return Color.Lerp(ToonPalette.Grass, ToonPalette.Sand,
                    Mathf.InverseLerp(_settings.PlayableRadius, shoreRadius - 2f, distance));

            // Cimen tonlari: yukseklige ve dusuk frekansli gurultuye gore alacali.
            float variation = Mathf.PerlinNoise(x * 0.035f + 13.7f, z * 0.035f + 91.2f);
            float height = SampleHeight(x, z);
            Color grass = variation < 0.42f
                ? ToonPalette.Grass
                : variation < 0.72f ? ToonPalette.GrassLight : ToonPalette.GrassDark;

            if (height > _settings.MaxHeight * 0.72f)
                grass = Color.Lerp(grass, ToonPalette.GrassShade, 0.45f);

            return grass;
        }

        #endregion

        #region Arazi mesh

        private static void BuildTerrainChunks(Transform parent, Material material)
        {
            float shoreRadius = _settings.PlayableRadius + _settings.BeachWidth;
            float extent = shoreRadius + _settings.WaterMargin;
            float chunkSize = extent * 2f / _settings.ChunkCount;

            for (int cz = 0; cz < _settings.ChunkCount; cz++)
            {
                for (int cx = 0; cx < _settings.ChunkCount; cx++)
                {
                    float originX = -extent + cx * chunkSize;
                    float originZ = -extent + cz * chunkSize;

                    LowPolyMeshBuilder builder = new();
                    builder.AddGrid(
                        new Vector3(originX, 0f, originZ),
                        chunkSize, chunkSize,
                        _settings.CellsPerChunk, _settings.CellsPerChunk,
                        SampleHeight,
                        SampleGroundColor);

                    Mesh mesh = builder.Build($"Terrain_{cx}_{cz}");
                    mesh = ArtGenIO.SaveMesh(mesh, $"Terrain_{cx}_{cz}", "Terrain");

                    GameObject chunk = ArtGenIO.CreateMeshObject($"Chunk_{cx}_{cz}", mesh, material, parent);
                    chunk.isStatic = true;

                    MeshCollider collider = chunk.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh;
                }
            }
        }

        private static void BuildWaterPlane(Transform parent, Material material)
        {
            float extent = _settings.PlayableRadius + _settings.BeachWidth + _settings.WaterMargin;

            LowPolyMeshBuilder builder = new();
            // Su yuzeyi: hafif dalgali izgara, iki tonlu.
            builder.AddGrid(new Vector3(-extent, -0.35f, -extent), extent * 2f, extent * 2f, 20, 20,
                (x, z) => Mathf.PerlinNoise(x * 0.02f, z * 0.02f) * 0.22f,
                (x, z) => Mathf.PerlinNoise(x * 0.012f + 4f, z * 0.012f + 4f) > 0.5f
                    ? ToonPalette.Water
                    : ToonPalette.WaterDeep);

            Mesh mesh = ArtGenIO.SaveMesh(builder.Build("Water"), "Water", "Terrain");
            GameObject water = ArtGenIO.CreateMeshObject("Water", mesh, material, parent);
            water.isStatic = true;
            water.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        #endregion

        #region POI ve yollar

        private static void LayoutPoisAndRoads(System.Random random)
        {
            _pois = new List<Poi>();
            _roads = new List<Road>();

            float r = _settings.PlayableRadius;

            // Dort ana POI'yi merkeze gore dagit; arada bir de merkez meydani.
            Vector2[] centers =
            {
                new(-r * 0.46f, r * 0.34f),   // Koy
                new(r * 0.48f, r * 0.28f),    // Ciftlik
                new(r * 0.30f, -r * 0.52f),   // Liman
                new(-r * 0.38f, -r * 0.44f),  // Kayalik kamp
                new(0f, 0f)                   // Merkez meydan
            };

            float[] radii = { 34f, 30f, 26f, 22f, 26f };

            for (int i = 0; i < centers.Length; i++)
            {
                _pois.Add(new Poi
                {
                    Center = centers[i],
                    Radius = radii[i],
                    GroundHeight = RawHeight(centers[i].x, centers[i].y),
                    Seed = _settings.Seed + (i + 1) * 9173
                });
            }

            // Merkezi her POI'ye baglayan yollar + cevre halkasi.
            for (int i = 0; i < 4; i++)
            {
                _roads.Add(new Road { A = centers[4], B = centers[i], Width = 7f });
                _roads.Add(new Road { A = centers[i], B = centers[(i + 1) % 4], Width = 5f });
            }
        }

        private static void BuildPois(Transform parent, Material material, System.Random random)
        {
            BuildVillage(parent, material, random, _pois[0]);
            BuildFarm(parent, material, random, _pois[1]);
            BuildHarbor(parent, material, random, _pois[2]);
            BuildRockCamp(parent, material, random, _pois[3]);
            BuildTownSquare(parent, material, random, _pois[4]);
        }

        private static void BuildVillage(Transform parent, Material material, System.Random random, Poi poi)
        {
            GameObject group = NewGroup("Village", parent, poi);

            int houseCount = 6;
            for (int i = 0; i < houseCount; i++)
            {
                float angle = (float)i / houseCount * Mathf.PI * 2f + 0.4f;
                float distance = poi.Radius * Mathf.Lerp(0.42f, 0.78f, (float)random.NextDouble());

                PropFactory.BuildingSpec spec = new()
                {
                    Width = Mathf.Lerp(6f, 9f, (float)random.NextDouble()),
                    Depth = Mathf.Lerp(6f, 8f, (float)random.NextDouble()),
                    WallHeight = 3.1f,
                    Wall = random.Next(2) == 0 ? ToonPalette.WallCream : ToonPalette.WallWood,
                    Roof = random.Next(2) == 0 ? ToonPalette.RoofRed : ToonPalette.RoofSlate,
                    TwoStorey = random.Next(3) == 0
                };

                Mesh mesh = ArtGenIO.SaveMesh(PropFactory.BuildHouse(poi.Seed + i, spec),
                    $"House_Village_{i}", "Buildings");

                Vector3 position = ToWorld(poi.Center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
                GameObject house = ArtGenIO.CreateMeshObject($"House_{i}", mesh, material, group.transform);
                house.transform.position = position;
                house.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
                house.isStatic = true;
                AddMeshCollider(house, mesh);
            }

            ScatterProps(group.transform, material, random, poi.Center, poi.Radius * 0.95f, 5,
                seed => PropFactory.BuildCrate(seed), "Crate", 0.9f);
        }

        private static void BuildFarm(Transform parent, Material material, System.Random random, Poi poi)
        {
            GameObject group = NewGroup("Farm", parent, poi);

            PropFactory.BuildingSpec barnSpec = new()
            {
                Width = 12f,
                Depth = 9f,
                WallHeight = 4.2f,
                Wall = ToonPalette.MedkitDark,
                Roof = ToonPalette.RoofBrown,
                TwoStorey = false
            };

            Mesh barn = ArtGenIO.SaveMesh(PropFactory.BuildHouse(poi.Seed, barnSpec), "Barn", "Buildings");
            GameObject barnObject = ArtGenIO.CreateMeshObject("Barn", barn, material, group.transform);
            barnObject.transform.position = ToWorld(poi.Center);
            barnObject.isStatic = true;
            AddMeshCollider(barnObject, barn);

            // Tarla siralari: alcak, duzenli bitki kumeleri.
            Mesh cropMesh = ArtGenIO.SaveMesh(PropFactory.BuildBush(poi.Seed + 77, 0.85f),
                "Crop", "Nature");

            for (int row = 0; row < 5; row++)
            {
                for (int column = 0; column < 7; column++)
                {
                    Vector2 local = new((column - 3) * 2.6f, -14f - row * 2.8f);
                    GameObject crop = ArtGenIO.CreateMeshObject($"Crop_{row}_{column}", cropMesh, material,
                        group.transform);
                    crop.transform.position = ToWorld(poi.Center + local);
                    crop.transform.localScale = Vector3.one * 0.75f;
                    crop.isStatic = true;
                }
            }
        }

        private static void BuildHarbor(Transform parent, Material material, System.Random random, Poi poi)
        {
            GameObject group = NewGroup("Harbor", parent, poi);

            Mesh dock = ArtGenIO.SaveMesh(PropFactory.BuildDock(14f, 4f), "Dock", "Buildings");
            Vector2 outward = poi.Center.normalized;

            GameObject dockObject = ArtGenIO.CreateMeshObject("Dock", dock, material, group.transform);
            dockObject.transform.position = ToWorld(poi.Center + outward * (poi.Radius * 0.9f));
            dockObject.transform.rotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.y));
            dockObject.isStatic = true;
            AddMeshCollider(dockObject, dock);

            PropFactory.BuildingSpec warehouseSpec = new()
            {
                Width = 11f,
                Depth = 8f,
                WallHeight = 4f,
                Wall = ToonPalette.WallConcrete,
                Roof = ToonPalette.RoofSlate,
                TwoStorey = false
            };

            Mesh warehouse = ArtGenIO.SaveMesh(PropFactory.BuildHouse(poi.Seed + 5, warehouseSpec),
                "Warehouse", "Buildings");
            GameObject warehouseObject = ArtGenIO.CreateMeshObject("Warehouse", warehouse, material, group.transform);
            warehouseObject.transform.position = ToWorld(poi.Center - outward * 6f);
            warehouseObject.isStatic = true;
            AddMeshCollider(warehouseObject, warehouse);

            ScatterProps(group.transform, material, random, poi.Center, poi.Radius * 0.8f, 9,
                seed => PropFactory.BuildBarrel(seed), "Barrel", 1f);
        }

        private static void BuildRockCamp(Transform parent, Material material, System.Random random, Poi poi)
        {
            GameObject group = NewGroup("RockCamp", parent, poi);

            for (int i = 0; i < 5; i++)
            {
                Mesh cliff = ArtGenIO.SaveMesh(
                    PropFactory.BuildCliff(poi.Seed + i, 9f, 6f, 8f), $"Cliff_{i}", "Nature");

                float angle = (float)i / 5f * Mathf.PI * 2f;
                Vector2 local = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * poi.Radius * 0.72f;

                GameObject cliffObject = ArtGenIO.CreateMeshObject($"Cliff_{i}", cliff, material, group.transform);
                cliffObject.transform.position = ToWorld(poi.Center + local);
                cliffObject.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                cliffObject.isStatic = true;
                AddMeshCollider(cliffObject, cliff);
            }

            ScatterProps(group.transform, material, random, poi.Center, poi.Radius * 0.9f, 6,
                seed => PropFactory.BuildCrate(seed), "Crate", 1f);
        }

        private static void BuildTownSquare(Transform parent, Material material, System.Random random, Poi poi)
        {
            GameObject group = NewGroup("TownSquare", parent, poi);

            for (int i = 0; i < 4; i++)
            {
                PropFactory.BuildingSpec spec = new()
                {
                    Width = 10f,
                    Depth = 8f,
                    WallHeight = 3.4f,
                    Wall = ToonPalette.WallConcrete,
                    Roof = ToonPalette.RoofSlate,
                    TwoStorey = true
                };

                Mesh mesh = ArtGenIO.SaveMesh(PropFactory.BuildHouse(poi.Seed + 100 + i, spec),
                    $"House_Square_{i}", "Buildings");

                float angle = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Vector2 local = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * poi.Radius * 0.80f;

                GameObject house = ArtGenIO.CreateMeshObject($"Block_{i}", mesh, material, group.transform);
                house.transform.position = ToWorld(poi.Center + local);
                house.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f);
                house.isStatic = true;
                AddMeshCollider(house, mesh);
            }
        }

        #endregion

        #region Doga serpistirme

        private static void ScatterNature(Transform parent, Material material, System.Random random)
        {
            GameObject natureRoot = new("Nature");
            natureRoot.transform.SetParent(parent, false);

            // Mesh havuzlari: ayni mesh'i tekrar kullanip sadece transform degistiriyoruz.
            Mesh[] trees = new Mesh[6];
            for (int i = 0; i < trees.Length; i++)
            {
                Mesh source = i % 3 == 2
                    ? PropFactory.BuildPineTree(_settings.Seed + i, Mathf.Lerp(0.9f, 1.25f, i / 6f))
                    : PropFactory.BuildRoundTree(_settings.Seed + i, Mathf.Lerp(0.9f, 1.3f, i / 6f));
                trees[i] = ArtGenIO.SaveMesh(source, $"Tree_{i}", "Nature");
            }

            Mesh[] bushes = new Mesh[4];
            for (int i = 0; i < bushes.Length; i++)
                bushes[i] = ArtGenIO.SaveMesh(PropFactory.BuildBush(_settings.Seed + 50 + i, 1.5f + i * 0.25f),
                    $"Bush_{i}", "Nature");

            Mesh[] grassPatches = new Mesh[3];
            for (int i = 0; i < grassPatches.Length; i++)
                grassPatches[i] = ArtGenIO.SaveMesh(
                    PropFactory.BuildTallGrass(_settings.Seed + 80 + i, 2.4f + i * 0.5f),
                    $"TallGrass_{i}", "Nature");

            Mesh[] rocks = new Mesh[4];
            for (int i = 0; i < rocks.Length; i++)
                rocks[i] = ArtGenIO.SaveMesh(PropFactory.BuildRock(_settings.Seed + 120 + i, 0.8f + i * 0.35f, i % 2 == 0),
                    $"Rock_{i}", "Nature");

            ScatterGroup(natureRoot.transform, material, random, trees, "Tree",
                Mathf.RoundToInt(260 * _settings.TreeDensity), 4.5f, true, false);

            ScatterGroup(natureRoot.transform, material, random, bushes, "Bush",
                Mathf.RoundToInt(150 * _settings.BushDensity), 3.5f, false, true);

            ScatterGroup(natureRoot.transform, material, random, grassPatches, "TallGrass",
                Mathf.RoundToInt(90 * _settings.BushDensity), 5f, false, true);

            ScatterGroup(natureRoot.transform, material, random, rocks, "Rock",
                Mathf.RoundToInt(110 * _settings.RockDensity), 3f, true, false);
        }

        /// <summary>
        /// Reddetme ornekleme ile prop dagitir: yola, POI merkezine veya suya denk gelen
        /// adaylar elenir. Boylece binalarin icine agac dikilmez, yollar kapanmaz.
        /// </summary>
        private static void ScatterGroup(Transform parent, Material material, System.Random random,
            Mesh[] meshes, string groupName, int count, float clearance, bool solid, bool hideVolume)
        {
            GameObject group = new(groupName);
            group.transform.SetParent(parent, false);

            float maxRadius = _settings.PlayableRadius + _settings.BeachWidth * 0.35f;
            int placed = 0;
            int attempts = 0;
            int maxAttempts = count * 18;

            while (placed < count && attempts < maxAttempts)
            {
                attempts++;

                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = maxRadius * Mathf.Sqrt((float)random.NextDouble());
                Vector2 point = new(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);

                if (IsOnRoad(point, clearance) || IsInsidePoiCore(point))
                    continue;

                float height = SampleHeight(point.x, point.y);
                if (height < 0.35f)
                    continue; // Su veya kum kiyisi: prop koyma.

                Mesh mesh = meshes[random.Next(meshes.Length)];
                GameObject instance = ArtGenIO.CreateMeshObject($"{groupName}_{placed}", mesh, material,
                    group.transform);

                instance.transform.position = new Vector3(point.x, height, point.y);
                instance.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                instance.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.2f, (float)random.NextDouble());
                instance.isStatic = true;

                if (solid)
                {
                    // Agac ve kayalar icin ucuz kapsul/kure collider: MeshCollider israf olur.
                    Bounds bounds = mesh.bounds;
                    CapsuleCollider collider = instance.AddComponent<CapsuleCollider>();
                    collider.radius = Mathf.Max(0.35f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.55f);
                    collider.height = bounds.size.y;
                    collider.center = new Vector3(0f, bounds.size.y * 0.5f, 0f);
                }

                if (hideVolume)
                    AddHideVolume(instance, mesh);

                placed++;
            }
        }

        /// <summary>
        /// Cali ve uzun ot obeklerine, oyuncunun gizlenmesini tetikleyen trigger collider ekler.
        /// PlayerHide bu collider'i "Bush" etiketiyle tanir.
        /// </summary>
        private static void AddHideVolume(GameObject instance, Mesh mesh)
        {
            Bounds bounds = mesh.bounds;

            GameObject volume = new("HideVolume");
            volume.transform.SetParent(instance.transform, false);
            volume.tag = ArtGenTags.BushTag;
            volume.layer = instance.layer;

            SphereCollider collider = volume.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * 0.85f;
            collider.center = new Vector3(0f, Mathf.Max(0.5f, bounds.size.y * 0.5f), 0f);
        }

        private static void ScatterProps(Transform parent, Material material, System.Random random,
            Vector2 center, float radius, int count, System.Func<int, Mesh> factory, string label, float scale)
        {
            // POI'ye ozgu son ek: iki farkli POI'nin prop'lari ayni asset dosyasini ezmesin.
            int poiKey = Mathf.Abs(center.GetHashCode()) % 997;

            for (int i = 0; i < count; i++)
            {
                Mesh mesh = ArtGenIO.SaveMesh(factory(center.GetHashCode() + i * 7),
                    $"{label}_{poiKey}_{i}", "Props");

                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = radius * Mathf.Sqrt((float)random.NextDouble());
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;

                GameObject instance = ArtGenIO.CreateMeshObject($"{label}_{i}", mesh, material, parent);
                instance.transform.position = ToWorld(point);
                instance.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                instance.transform.localScale = Vector3.one * scale;
                instance.isStatic = true;

                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = mesh.bounds.center;
                collider.size = mesh.bounds.size;
            }
        }

        private static void BuildBorderCliffs(Transform parent, Material material, System.Random random)
        {
            GameObject group = new("BorderCliffs");
            group.transform.SetParent(parent, false);

            Mesh fence = ArtGenIO.SaveMesh(PropFactory.BuildFenceSegment(10f), "BorderFence", "Props");

            // Kiyi hattinda, oyuncuyu haritada tutan gorsel + fiziksel sinir.
            float radius = _settings.PlayableRadius + _settings.BeachWidth * 0.55f;
            int segments = Mathf.RoundToInt(2f * Mathf.PI * radius / 9.5f);

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector2 point = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);

                GameObject instance = ArtGenIO.CreateMeshObject($"Fence_{i}", fence, material, group.transform);
                instance.transform.position = ToWorld(point);
                instance.transform.rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
                instance.isStatic = true;

                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.55f, 0f);
                collider.size = new Vector3(10f, 1.4f, 0.5f);
            }
        }

        #endregion

        #region Yardimcilar

        private static GameObject NewGroup(string name, Transform parent, Poi poi)
        {
            GameObject group = new(name);
            group.transform.SetParent(parent, false);
            group.transform.position = Vector3.zero;
            return group;
        }

        private static Vector3 ToWorld(Vector2 point)
        {
            return new Vector3(point.x, SampleHeight(point.x, point.y), point.y);
        }

        private static void AddMeshCollider(GameObject target, Mesh mesh)
        {
            MeshCollider collider = target.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private static bool IsOnRoad(Vector2 point, float clearance)
        {
            foreach (Road road in _roads)
            {
                if (DistanceToSegment(point, road.A, road.B) < road.Width * 0.7f + clearance)
                    return true;
            }

            return false;
        }

        private static bool IsInsidePoiCore(Vector2 point)
        {
            foreach (Poi poi in _pois)
            {
                if (Vector2.Distance(point, poi.Center) < poi.Radius * 1.05f)
                    return true;
            }

            return false;
        }

        private static float ProjectOnSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1e-6f)
                return 0f;

            return Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            float t = ProjectOnSegment(point, a, b);
            return Vector2.Distance(point, a + (b - a) * t);
        }

        #endregion
    }
}
