using UnityEngine;

namespace NewBattle.UI
{
    /// <summary>Nesneyi surekli dondurur (yukleme gostergesi).</summary>
    public class UISpin : MonoBehaviour
    {
        [Tooltip("Saniyede derece. Eksi = saat yonu.")]
        [SerializeField] private float degreesPerSecond = -320f;

        private void Update()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
        }
    }
}
