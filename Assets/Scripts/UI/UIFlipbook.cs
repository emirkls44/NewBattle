using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>Image uzerinde kare kare animasyon (yukleme ekranindaki kosan karakter).</summary>
    [RequireComponent(typeof(Image))]
    public class UIFlipbook : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField, Min(1f)] private float framesPerSecond = 14f;

        private Image _image;

        private void Awake() => _image = GetComponent<Image>();

        private void Update()
        {
            if (frames == null || frames.Length == 0)
                return;

            int index = Mathf.FloorToInt(Time.unscaledTime * framesPerSecond) % frames.Length;
            _image.sprite = frames[index];
        }
    }
}
