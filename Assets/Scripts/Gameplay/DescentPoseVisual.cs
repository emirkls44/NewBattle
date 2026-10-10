using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Serbest dususte karakter modelini yuzustu yatirir ve yavasca dondurur;
    /// parasut acilinca karakter yumusakca dikilir.
    ///
    /// Sadece gorsel: ag konumuna ve carpisma kutusuna dokunmaz. Model her
    /// karede CharacterAnimationDriver tarafindan yeniden donduruldugu icin
    /// bu bilesen ondan SONRA calisir ve pozu onun ustune ekler.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class DescentPoseVisual : MonoBehaviour
    {
        [Tooltip("Serbest dususte one yatma acisi (90 = tam yuzustu).")]
        [SerializeField, Range(0f, 90f)] private float freefallTilt = 78f;
        [Tooltip("Dusus sirasinda kendi etrafinda donme hizi (derece/saniye).")]
        [SerializeField] private float spinSpeed = 40f;
        [Tooltip("Hafif yalpalama (derece).")]
        [SerializeField, Range(0f, 20f)] private float wobble = 7f;
        [SerializeField, Min(0.1f)] private float blendSpeed = 7f;
        [Tooltip("Yatirma bu yukseklikteki nokta etrafinda yapilir (yaklasik bel).")]
        [SerializeField, Min(0f)] private float pivotHeight = 0.9f;

        private ParachuteDescent _descent;
        private Transform _model;
        private Vector3 _baseLocalPosition;
        private Quaternion _lastApplied;
        private Quaternion _lastBase;
        private float _tilt;
        private float _spin;
        private bool _applied;

        private void Awake()
        {
            _descent = GetComponent<ParachuteDescent>();
            Animator animator = GetComponentInChildren<Animator>(true);
            _model = animator != null ? animator.transform : null;

            if (_model != null)
                _baseLocalPosition = _model.localPosition;
        }

        private void LateUpdate()
        {
            if (_descent == null || _model == null || _descent.Object == null || !_descent.Object.IsValid)
                return;

            bool falling = _descent.Phase == ParachuteDescent.DescentPhase.Freefall;
            float blend = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);

            _tilt = Mathf.Lerp(_tilt, falling ? freefallTilt : 0f, blend);
            _spin = falling ? _spin + spinSpeed * Time.deltaTime : Mathf.LerpAngle(_spin, 0f, blend);

            if (!falling && _tilt < 0.05f && Mathf.Abs(Mathf.DeltaAngle(_spin, 0f)) < 0.05f)
            {
                if (_applied)
                {
                    _model.localPosition = _baseLocalPosition;
                    _applied = false;
                }

                _tilt = 0f;
                _spin = 0f;
                return;
            }

            // Animasyon surucusu bu karede donusu yeniden yazmadiysa, bizim bir
            // onceki pozumuzun ustune tekrar eklemeyelim (birikirdi).
            Quaternion current = _model.localRotation;
            Quaternion baseRotation = _applied && current == _lastApplied ? _lastBase : current;

            float wobbleAngle = Mathf.Sin(Time.time * 3.1f) * wobble * (_tilt / Mathf.Max(1f, freefallTilt));
            Quaternion pose = Quaternion.AngleAxis(_spin, Vector3.up) *
                              Quaternion.AngleAxis(_tilt, Vector3.right) *
                              Quaternion.AngleAxis(wobbleAngle, Vector3.forward);

            _model.localRotation = pose * baseRotation;

            // Ayaklar degil bel etrafinda donsun.
            Vector3 pivot = Vector3.up * pivotHeight;
            _model.localPosition = _baseLocalPosition + pivot - pose * pivot;

            _lastApplied = _model.localRotation;
            _lastBase = baseRotation;
            _applied = true;
        }
    }
}
