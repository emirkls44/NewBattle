using UnityEngine;

public class MobileMaterialOptimizer : MonoBehaviour
{
    [Tooltip("Oyuncunun Mesh Renderer bileþenini buraya sürükle.")]
    [SerializeField] private Renderer _playerRenderer;

    void Start()
    {
        // Doðrulama kontrolü: Eðer referans yoksa NullReferenceException alýp performansý baltalamayalým.
        if (_playerRenderer != null && _playerRenderer.sharedMaterial != null)
        {
            // DÝKKAT: .material kullanmak yeni bir instance (kopya) yaratýr ve Garbage Collector'ý tetikler.
            // Bu yüzden allocation yaratmayan .sharedMaterial kullanarak doðrudan ana materyale müdahale ediyoruz.

            // URP Simple Lit shader'ýnda speküler (parlama) ve çevresel yansýmalarý zorla kapatýyoruz.
            _playerRenderer.sharedMaterial.SetFloat("_SpecularHighlights", 0f);
            _playerRenderer.sharedMaterial.SetFloat("_EnvironmentReflections", 0f);

            // Render maliyetini daha da düþürmek için Global Illumination (Küresel Aydýnlatma) tepkisini kesiyoruz.
            _playerRenderer.sharedMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
    }
}