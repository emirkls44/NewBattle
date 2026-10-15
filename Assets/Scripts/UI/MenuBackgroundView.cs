using NewBattle.Progression;
using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Ana menunun arkasina magazadan alinan arka plani koyar. Secili yoksa sahnedeki
    /// (sari) arka plan gorunur. Resim ekrani kaplayacak sekilde orantili buyutulur.
    /// MainMenuScreen bunu kendisi ekler.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuBackgroundView : MonoBehaviour
    {
        private Image _image;
        private AspectRatioFitter _fitter;

        private void OnEnable()
        {
            MenuBackgroundLibrary.Changed += Apply;
            Apply();
        }

        private void OnDisable() => MenuBackgroundLibrary.Changed -= Apply;

        private void Apply()
        {
            MenuBackgroundLibrary library = MenuBackgroundLibrary.Instance;
            MenuBackground current = library != null ? library.Current : null;
            Sprite sprite = current != null ? current.image : null;

            if (sprite == null)
            {
                if (_image != null)
                    _image.gameObject.SetActive(false);
                return;
            }

            EnsureImage();
            _image.sprite = sprite;
            _fitter.aspectRatio = sprite.rect.height > 0f ? sprite.rect.width / sprite.rect.height : 16f / 9f;
            _image.gameObject.SetActive(true);
            _image.transform.SetAsFirstSibling(); // sari arka planin ustunde, her seyin altinda
        }

        private void EnsureImage()
        {
            if (_image != null)
                return;

            var holder = new GameObject("SeciliArkaPlan", typeof(RectTransform));
            var rect = (RectTransform)holder.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            _image = holder.AddComponent<Image>();
            _image.raycastTarget = false;
            _fitter = holder.AddComponent<AspectRatioFitter>();
            _fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        }
    }
}
