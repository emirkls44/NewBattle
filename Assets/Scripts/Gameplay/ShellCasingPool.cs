using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Ates edildiginde silahtan firlayan kovanlar.
    ///
    /// Rigidbody KULLANMIYOR. Seri ateste ekranda aninda 20-30 kovan olur;
    /// her birine fizik govdesi vermek mobilde bosa giden bir yuk olurdu.
    /// Kovanin yaptigi is basit bir parabol ve donme - ikisi de birkac
    /// carpma islemiyle hesaplaniyor, carpisma cozumune ihtiyac yok.
    ///
    /// Havuz sabit: her atista nesne yaratip yok etmek seri ateste surekli
    /// cop toplama tetikler ve oyun takilir. Havuz dolunca en eski kovan
    /// geri donusuyor.
    /// </summary>
    public class ShellCasingPool : MonoBehaviour
    {
        private const int PoolSize = 28;

        /// <summary>Pirinc rengi. Videoda kovanlar kucuk sari lekeler halinde.</summary>
        private static readonly Color CasingColor = new(0.95f, 0.78f, 0.25f, 1f);

        private struct Casing
        {
            public Transform Body;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public Vector3 Spin;
            public float GroundY;
            public float Age;
            public bool Active;
        }

        private static ShellCasingPool _instance;

        private Casing[] _pool;
        private int _nextIndex;
        private MaterialPropertyBlock _block;

        [Header("Firlatma")]
        [Tooltip("Kovanin silahtan yana firlama hizi.")]
        [SerializeField] private float sidewaysSpeed = 2.6f;

        [Tooltip("Kovanin yukari firlama hizi.")]
        [SerializeField] private float upwardSpeed = 2.2f;

        [Tooltip("Hiza eklenen rastgelelik. Sifir olsaydi her kovan ayni yere duserdi.")]
        [SerializeField] private float randomness = 0.9f;

        [Header("Yasam")]
        [Tooltip("Yer cekimi (negatif).")]
        [SerializeField] private float gravity = -14f;

        [Tooltip("Kovanin yok olmadan once ekranda kalma suresi.")]
        [SerializeField] private float lifetime = 1.5f;

        [Tooltip("Zemine carpinca hizin ne kadari korunur.")]
        [SerializeField, Range(0f, 0.8f)] private float bounce = 0.35f;

        [Tooltip("Kovanin boyutu.")]
        [SerializeField] private float size = 0.09f;

        /// <summary>
        /// Bir kovan firlatir.
        /// </summary>
        /// <param name="origin">Namlu konumu.</param>
        /// <param name="aimDirection">Atis yonu; kovan buna dik firlar.</param>
        /// <param name="groundY">Kovanin uzerine dusecegi zemin yuksekligi.</param>
        public static void Eject(Vector3 origin, Vector3 aimDirection, float groundY)
        {
            Ensure();
            _instance.Emit(origin, aimDirection, groundY);
        }

        private static void Ensure()
        {
            if (_instance != null)
                return;

            GameObject host = new("ShellCasingPool");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<ShellCasingPool>();
            _instance.BuildPool();
        }

        private void BuildPool()
        {
            _block = new MaterialPropertyBlock();
            _block.SetColor(OverlayMeshLibrary.BaseColorId, CasingColor);
            _block.SetFloat(OverlayMeshLibrary.FillId, 1f);

            _pool = new Casing[PoolSize];

            for (int i = 0; i < PoolSize; i++)
            {
                GameObject casing = new($"Casing_{i}");
                casing.transform.SetParent(transform, false);

                MeshFilter filter = casing.AddComponent<MeshFilter>();
                filter.sharedMesh = OverlayMeshLibrary.Disc;

                MeshRenderer renderer = casing.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = OverlayMeshLibrary.OverlayMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.SetPropertyBlock(_block);

                casing.transform.localScale = Vector3.one * size;
                casing.SetActive(false);

                _pool[i] = new Casing { Body = casing.transform, Renderer = renderer };
            }
        }

        private void Emit(Vector3 origin, Vector3 aimDirection, float groundY)
        {
            int index = _nextIndex;
            _nextIndex = (_nextIndex + 1) % _pool.Length;

            // Gercek silahlarda kovan namlunun SAGINA atilir; nisan yonune dik
            // olmasi hareketi okunur kiliyor. Ayni yone atsaydik kovan mermiyle
            // birlikte gidip kayboluyordu.
            Vector3 flat = new Vector3(aimDirection.x, 0f, aimDirection.z).normalized;

            if (flat.sqrMagnitude < 0.01f)
                flat = Vector3.forward;

            Vector3 right = Vector3.Cross(Vector3.up, flat);

            Vector3 velocity =
                right * sidewaysSpeed +
                Vector3.up * upwardSpeed +
                new Vector3(
                    Random.Range(-randomness, randomness),
                    Random.Range(0f, randomness),
                    Random.Range(-randomness, randomness));

            _pool[index].Body.position = origin;
            _pool[index].Body.rotation = Random.rotation;
            _pool[index].Velocity = velocity;
            _pool[index].Spin = new Vector3(
                Random.Range(-720f, 720f),
                Random.Range(-720f, 720f),
                Random.Range(-720f, 720f));
            _pool[index].GroundY = groundY;
            _pool[index].Age = 0f;
            _pool[index].Active = true;
            _pool[index].Body.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_pool == null)
                return;

            float delta = Time.deltaTime;

            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active)
                    continue;

                _pool[i].Age += delta;

                if (_pool[i].Age >= lifetime)
                {
                    _pool[i].Active = false;
                    _pool[i].Body.gameObject.SetActive(false);
                    continue;
                }

                _pool[i].Velocity += Vector3.up * gravity * delta;

                Vector3 position = _pool[i].Body.position + _pool[i].Velocity * delta;

                // Zemin: tek bir yukseklik karsilastirmasi. Raycast atmak
                // kovan basina her karede bir sorgu demek olurdu; bu kadar
                // kucuk ve kisa omurlu bir efekt icin gereksiz.
                if (position.y <= _pool[i].GroundY)
                {
                    position.y = _pool[i].GroundY;

                    _pool[i].Velocity = new Vector3(
                        _pool[i].Velocity.x * bounce,
                        -_pool[i].Velocity.y * bounce,
                        _pool[i].Velocity.z * bounce);

                    _pool[i].Spin *= bounce;
                }

                _pool[i].Body.position = position;
                _pool[i].Body.Rotate(_pool[i].Spin * delta, Space.Self);

                // Son ceyrekte soluyor: aniden yok olan kovanlar goz takiliyor.
                float fadeStart = lifetime * 0.75f;

                if (_pool[i].Age > fadeStart)
                {
                    float t = 1f - (_pool[i].Age - fadeStart) / (lifetime - fadeStart);
                    Color faded = CasingColor;
                    faded.a = t;

                    _block.SetColor(OverlayMeshLibrary.BaseColorId, faded);
                    _block.SetFloat(OverlayMeshLibrary.FillId, 1f);
                    _pool[i].Renderer.SetPropertyBlock(_block);
                }
            }
        }
    }
}
