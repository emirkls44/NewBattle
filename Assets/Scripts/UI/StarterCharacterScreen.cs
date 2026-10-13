using System;
using NewBattle.Audio;
using NewBattle.Gameplay;
using NewBattle.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Ilk acilista isimden sonra: "KARAKTERİNİ SEÇ". Solda erkek (Ahmet), sagda kiz
    /// (Ayse); ikisi de canli 3B onizleme (bekleme animasyonu), altlarinda isimleri.
    /// Karta dokun = sec, DEVAM ET = karakter oyuncunun olur ve ana menude gorunur.
    /// Digerine sonra gecmek icin seviye gerekir (KarakterKutuphanesi > Requirement).
    ///
    /// Arka plan tamamen opak (menu arka planlarindan); arkada oyun gorunmez.
    /// Tools > NewBattle > Menü > Kur ile "KarakterSecimEkrani" olarak kurulur.
    /// </summary>
    public class StarterCharacterScreen : MonoBehaviour
    {
        [Serializable]
        public class Slot
        {
            [Tooltip("KarakterKutuphanesi'ndeki kimlik (ahmet / ayse).")]
            public string characterId;
            public Button card;
            public Image frame;
            public RawImage preview;
            public TextMeshProUGUI nameText;
        }

        [SerializeField] private Slot left = new() { characterId = "ahmet" };
        [SerializeField] private Slot right = new() { characterId = "ayse" };
        [SerializeField] private Button confirmButton;
        [SerializeField] private TextMeshProUGUI hintText;
        [Tooltip("Onizlemelerde oynayan bekleme animasyonu bu kutuphanenin varsayilanidir.")]
        [SerializeField] private LobbyAnimationLibrary animations;

        [Header("Gorunum")]
        [SerializeField] private Color selectedFrame = new(1f, 0.83f, 0.2f, 1f);
        [SerializeField] private Color idleFrame = new(1f, 1f, 1f, 0.25f);
        [SerializeField, Range(0.5f, 1f)] private float unselectedScale = 0.9f;
        [SerializeField, Range(256, 1024)] private int previewResolution = 640;

        private Action _onDone;
        private string _selectedId;
        private bool _initialized;
        private bool _opening;

        private Preview _leftPreview;
        private Preview _rightPreview;

        private sealed class Preview
        {
            public GameObject Root;
            public RenderTexture Texture;
        }

        private void Awake()
        {
            Initialize();
            if (!_opening)
                gameObject.SetActive(false);
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            if (left.card != null)
                left.card.onClick.AddListener(() => Pick(left.characterId));
            if (right.card != null)
                right.card.onClick.AddListener(() => Pick(right.characterId));
            if (confirmButton != null)
                confirmButton.onClick.AddListener(Confirm);
        }

        /// <summary>Ilk acilis: karakter secilmeden kapanmaz. Bitince onDone.</summary>
        public void Open(Action onDone)
        {
            Initialize();
            _onDone = onDone;
            _selectedId = null;

            _opening = true;
            gameObject.SetActive(true);
            _opening = false;
            transform.SetAsLastSibling();

            CharacterLibrary library = CharacterLibrary.Instance;
            _leftPreview = BuildPreview(library?.Find(left.characterId), new Vector3(-3000f, 2000f, 0f), left);
            _rightPreview = BuildPreview(library?.Find(right.characterId), new Vector3(-3000f, 2000f, 40f), right);

            Refresh();
        }

        private void Pick(string id)
        {
            _selectedId = id;
            Sfx.PlayUI("buton");
            Refresh();
        }

        private void Confirm()
        {
            CharacterLibrary library = CharacterLibrary.Instance;
            CharacterDefinition chosen = library != null ? library.Find(_selectedId) : null;
            if (chosen == null)
            {
                Sfx.PlayUI("hata");
                if (hintText != null)
                    hintText.text = "Önce bir karakter seç!";
                return;
            }

            library.ChooseStarter(chosen);
            Sfx.PlayUI("odul");
            Close();

            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private void Close()
        {
            DestroyPreview(ref _leftPreview);
            DestroyPreview(ref _rightPreview);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            DestroyPreview(ref _leftPreview);
            DestroyPreview(ref _rightPreview);
        }

        private void Refresh()
        {
            CharacterLibrary library = CharacterLibrary.Instance;
            DrawSlot(left, library);
            DrawSlot(right, library);

            if (confirmButton != null)
                confirmButton.interactable = !string.IsNullOrEmpty(_selectedId);
            if (hintText != null)
                hintText.text = string.IsNullOrEmpty(_selectedId)
                    ? "Bir karaktere dokun"
                    : "Diğerini sonra seviye atlayarak açabilirsin";
        }

        private void DrawSlot(Slot slot, CharacterLibrary library)
        {
            CharacterDefinition character = library != null ? library.Find(slot.characterId) : null;
            if (slot.nameText != null)
                slot.nameText.text = character != null ? character.displayName : slot.characterId;

            bool selected = slot.characterId == _selectedId;
            if (slot.frame != null)
                slot.frame.color = selected ? selectedFrame : idleFrame;
            if (slot.card != null)
                slot.card.transform.localScale = Vector3.one * (selected || string.IsNullOrEmpty(_selectedId) ? 1f : unselectedScale);
        }

        // ------------------------------------------------------------------
        // 3B onizleme: dunyanin uzak bir kosesinde model + kamera, RenderTexture'a.
        // ------------------------------------------------------------------

        private Preview BuildPreview(CharacterDefinition character, Vector3 origin, Slot slot)
        {
            if (character == null || slot.preview == null)
                return null;

            var preview = new Preview { Root = new GameObject("SecimOnizleme_" + character.id) };
            preview.Root.transform.position = origin;

            var holder = new GameObject("Model", typeof(Animator));
            holder.transform.SetParent(preview.Root.transform, false);
            // Kamera +Z'den bakar; karakter kameraya donuk dursun.
            holder.transform.localRotation = Quaternion.identity;
            Animator animator = holder.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            CharacterModelSwap.Apply(animator, character, null);

            AnimationClip idle = animations != null && animations.Default != null ? animations.Default.clip : null;
            if (idle != null)
                holder.AddComponent<LoopingClipPlayer>().Play(animator, idle);

            // Kadraj: ayakta duran poza gore.
            animator.Update(0f);
            Renderer[] renderers = holder.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(origin + Vector3.up, Vector3.one * 1.7f);
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            RectTransform area = slot.preview.rectTransform;
            float aspect = area.rect.height > 1f ? Mathf.Max(0.2f, area.rect.width / area.rect.height) : 0.75f;
            int height = previewResolution;
            int width = Mathf.Max(64, Mathf.RoundToInt(height * aspect));
            preview.Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Secim_" + character.id, antiAliasing = 2 };
            preview.Texture.Create();
            slot.preview.texture = preview.Texture;
            slot.preview.color = Color.white;

            var cameraObject = new GameObject("Kamera", typeof(Camera));
            cameraObject.transform.SetParent(preview.Root.transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = 26f;
            camera.targetTexture = preview.Texture;
            camera.allowHDR = false;
            cameraObject.AddComponent<ToonPreviewLight>(); // yuz hep aydinlik

            float frameHeight = bounds.size.y * 1.14f;
            float tanHalf = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = Mathf.Max(frameHeight * 0.5f / tanHalf, bounds.size.x * 0.5f / (tanHalf * aspect)) + bounds.extents.z;
            Vector3 target = bounds.center;
            camera.transform.position = target + new Vector3(0f, bounds.size.y * 0.04f, distance);
            camera.transform.LookAt(target);
            camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.1f);
            camera.farClipPlane = distance * 4f;

            return preview;
        }

        private static void DestroyPreview(ref Preview preview)
        {
            if (preview == null)
                return;

            if (preview.Root != null)
                Destroy(preview.Root);
            if (preview.Texture != null)
            {
                preview.Texture.Release();
                Destroy(preview.Texture);
            }

            preview = null;
        }
    }
}
