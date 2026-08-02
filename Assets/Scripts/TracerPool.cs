using UnityEngine;

public class TracerPool : MonoBehaviour
{
    [SerializeField] private GameObject tracerPrefab;
    [SerializeField] private int poolSize = 30;

    // Optimizasyon: Boyutu deðiþmeyen yapýlar için List yerine Array kullanýmý (Zero-GC)
    private GameObject[] pool;
    private int currentIndex = 0;

    private void Start()
    {
        // Bellek tahsisi (Allocation) sadece sahne yüklenirken yapýlýr.
        pool = new GameObject[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            pool[i] = Instantiate(tracerPrefab, transform);
            pool[i].SetActive(false);
        }
    }

    public GameObject GetTracer(Vector3 spawnPosition, Quaternion spawnRotation)
    {
        // Havuz kapasitesi dolduðunda en eski objeyi (aktif olsa bile) baþa sarýp tekrar kullanýr.
        GameObject tracer = pool[currentIndex];

        // Objenin transform deðerlerini güncelle
        tracer.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        // Yeniden baþlatma tetiklemesi için kapatýp aç
        if (tracer.activeSelf) tracer.SetActive(false);
        tracer.SetActive(true);

        // Ýndeksi dairesel olarak kaydýr (Ring Buffer)
        currentIndex = (currentIndex + 1) % poolSize;
        return tracer;
    }
}