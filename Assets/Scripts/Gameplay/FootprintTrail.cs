using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Yuruyen oyuncunun arkasinda birakip solan kucuk ayak izleri.
    ///
    /// Izler aga gonderilmez: her istemci, zaten replike edilen konumu izleyerek
    /// kendi tarafinda uretir. Bu hem bant genisligi harcamaz hem de cimende
    /// gizlenen bir oyuncunun izlerinin gozlemciye gore farkli gorunmesini
    /// (yakinsa gorunur, uzaksa gorunmez) dogal olarak mumkun kilar.
    ///
    /// Havuz sabit boyutludur; en eski iz geri donusturulur, calisma aninda
    /// hic tahsis yapilmaz.
    /// </summary>
    public class FootprintTrail : MonoBehaviour
    {
        [Header("Adim")]
        [Tooltip("Iki iz arasindaki yatay mesafe.")]
        [SerializeField, Min(0.1f)] private float stepDistance = 0.55f;

        [Tooltip("Izin merkez cizgiden yana kaymasi - sag/sol ayak hissi.")]
        [SerializeField, Min(0f)] private float footSpacing = 0.13f;

        [SerializeField] private float groundOffset = 0.03f;

        [Header("Solma")]
        [SerializeField, Min(0.2f)] private float lifetime = 4f;
        [SerializeField, Min(1)] private int poolSize = 14;
        [SerializeField] private Color printColor = new(0.18f, 0.14f, 0.09f, 0.55f);

        private Transform _container;
        private Print[] _pool;
        private int _nextIndex;
        private Vector3 _lastStepPosition;
        private bool _steppedRight;
        private bool _visible = true;
        private MaterialPropertyBlock _propertyBlock;

        private struct Print
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public float SpawnTime;
            public bool Active;
        }

        private void Start()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _lastStepPosition = transform.position;

            // Izler dunyada kalmali: oyuncunun altinda degil, sahne kokunde tutulur.
            GameObject containerObject = new($"Footprints_{name}");
            _container = containerObject.transform;

            _pool = new Print[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                MeshRenderer renderer = OverlayMeshLibrary.CreateOverlayObject(
                    $"Print_{i}", OverlayMeshLibrary.Footprint, _container, Vector3.zero, 1f);

                renderer.gameObject.SetActive(false);

                _pool[i] = new Print
                {
                    Transform = renderer.transform,
                    Renderer = renderer,
                    Active = false
                };
            }
        }

        private void OnDestroy()
        {
            if (_container != null)
                Destroy(_container.gameObject);
        }

        private void Update()
        {
            TrySpawnPrint();
            FadePrints();
        }

        private void TrySpawnPrint()
        {
            Vector3 position = transform.position;

            Vector3 travel = position - _lastStepPosition;
            travel.y = 0f;

            if (travel.sqrMagnitude < stepDistance * stepDistance)
                return;

            _lastStepPosition = position;
            _steppedRight = !_steppedRight;

            Vector3 forward = travel.normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (_steppedRight ? footSpacing : -footSpacing);

            SpawnPrint(position + side, forward);
        }

        private void SpawnPrint(Vector3 position, Vector3 forward)
        {
            // Havuzdaki en eski izi geri donustur.
            int index = _nextIndex;
            _nextIndex = (_nextIndex + 1) % _pool.Length;

            _pool[index].Transform.SetPositionAndRotation(
                new Vector3(position.x, position.y + groundOffset, position.z),
                Quaternion.LookRotation(forward, Vector3.up));

            _pool[index].SpawnTime = Time.time;
            _pool[index].Active = true;
            _pool[index].Renderer.gameObject.SetActive(_visible);
        }

        private void FadePrints()
        {
            float now = Time.time;

            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active)
                    continue;

                float age = now - _pool[i].SpawnTime;

                if (age >= lifetime)
                {
                    _pool[i].Active = false;
                    _pool[i].Renderer.gameObject.SetActive(false);
                    continue;
                }

                if (!_visible)
                    continue;

                Color color = printColor;
                color.a *= 1f - age / lifetime;

                _pool[i].Renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(OverlayMeshLibrary.BaseColorId, color);
                _propertyBlock.SetFloat(OverlayMeshLibrary.FillId, 1f);
                _pool[i].Renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>PlayerVisibility cagirir: izler bu gozlemciye gorunsun mu.</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;

            _visible = visible;

            if (_pool == null)
                return;

            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].Renderer != null)
                    _pool[i].Renderer.gameObject.SetActive(visible && _pool[i].Active);
            }
        }
    }
}
