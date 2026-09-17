using Fusion;
using NewBattle.Gameplay;
using UnityEditor;
using UnityEngine;

namespace NewBattle.ArtGen
{
    /// <summary>
    /// Oyuncu prefabini yeni altyapiya hazirlar:
    ///
    ///   1. HitboxRoot + Hitbox  -> gecikme telafili atis icin ZORUNLU.
    ///      Fusion'un LagCompensation'i PhysX collider'lari degil, kendi Hitbox
    ///      bilesenlerini gecmise sarar. Bunlar olmadan atis yine calisir ama
    ///      telafisiz calisir: yuksek pingde hareketli hedefin onunu nisanlamak
    ///      gerekir.
    ///
    ///   2. PlayerRegistry -> her tick sahne taramasini bitiren merkezi kayit.
    ///
    ///   3. Silah tanimlari -> PlayerShooting'in dizisi bossa varsayilan seti yazar.
    ///
    /// Idempotent: tekrar calistirmak kopya uretmez.
    /// </summary>
    public static class PlayerRigSetup
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/PlayerOnline.prefab";

        /// <summary>Silahin zeminden yuksekligi. Karakterin gogus hizasi.</summary>
        private const float WeaponHeight = 0.95f;

        /// <summary>Silahin govdenin ne kadar onunde duracagi.</summary>
        private const float WeaponForward = 0.25f;

        [MenuItem("Tools/NewBattle/Isabet Kutularini Kur", false, 12)]
        public static void Run()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
            {
                Debug.LogError($"{PlayerPrefabPath} bulunamadi.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            int changes = 0;

            try
            {
                changes += EnsureRegistry(root);
                changes += EnsureHitboxes(root);
                changes += EnsureWeapons(root);
                changes += CenterWeapon(root);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"Oyuncu prefabi hazir. {changes} degisiklik yapildi.\n" +
                "Gecikme telafisi icin NetworkProjectConfig > Simulation > " +
                "Lag Compensation'in acik oldugundan emin ol.");
        }

        /// <summary>
        /// Silahi karakterin SAG ELINDEN alip tam onune, govde ekseninin uzerine
        /// tasir.
        ///
        /// NEDEN: Silah el kemigine bagliydi, yani karakterin sag tarafinda
        /// duruyordu. Ustten bakan bir nisancida bunun iki sonucu var:
        ///   1. Silah gorsel olarak yana kaymis gorunur.
        ///   2. Daha onemlisi, mermiler govde ekseninden DEGIL namludan cikar;
        ///      nisan cizgisi ise govde ekseninde. Ikisi paralel ama kaymis iki
        ///      cizgi olur ve oyuncu nisan aldigi yeri vuramaz.
        ///
        /// Yon hesabi tahmine dayanmaz: namlu yonunu (RifleVisual -> firePoint)
        /// olcup onu karakterin ileri yonune dondururuz.
        ///
        /// Bedeli: silah artik el animasyonunu takip etmez. Ustten bakan bir
        /// oyunda bu fark edilmez, nisan dogrulugu ise her seydir.
        /// </summary>
        private static int CenterWeapon(GameObject root)
        {
            PlayerLoadout loadout = root.GetComponent<PlayerLoadout>();
            PlayerShooting shooting = root.GetComponent<PlayerShooting>();

            if (loadout == null || shooting == null)
                return 0;

            SerializedObject loadoutSerialized = new(loadout);
            SerializedProperty visualProperty = loadoutSerialized.FindProperty("rifleVisual");

            GameObject visual = visualProperty != null
                ? visualProperty.objectReferenceValue as GameObject
                : null;

            if (visual == null)
            {
                Debug.LogWarning("Silah ortalanamadi: PlayerLoadout.rifleVisual atanmamis.");
                return 0;
            }

            Transform socket = visual.transform.parent;

            // Silah dogrudan koke bagliysa zaten ortalanmis demektir.
            if (socket == null || socket == root.transform)
                return 0;

            Transform firePoint = shooting.FirePoint;
            Quaternion worldRotation = socket.rotation;
            Quaternion targetRotation = worldRotation;

            if (firePoint != null)
            {
                Vector3 barrel = firePoint.position - visual.transform.position;

                if (barrel.sqrMagnitude > 0.0001f)
                {
                    // Namlu yonunu karakterin ileri yonune (+Z) cevir.
                    Quaternion correction =
                        Quaternion.FromToRotation(barrel.normalized, Vector3.forward);
                    targetRotation = correction * worldRotation;
                }
            }

            socket.SetParent(root.transform, false);
            socket.localRotation = targetRotation;
            socket.localPosition = new Vector3(0f, WeaponHeight, WeaponForward);
            socket.localScale = Vector3.one;

            Debug.Log(
                $"Silah ortalandi: '{socket.name}' artik karakter kokune bagli, " +
                $"konum (0, {WeaponHeight}, {WeaponForward}).");

            return 1;
        }

        private static int EnsureRegistry(GameObject root)
        {
            if (root.GetComponent<PlayerRegistry>() != null)
                return 0;

            root.AddComponent<PlayerRegistry>();
            return 1;
        }

        /// <summary>
        /// Govde icin tek bir kapsul hitbox yeterli. Kafa/govde ayrimi (headshot)
        /// istenirse buraya ikinci bir Hitbox eklenir; simdilik referans oyunda
        /// da boyle bir ayrim yok.
        /// </summary>
        private static int EnsureHitboxes(GameObject root)
        {
            int changes = 0;

            HitboxRoot hitboxRoot = root.GetComponent<HitboxRoot>();
            if (hitboxRoot == null)
            {
                hitboxRoot = root.AddComponent<HitboxRoot>();
                changes++;
            }

            Transform existing = root.transform.Find("BodyHitbox");
            GameObject hitboxObject;

            if (existing != null)
            {
                hitboxObject = existing.gameObject;
            }
            else
            {
                hitboxObject = new GameObject("BodyHitbox");
                hitboxObject.transform.SetParent(root.transform, false);
                changes++;
            }

            Hitbox hitbox = hitboxObject.GetComponent<Hitbox>();
            if (hitbox == null)
            {
                hitbox = hitboxObject.AddComponent<Hitbox>();
                changes++;
            }

            // Olculeri CharacterController'dan aliyoruz: gorsel model degisse bile
            // isabet hacmi oyuncunun gercekte kapladigi yerle ayni kalsin.
            CharacterController controller = root.GetComponent<CharacterController>();
            float radius = controller != null ? controller.radius : 0.35f;
            float height = controller != null ? controller.height : 1.8f;
            Vector3 center = controller != null ? controller.center : new Vector3(0f, 0.9f, 0f);

            SerializedObject serialized = new(hitbox);

            // Alan adlari Fusion.Hitbox'tan birebir: Type, CapsuleRadius,
            // CapsuleExtents, SphereRadius, Offset. "CapsuleHeight" diye bir alan
            // YOK - kapsulun uzunlugu CapsuleExtents ile verilir.
            SetEnumByName(serialized, "Type", "Capsule");
            SetFloat(serialized, "CapsuleRadius", radius);
            SetFloat(serialized, "SphereRadius", radius);

            // CapsuleExtents yari uzunluktur ve kapsulun kure uclari yaricap kadar
            // yer kapladigi icin govde kismi bu kadar kisalir.
            float halfExtent = Mathf.Max(0.05f, height * 0.5f - radius);
            SerializedProperty extents = serialized.FindProperty("CapsuleExtents");
            if (extents != null)
            {
                if (extents.propertyType == SerializedPropertyType.Float)
                    extents.floatValue = halfExtent;
                else if (extents.propertyType == SerializedPropertyType.Vector3)
                    extents.vector3Value = new Vector3(radius, halfExtent, radius);
            }

            SerializedProperty offset = serialized.FindProperty("Offset");
            if (offset != null)
                offset.vector3Value = center;

            serialized.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject rootSerialized = new(hitboxRoot);

            // Genis faz yaricapi tum hitbox'lari icine almali; kucuk kalirsa
            // isin hitbox'a hic ulasmadan elenir ve atis sessizce isabetsiz olur.
            SerializedProperty broadRadius = rootSerialized.FindProperty("BroadRadius");
            if (broadRadius != null && broadRadius.propertyType == SerializedPropertyType.Float)
                broadRadius.floatValue = Mathf.Max(radius, height * 0.5f) + 0.25f;

            SerializedProperty rootOffset = rootSerialized.FindProperty("Offset");
            if (rootOffset != null && rootOffset.propertyType == SerializedPropertyType.Vector3)
                rootOffset.vector3Value = center;

            SerializedProperty hitboxes = rootSerialized.FindProperty("Hitboxes");

            if (hitboxes != null && hitboxes.isArray)
            {
                bool alreadyListed = false;
                for (int i = 0; i < hitboxes.arraySize; i++)
                {
                    if (hitboxes.GetArrayElementAtIndex(i).objectReferenceValue == hitbox)
                    {
                        alreadyListed = true;
                        break;
                    }
                }

                if (!alreadyListed)
                {
                    int index = hitboxes.arraySize;
                    hitboxes.InsertArrayElementAtIndex(index);
                    hitboxes.GetArrayElementAtIndex(index).objectReferenceValue = hitbox;
                    changes++;
                }
            }

            rootSerialized.ApplyModifiedPropertiesWithoutUndo();

            return changes;
        }

        private static void SetFloat(SerializedObject serialized, string path, float value)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property != null && property.propertyType == SerializedPropertyType.Float)
                property.floatValue = value;
        }

        /// <summary>
        /// Enum'u ISMIYLE ayarlar. Sayisal indeks yazmak kirilgan: Fusion enum'a
        /// yeni bir deger eklerse sessizce yanlis sekli secmis oluruz.
        /// </summary>
        private static void SetEnumByName(SerializedObject serialized, string path, string valueName)
        {
            SerializedProperty property = serialized.FindProperty(path);
            if (property == null || property.propertyType != SerializedPropertyType.Enum)
                return;

            string[] names = property.enumNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == valueName)
                {
                    property.enumValueIndex = i;
                    return;
                }
            }

            Debug.LogWarning($"Hitbox: '{valueName}' turu bulunamadi, varsayilan birakildi.");
        }

        /// <summary>
        /// Silah dizisi bossa varsayilan seti yazar. Dolu bir diziye dokunmaz:
        /// oyuncu Inspector'da degerleri ayarladiysa onlari ezmek istemiyoruz.
        /// </summary>
        private static int EnsureWeapons(GameObject root)
        {
            PlayerShooting shooting = root.GetComponent<PlayerShooting>();
            if (shooting == null)
                return 0;

            SerializedObject serialized = new(shooting);
            SerializedProperty weapons = serialized.FindProperty("weapons");

            if (weapons == null || !weapons.isArray || weapons.arraySize > 0)
                return 0;

            WeaponDefinition[] defaults = WeaponDefinition.CreateDefaultSet();
            weapons.arraySize = defaults.Length;

            for (int i = 0; i < defaults.Length; i++)
            {
                SerializedProperty element = weapons.GetArrayElementAtIndex(i);
                WeaponDefinition source = defaults[i];

                element.FindPropertyRelative("label").stringValue = source.label;
                element.FindPropertyRelative("isMelee").boolValue = source.isMelee;
                element.FindPropertyRelative("damage").floatValue = source.damage;
                element.FindPropertyRelative("fireInterval").floatValue = source.fireInterval;
                element.FindPropertyRelative("range").floatValue = source.range;
                element.FindPropertyRelative("pellets").intValue = source.pellets;
                element.FindPropertyRelative("spreadAngle").floatValue = source.spreadAngle;
                element.FindPropertyRelative("maxAmmo").intValue = source.maxAmmo;
                element.FindPropertyRelative("infiniteAmmo").boolValue = source.infiniteAmmo;
                element.FindPropertyRelative("tracerColor").colorValue = source.tracerColor;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }
    }
}
