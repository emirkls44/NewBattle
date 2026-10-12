#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Gorsel iyilestirmeler: elleri tufege oturtan IK ve yerdeki esyalarin
    /// (ilk yardim, silah, mermi, kalkan) daha canli ve buyuk gorunmesi.
    /// Sadece bir kez uygulanir; senin sonradan yaptigin ayarlara dokunmaz.
    /// </summary>
    public static partial class MainMenuBuilder
    {
        private const string WorldMaterialPath = "Assets/GameArt/Materials/ToonWorld.mat";
        private const string LootMaterialPath = "Assets/GameArt/Materials/ToonLoot.mat";
        private const float LootScaleBoost = 1.35f;

        private static readonly string[] LootPrefabs =
        {
            "Assets/Prefabs/Pickups/AmmoPickup.prefab",
            "Assets/Prefabs/Pickups/HealthPickup.prefab",
            "Assets/Prefabs/Pickups/RiflePickup.prefab",
            "Assets/Prefabs/Pickups/ShieldPickup.prefab",
        };

        /// <summary>
        /// Sahneye "GorselAyarlar" nesnesini ekler: kamera yakinligi, esya ve
        /// harita renkleri tek yerden. Degerleri mevcut ayarlardan okur (hicbir
        /// sey degismez). Eklediyse true.
        /// </summary>
        private static bool EnsureVisualSettings()
        {
            if (Object.FindFirstObjectByType<VisualSettings>(FindObjectsInactive.Include) != null)
                return false;

            var go = new GameObject("GorselAyarlar");
            Undo.RegisterCreatedObjectUndo(go, "Gorsel ayarlar");
            VisualSettings settings = go.AddComponent<VisualSettings>();

            CameraFollow follow = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
            if (follow != null)
                settings.cameraViewSize = follow.ViewSize;

            settings.loot.ReadFrom(AssetDatabase.LoadAssetAtPath<Material>(LootMaterialPath));
            settings.world.ReadFrom(AssetDatabase.LoadAssetAtPath<Material>(WorldMaterialPath));
            EditorUtility.SetDirty(settings);
            return true;
        }

        /// <summary>PlayerOnline'a el IK'sini ve tutamak noktalarini ekler. Eklediyse true.</summary>
        private static bool EnsureWeaponHandIK()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (contents.GetComponent<WeaponHandIK>() != null)
                    return false;

                Transform rifle = contents.transform.Find("WeaponSocket/RifleVisual");
                if (rifle == null)
                {
                    Debug.LogWarning("PlayerOnline'da WeaponSocket/RifleVisual bulunamadi; el IK'si eklenmedi.");
                    return false;
                }

                // Yerler tufek modelinin olcusune gore: kabza ~%35, on tutamak ~%65.
                // Ellerin yerini bu nesneleri tasiyarak ayarlayabilirsin.
                Transform right = GripPoint(rifle, "SagElTutamak", new Vector3(0.02f, -0.06f, -0.03f));
                Transform left = GripPoint(rifle, "SolElTutamak", new Vector3(-0.03f, -0.08f, 0.28f));

                WeaponHandIK ik = contents.AddComponent<WeaponHandIK>();
                var serialized = new SerializedObject(ik);
                serialized.FindProperty("weapon").objectReferenceValue = rifle.gameObject;
                serialized.FindProperty("rightGrip").objectReferenceValue = right;
                serialized.FindProperty("leftGrip").objectReferenceValue = left;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static Transform GripPoint(Transform rifle, string name, Vector3 localPosition)
        {
            Transform existing = rifle.Find(name);
            if (existing != null)
                return existing;

            var point = new GameObject(name).transform;
            point.SetParent(rifle, false);
            point.localPosition = localPosition;
            return point;
        }

        /// <summary>Yerdeki esyalari canli renkli materyale gecirip buyutur. Kac tane degistigini dondurur.</summary>
        private static int EnsureLootLook()
        {
            Material loot = AssetDatabase.LoadAssetAtPath<Material>(LootMaterialPath);
            if (loot == null)
            {
                if (!AssetDatabase.CopyAsset(WorldMaterialPath, LootMaterialPath))
                    return 0;

                loot = AssetDatabase.LoadAssetAtPath<Material>(LootMaterialPath);
                // Vertex renkleri sRGB girilmis: cevirmeyince kirmizi pembe gorunuyordu.
                loot.SetFloat("_VertexColorSRGB", 1f);
                loot.SetFloat("_Saturation", 1.15f);
                loot.SetFloat("_Brightness", 1f);
                loot.SetFloat("_Ambient", 0.65f);
                loot.SetFloat("_RimStrength", 0.18f);
                EditorUtility.SetDirty(loot);
                AssetDatabase.SaveAssets();
            }

            int changed = 0;
            foreach (string path in LootPrefabs)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform model = contents.transform.Find("Visual/Model");
                    MeshRenderer renderer = model != null ? model.GetComponent<MeshRenderer>() : null;

                    // Zaten canli materyaldeyse bir daha buyutme.
                    if (renderer == null || renderer.sharedMaterial == loot)
                        continue;

                    renderer.sharedMaterial = loot;
                    model.localScale *= LootScaleBoost;
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    changed++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return changed;
        }
    }
}
#endif
