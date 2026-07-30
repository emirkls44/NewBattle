using Fusion;
using UnityEngine;

public class PlayerHide : NetworkBehaviour
{
    [Header("Ayarlar")]
    public float revealDistance = 3f;
    public GameObject footprintIcon;

    // AÐ OPTÝMÝZASYONU: Gizlilik durumunu að üzerinden Zero-GC senkronize ediyoruz.
    [Networked, OnChangedRender(nameof(OnHiddenStatusChanged))]
    public NetworkBool isHidden { get; set; }

    private bool inBush = false;
    private Vector3 lastPosition;

    // PERFORMANS: Sýk kullanýlan referanslar önbelleðe (Cache) alýndý.
    private Renderer[] _renderers;
    private MaterialPropertyBlock _propBlock;
    private Collider[] _enemyColliders = new Collider[5]; // NonAlloc için sabit dizi

    public override void Spawned()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _propBlock = new MaterialPropertyBlock();
        lastPosition = transform.position;

        if (footprintIcon != null) footprintIcon.SetActive(false);
    }

    public override void FixedUpdateNetwork()
    {
        // Yalnýzca karakterin sahibi bu mantýðý hesaplar, diðer cihazlar sonucu kopyalar.
        if (!HasStateAuthority) return;

        if (inBush)
        {
            float speedSq = (transform.position - lastPosition).sqrMagnitude; // Karekök hesaplamadan kaçýnma
            bool isMoving = speedSq > 0.01f; // (0.1f * 0.1f)

            bool enemyTooClose = CheckEnemyProximity();

            if (enemyTooClose)
            {
                isHidden = false;
            }
            else
            {
                isHidden = true;
                // Ayak izi sadece yerel oyuncuda veya StateAuthority'de iþlenmeli (görsel tercih)
                if (footprintIcon != null && footprintIcon.activeSelf != isMoving)
                    footprintIcon.SetActive(isMoving);
            }
        }
        else
        {
            isHidden = false;
            if (footprintIcon != null && footprintIcon.activeSelf)
                footprintIcon.SetActive(false);
        }

        lastPosition = transform.position;
    }

    bool CheckEnemyProximity()
    {
        // PERFORMANS: FindGameObjectsWithTag yerine GC oluþturmayan NonAlloc fizik küresi kullanýldý.
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, revealDistance, _enemyColliders);
        for (int i = 0; i < hitCount; i++)
        {
            if (_enemyColliders[i].CompareTag("Enemy"))
            {
                return true;
            }
        }
        return false;
    }

    private void OnHiddenStatusChanged()
    {
        SetVisible(!isHidden);
    }

    void SetVisible(bool isVisible)
    {
        float targetAlpha = isVisible ? 1f : 0.3f;

        foreach (Renderer r in _renderers)
        {
            if (footprintIcon != null && r.gameObject == footprintIcon) continue;

            // PERFORMANS: .material yerine MaterialPropertyBlock kullanarak materyal kopyalanmasý (GC) engellendi.
            r.GetPropertyBlock(_propBlock);

            // Mevcut rengi koruyup sadece Alpha'yý güncellemek istiyorsak:
            Color baseColor = r.sharedMaterial.HasProperty("_Color") ? r.sharedMaterial.color : Color.white;
            baseColor.a = targetAlpha;

            _propBlock.SetColor("_Color", baseColor); // URP kullanýyorsan "_BaseColor" olarak deðiþtir
            r.SetPropertyBlock(_propBlock);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bush")) inBush = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Bush")) inBush = false;
    }
}