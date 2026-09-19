using System.Collections.Generic;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Isabet alan karakterin anlik parlamasi.
    ///
    /// Vurdugunu anlamak icin hasar sayisi tek basina yetmiyor: sayi
    /// karakterin USTUNDE beliriyor, goz ise nisan alirken govdede
    /// oluyor. Govdenin kendisi tepki verince isabet dogrudan hissediliyor.
    ///
    /// Renk MaterialPropertyBlock ile veriliyor, materyal kopyalanmiyor.
    /// Kopyalasaydik her karakter kendi materyal ornegini alir, SRP
    /// Batcher gruplamayi birakir ve mobilde cizim cagrisi sayisi artardi.
    /// Ayrica bu sayede PlayerVisibility'nin materyal degistirmesiyle de
    /// catismiyor - property block renderer'a bagli, materyale degil.
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        [Tooltip("Isabet aninda govdenin donecegi renk.")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("Parlamanin ne kadar baskin olacagi. 1 = tamamen flashColor.")]
        [SerializeField, Range(0f, 1f)] private float strength = 0.8f;

        [Tooltip("Parlamanin suresi. Uzun olursa karakter surekli beyaz kalir.")]
        [SerializeField, Min(0.01f)] private float flashSeconds = 0.09f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int LegacyColorId = Shader.PropertyToID("_Color");

        private readonly List<Renderer> _renderers = new();
        private readonly List<Color> _originalColors = new();
        private readonly List<int> _colorIds = new();

        private MaterialPropertyBlock _block;
        private float _remaining;
        private bool _flashing;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            Collect();
        }

        /// <summary>
        /// Govde renderer'larini toplar ve her birinin ozgun rengini saklar.
        ///
        /// Gosterge katmanlari (nisan cemberi, ayak izi, saglik cubugu)
        /// disarida birakiliyor: onlar govdenin parcasi degil ve parlarsa
        /// isabet geri bildirimi yerine gorsel gurultu olurlardi.
        /// </summary>
        private void Collect()
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<OverlayVisual>() != null)
                    continue;

                if (renderer is ParticleSystemRenderer or LineRenderer or TrailRenderer)
                    continue;

                Material material = renderer.sharedMaterial;

                if (material == null)
                    continue;

                int colorId;

                if (material.HasProperty(BaseColorId))
                    colorId = BaseColorId;
                else if (material.HasProperty(LegacyColorId))
                    colorId = LegacyColorId;
                else
                    continue;

                _renderers.Add(renderer);
                _colorIds.Add(colorId);
                _originalColors.Add(material.GetColor(colorId));
            }
        }

        /// <summary>
        /// Bu karakteri parlatir.
        ///
        /// Bilesen yoksa kendisi ekliyor, boylece prefab'a elle bir sey
        /// baglamadan calisiyor. Parlamanin rengini veya suresini
        /// degistirmek istersen HitFlash'i PlayerOnline prefab'ina elle
        /// ekle; o zaman buradaki otomatik ekleme devreye girmez ve senin
        /// ayarlarin kullanilir.
        /// </summary>
        public static void Play(GameObject target)
        {
            if (target == null)
                return;

            if (!target.TryGetComponent(out HitFlash flash))
                flash = target.AddComponent<HitFlash>();

            flash.Flash();
        }

        public void Flash()
        {
            if (_renderers.Count == 0)
                return;

            _remaining = flashSeconds;

            if (_flashing)
                return;

            _flashing = true;

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null)
                    continue;

                Color lit = Color.Lerp(_originalColors[i], flashColor, strength);

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(_colorIds[i], lit);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void Update()
        {
            if (!_flashing)
                return;

            _remaining -= Time.deltaTime;

            if (_remaining > 0f)
                return;

            _flashing = false;
            Restore();
        }

        private void Restore()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null)
                    continue;

                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(_colorIds[i], _originalColors[i]);
                _renderers[i].SetPropertyBlock(_block);
            }
        }
    }
}
