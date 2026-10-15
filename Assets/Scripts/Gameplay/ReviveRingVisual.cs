using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Takim modunda yere dusen oyuncunun altindaki kaldirma halkasi: loot halkasi gibi,
    /// arkadasi icine girip beklerken yesil dolar. Halkayi yerdeki oyuncu ve takim
    /// arkadaslari gorur. HealthController oyuncu dogarken bunu kendisi ekler.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReviveRingVisual : MonoBehaviour
    {
        private static readonly Color TrackColor = new(1f, 0.35f, 0.3f, 0.75f);
        private static readonly Color FillColor = new(0.2f, 1f, 0.08f, 1f);

        private HealthController _health;
        private PlayerCombatStats _stats;
        private GameObject _root;
        private MeshRenderer _track, _fill;
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _health = GetComponent<HealthController>();
            _stats = GetComponent<PlayerCombatStats>();
        }

        private void LateUpdate()
        {
            bool show = _health != null && _health.Object != null && _health.Object.IsValid &&
                        _health.IsDowned && VisibleToLocal();

            if (!show)
            {
                if (_root != null && _root.activeSelf)
                    _root.SetActive(false);
                return;
            }

            EnsureRings();
            if (!_root.activeSelf)
                _root.SetActive(true);

            // Dunyaya gore duz ve dogru yaricapli kalsin (karakter donse de).
            Vector3 position = transform.position;
            _root.transform.SetPositionAndRotation(new Vector3(position.x, position.y + 0.05f, position.z), Quaternion.identity);
            Vector3 parentScale = transform.lossyScale;
            _root.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                1f / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
            float radius = _health.ReviveRadius;
            _track.transform.localScale = Vector3.one * radius;
            _fill.transform.localScale = Vector3.one * radius;

            SetOverlay(_track, TrackColor, 1f);
            SetOverlay(_fill, FillColor, Mathf.Clamp01(_health.ReviveProgress));
        }

        /// <summary>Yerdeki oyuncunun kendisi ve takim arkadaslari gorur (olup izleyen de).</summary>
        private bool VisibleToLocal()
        {
            if (_stats == null || _stats.HasInputAuthority)
                return true;

            var registry = PlayerRegistry.All;
            for (int i = 0; i < registry.Count; i++)
            {
                PlayerRegistry entry = registry[i];
                if (entry != null && entry.Object != null && entry.Object.IsValid && entry.Object.HasInputAuthority)
                    return entry.Stats != null && entry.Stats.IsTeammate(_stats);
            }

            return true; // yerel oyuncu olmus ve silinmis: izlerken takim arkadasini goruyor
        }

        private void EnsureRings()
        {
            if (_root != null)
                return;

            _root = new GameObject("KaldirmaHalkasi");
            _root.transform.SetParent(transform, false);
            _block = new MaterialPropertyBlock();
            _track = OverlayMeshLibrary.CreateOverlayObject("Iz", OverlayMeshLibrary.SoftRing, _root.transform, new Vector3(0f, 0.004f, 0f), 1f);
            _fill = OverlayMeshLibrary.CreateOverlayObject("Dolum", OverlayMeshLibrary.SoftRing, _root.transform, new Vector3(0f, 0.008f, 0f), 1f);
        }

        private void SetOverlay(MeshRenderer overlay, Color color, float fill)
        {
            bool visible = fill > 0f;
            if (overlay.enabled != visible)
                overlay.enabled = visible;
            if (!visible)
                return;

            _block.SetColor(OverlayMeshLibrary.BaseColorId, color);
            _block.SetFloat(OverlayMeshLibrary.FillId, fill);
            overlay.SetPropertyBlock(_block);
        }

        private void OnDestroy()
        {
            if (_root != null)
                Destroy(_root);
        }
    }
}
