using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Karakterin ayagi altindaki alan cemberi.
    ///
    /// Renk gozlemciye goredir; ayni oyuncu senin ekraninda mavi (takim arkadasi),
    /// baskasinin ekraninda kirmizi (dusman) gorunur. Bu yuzden renk aga yazilmaz,
    /// her istemcide LocalPlayerContext uzerinden yerel olarak hesaplanir.
    ///
    /// Cember govdeyle birlikte gizlenir: cimende kaybolan oyuncunun cemberi
    /// ortada kalirsa gizlenmenin hicbir anlami kalmaz.
    /// </summary>
    [RequireComponent(typeof(PlayerCombatStats))]
    public class PlayerRingIndicator : MonoBehaviour
    {
        [Header("Olcu")]
        [SerializeField, Min(0.2f)] private float radius = 1.15f;
        [SerializeField] private float groundOffset = 0.04f;

        [Header("Renkler")]
        [SerializeField] private Color selfColor = new(1f, 1f, 1f, 0.85f);
        [SerializeField] private Color teammateColor = new(0.25f, 0.66f, 1f, 0.85f);
        [SerializeField] private Color enemyColor = new(1f, 0.27f, 0.36f, 0.85f);

        [Header("Davranis")]
        [Tooltip("Acikken dusman cemberleri sadece dusman gorunurken cizilir.")]
        [SerializeField] private bool followBodyVisibility = true;

        private PlayerCombatStats _stats;
        private PlayerVisibility _visibility;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private PlayerRelation _lastRelation = (PlayerRelation)(-1);

        private void Start()
        {
            _stats = GetComponent<PlayerCombatStats>();
            _visibility = GetComponent<PlayerVisibility>();
            _propertyBlock = new MaterialPropertyBlock();

            _renderer = OverlayMeshLibrary.CreateOverlayObject(
                "AreaRing",
                OverlayMeshLibrary.Ring,
                transform,
                new Vector3(0f, groundOffset, 0f),
                radius);

            ApplyColor(PlayerRelation.Unknown);
        }

        private void LateUpdate()
        {
            if (_renderer == null)
                return;

            // Karakter donerken cember de donmesin; dunya eksenine sabit kalsin.
            _renderer.transform.rotation = Quaternion.identity;

            PlayerRelation relation = LocalPlayerContext.GetRelation(_stats);

            if (relation != _lastRelation)
                ApplyColor(relation);

            bool shouldDraw = !followBodyVisibility
                              || _visibility == null
                              || _visibility.IsBodyVisible;

            if (_renderer.enabled != shouldDraw)
                _renderer.enabled = shouldDraw;
        }

        private void ApplyColor(PlayerRelation relation)
        {
            _lastRelation = relation;

            Color color = relation switch
            {
                PlayerRelation.Self => selfColor,
                PlayerRelation.Teammate => teammateColor,
                PlayerRelation.Enemy => enemyColor,
                _ => selfColor
            };

            _renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(OverlayMeshLibrary.BaseColorId, color);
            _propertyBlock.SetFloat(OverlayMeshLibrary.FillId, 1f);
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
