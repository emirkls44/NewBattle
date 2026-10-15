using NewBattle.Progression;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Bir karakterin gorunusunu baska bir karakterle degistirir.
    ///
    /// Animator bileseni YERINDE kalir; sadece altindaki iskelet ve govde
    /// yenisiyle degisir ve avatar degistirilir. Bircok script Animator'u
    /// basta bir kez bulup sakladigi icin (atis, durum makinesi, olum...)
    /// Animator'un kendisini degistirmek onlari bozardi.
    ///
    /// Renderer listesi tutan scriptler (gorunurluk, animasyon surucusu,
    /// isabet parlamasi) degisimden sonra <see cref="Swapped"/> ile yenilenir.
    /// </summary>
    public static class CharacterModelSwap
    {
        /// <summary>Modeli degisen Animator (dinleyenler kendi listelerini yeniler).</summary>
        public static event System.Action<Animator> Swapped;

        /// <summary>Uygulanan karakter; ayni karakter tekrar istenirse hicbir sey yapilmaz.</summary>
        private class Applied : MonoBehaviour
        {
            public string Id;
            public Vector3 BaseScale;
        }

        /// <summary>Modeli degistirir. Degistirdiyse true.</summary>
        public static bool Apply(Animator animator, CharacterDefinition character, CharacterDefinition defaultCharacter)
        {
            if (animator == null || character == null)
                return false;

            Transform root = animator.transform;
            if (!root.TryGetComponent(out Applied applied))
            {
                applied = root.gameObject.AddComponent<Applied>();
                applied.hideFlags = HideFlags.HideInInspector;
                applied.BaseScale = root.localScale;

                // Prefab'daki model (eski takim elbiseli) artik hicbir karakter degil,
                // sadece iskelet tasiyicisi: ilk istekte her zaman secili karaktere
                // (Ahmet / Ayse / ...) donusturulur. Eskiden "zaten varsayilan" sayiliyordu.
                applied.Id = string.Empty;
            }

            if (applied.Id == character.id || character.model == null || character.avatar == null)
                return false;

            int layer = root.gameObject.layer;

            // Eski govde ve iskelet. Hemen yok edilir: ayni karede renderer
            // listesini yeniden toplayan scriptler eskiyi bulmasin.
            for (int i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);

            GameObject instance = Object.Instantiate(character.model);

            // Model dosyasinda karaktere ait olmayan artiklar (ornek: Icosphere).
            if (character.removeObjects != null)
            {
                foreach (string part in character.removeObjects)
                {
                    Transform extra = FindDeep(instance.transform, part);
                    if (extra != null)
                        Object.DestroyImmediate(extra.gameObject);
                }
            }

            while (instance.transform.childCount > 0)
                instance.transform.GetChild(0).SetParent(root, false);
            Object.DestroyImmediate(instance);

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;

            if (character.material != null)
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = character.material;
                    renderer.sharedMaterials = materials;
                }
            }

            // Boy esitleme: modelin dosyadaki boyu ne olursa olsun referans karakterle ayni boy.
            CharacterLibrary library = CharacterLibrary.Instance;
            Vector3 baseScale = applied.BaseScale * (library != null ? library.HeightFactor(character) : 1f);
            root.localScale = baseScale * character.scale;
            animator.avatar = character.avatar;
            animator.Rebind();

            // Karaktere ozel durus duzeltmesi (boyun / bas geri egimi).
            PostureCorrection posture = root.GetComponent<PostureCorrection>();
            if (posture == null)
                posture = root.gameObject.AddComponent<PostureCorrection>();
            posture.Configure(animator, character, baseScale);

            applied.Id = character.id;
            Swapped?.Invoke(animator);
            return true;
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;

                Transform found = FindDeep(child, name);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
