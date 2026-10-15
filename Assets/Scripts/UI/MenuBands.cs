using UnityEngine;
using UnityEngine.UI;

namespace NewBattle.UI
{
    /// <summary>
    /// Ana menude arka planin ustunde, karakterin altinda iki bant:
    ///   - Zemin: altta, ustunde koyu cizgi olan beyaz alan (karakterin bastigi yer)
    ///   - Tavan: ustte, altinda koyu cizgi olan beyaz alan
    /// Yukseklik, cizgi kalinligi ve renkler buradan; Play'e basmadan da aninda gorunur.
    /// Tools > NewBattle > Menü > Malatya Ana Menü (deneme) kurar (AnaMenu uzerinde).
    /// </summary>
    [ExecuteAlways]
    public class MenuBands : MonoBehaviour
    {
        [Header("Alt (Zemin)")]
        [SerializeField] private RectTransform bottom;
        public bool showBottom = true;
        [Tooltip("Alttaki beyaz alanin yuksekligi = cizginin ekranin altindan uzakligi (1920x1080 olcusunde).")]
        [Range(0f, 700f)] public float bottomHeight = 230f;

        [Header("Ust (Tavan)")]
        [SerializeField] private RectTransform top;
        public bool showTop = true;
        [Tooltip("Ustteki beyaz alanin yuksekligi = cizginin ekranin ustunden uzakligi.")]
        [Range(0f, 700f)] public float topHeight = 120f;

        [Header("Gorunum")]
        [Range(0f, 40f)] public float lineThickness = 12f;
        public Color lineColor = new(0.306f, 0.176f, 0.078f, 1f);  // koyu ahsap
        public Color fillColor = new(0.478f, 0.29f, 0.149f, 1f);   // ahsap kahvesi

        [Tooltip("Kurulum aracinin renk temasi surumu (elle degistirme).")]
        [SerializeField, HideInInspector] private int themeVersion;
        public int ThemeVersion { get => themeVersion; set => themeVersion = value; }

        private void OnEnable() => Apply();
        private void OnValidate() => Apply();

        private void Update()
        {
            if (!Application.isPlaying)
                Apply();
        }

        public void Apply()
        {
            ApplyBand(bottom, showBottom, bottomHeight);
            ApplyBand(top, showTop, topHeight);
        }

        private void ApplyBand(RectTransform band, bool show, float height)
        {
            if (band == null)
                return;

            if (band.gameObject.activeSelf != show)
                band.gameObject.SetActive(show);
            if (!show)
                return;

            if (!Mathf.Approximately(band.sizeDelta.y, height))
                band.sizeDelta = new Vector2(band.sizeDelta.x, height);

            if (band.TryGetComponent(out Image fill) && fill.color != fillColor)
                fill.color = fillColor;

            if (band.Find("Cizgi") is RectTransform line)
            {
                if (!Mathf.Approximately(line.sizeDelta.y, lineThickness))
                    line.sizeDelta = new Vector2(line.sizeDelta.x, lineThickness);
                if (line.TryGetComponent(out Image lineImage) && lineImage.color != lineColor)
                    lineImage.color = lineColor;
            }
        }
    }
}
