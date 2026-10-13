using NewBattle.Progression;
using UnityEngine;

namespace NewBattle.Gameplay
{
    /// <summary>
    /// Karaktere ozel canli ayarlar: boy (Scale) ve durus duzeltmesi. Play'de
    /// KarakterKutuphanesi'nden degistirince aninda uygulanir.
    ///
    /// Durus: animasyondan SONRA boyun ve basi geriye
    /// (ya da eksi degerle one) eger. Iri kafali modellerde (Ahmet) bas boyunla
    /// birlikte one cikik duruyordu.
    ///
    /// Degerler KarakterKutuphanesi > karakter > Neck Back Tilt / Head Back Tilt.
    /// Play'de degistirince aninda gorunur. CharacterModelSwap ekler/gunceller.
    /// </summary>
    [DefaultExecutionOrder(400)] // animasyondan sonra, el IK'sindan (500) once
    public class PostureCorrection : MonoBehaviour
    {
        [SerializeField] private string characterId;
        [Tooltip("Modelin karaktere donusturulmeden onceki olcegi; boy = bu x karakterin Scale'i.")]
        [SerializeField] private Vector3 baseScale = Vector3.one;

        private Animator _animator;
        private Transform _neck;
        private Transform _head;
        private Quaternion _lastNeck;
        private Quaternion _lastHead;
        private bool _applied;
        private CharacterDefinition _definition;

        public void Configure(Animator animator, CharacterDefinition character, Vector3 originalScale)
        {
            characterId = character != null ? character.id : string.Empty;
            baseScale = originalScale;
            _animator = animator;
            _definition = character;
            _neck = null;
            _head = null;
            _applied = false;
        }

        private void LateUpdate()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>();
            if (_animator == null || !_animator.isActiveAndEnabled || _animator.avatar == null || !_animator.avatar.isHuman)
                return;

            if (_definition == null || _definition.id != characterId)
                _definition = CharacterLibrary.Instance != null ? CharacterLibrary.Instance.Find(characterId) : null;
            if (_definition == null)
                return;

            // Boy: Inspector'dan degisirse aninda.
            Vector3 targetScale = baseScale * _definition.scale;
            if (transform.localScale != targetScale)
                transform.localScale = targetScale;

            float neckTilt = _definition.neckBackTilt;
            float headTilt = _definition.headBackTilt;
            if (Mathf.Approximately(neckTilt, 0f) && Mathf.Approximately(headTilt, 0f))
                return;

            if (_neck == null)
                _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            if (_head == null)
                _head = _animator.GetBoneTransform(HumanBodyBones.Head);

            // Animator bu kare kemigi yazmadiysa (durdurulmus vb.) tekrar egip
            // her karede ust uste eklemeyelim.
            if (_applied && _neck != null && _neck.localRotation == _lastNeck &&
                _head != null && _head.localRotation == _lastHead)
                return;

            Vector3 right = transform.right;
            if (_neck != null && !Mathf.Approximately(neckTilt, 0f))
                _neck.rotation = Quaternion.AngleAxis(-neckTilt, right) * _neck.rotation;
            if (_head != null && !Mathf.Approximately(headTilt, 0f))
                _head.rotation = Quaternion.AngleAxis(-headTilt, right) * _head.rotation;

            _applied = true;
            if (_neck != null)
                _lastNeck = _neck.localRotation;
            if (_head != null)
                _lastHead = _head.localRotation;
        }
    }
}
