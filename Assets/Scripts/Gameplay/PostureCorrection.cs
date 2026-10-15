using System.Collections.Generic;
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
    /// Ayaklar: animasyon baska iskeletten aktarilinca (iri ayakkabili modellerde)
    /// ayak burnu egik/ayakkabi bukuk kaliyordu; Foot Flatten ayagi yere paralel,
    /// Toe Lock parmak kemigini T pozundaki gibi tutar.
    ///
    /// Degerler KarakterKutuphanesi > karakter > Neck/Head Back Tilt, Foot Flatten, Toe Lock.
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

        // Ayaklar: [0] sol, [1] sag. Dinlenme donusleri avatarin T pozundan.
        private readonly Transform[] _feet = new Transform[2];
        private readonly Transform[] _toes = new Transform[2];
        private readonly Quaternion[] _footRest = new Quaternion[2]; // animator kokune gore
        private readonly Quaternion[] _toeRest = new Quaternion[2];  // yerel
        private bool _feetReady;

        public void Configure(Animator animator, CharacterDefinition character, Vector3 originalScale)
        {
            characterId = character != null ? character.id : string.Empty;
            baseScale = originalScale;
            _animator = animator;
            _definition = character;
            _neck = null;
            _head = null;
            _applied = false;
            _feetReady = false;
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

            // Yere yatmisken (olum / takim modunda yere dusme) ayaklari zemine yatirma.
            if (!_animator.GetCurrentAnimatorStateInfo(0).IsName("Death"))
                CorrectFeet(_definition.footFlatten, _definition.toeLock);

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

        private void CorrectFeet(float flatten, float toeLock)
        {
            if (flatten <= 0f && toeLock <= 0f)
                return;
            if (!_feetReady && !PrepareFeet())
                return;

            Transform root = _animator.transform;
            Vector3 up = root.up;
            for (int i = 0; i < 2; i++)
            {
                Transform foot = _feet[i];
                if (foot != null && flatten > 0f)
                {
                    // Ayagin "onu" (T pozunda karakterin onu olan yon) yere izdusurulur;
                    // yon (saga/sola donus) animasyondan kalir, egim/yatma sifirlanir.
                    Vector3 forward = foot.rotation * (Quaternion.Inverse(_footRest[i]) * Vector3.forward);
                    Vector3 flat = Vector3.ProjectOnPlane(forward, up);
                    if (flat.sqrMagnitude > 1e-6f)
                    {
                        Quaternion target = Quaternion.LookRotation(flat, up) * _footRest[i];
                        foot.rotation = Quaternion.Slerp(foot.rotation, target, flatten);
                    }
                }

                Transform toe = _toes[i];
                if (toe != null && toeLock > 0f)
                    toe.localRotation = Quaternion.Slerp(toe.localRotation, _toeRest[i], toeLock);
            }
        }

        /// <summary>Ayak/parmak kemiklerini ve T pozundaki donuslerini avatardan bulur.</summary>
        private bool PrepareFeet()
        {
            HumanDescription description = _animator.avatar.humanDescription;
            var rest = new Dictionary<string, Quaternion>();
            if (description.skeleton != null)
                foreach (SkeletonBone bone in description.skeleton)
                    rest[bone.name] = bone.rotation;

            Transform root = _animator.transform;
            Quaternion RestLocal(Transform t) => rest.TryGetValue(t.name, out Quaternion r) ? r : t.localRotation;

            _feet[0] = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            _feet[1] = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
            _toes[0] = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
            _toes[1] = _animator.GetBoneTransform(HumanBodyBones.RightToes);
            for (int i = 0; i < 2; i++)
            {
                Quaternion relative = Quaternion.identity;
                for (Transform t = _feet[i]; t != null && t != root; t = t.parent)
                    relative = RestLocal(t) * relative;
                _footRest[i] = relative;
                if (_toes[i] != null)
                    _toeRest[i] = RestLocal(_toes[i]);
            }

            _feetReady = _feet[0] != null || _feet[1] != null;
            return _feetReady;
        }
    }
}
