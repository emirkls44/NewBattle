using UnityEngine;

/// <summary>
/// Atis gorsel efektleri: namlu parlamasi, ilerleyen mermi izi, isabet kivilcimi.
///
/// Eski surum her atista yeni bir GameObject + LineRenderer yaratip yok ediyordu.
/// Otomatik silahla saniyede ~6 atis yapan 16 oyuncu, saniyede ~100 tahsis demek;
/// mobilde bu dogrudan GC takilmasi olarak hissediliyor. Artik sabit boyutlu bir
/// havuz var ve calisma aninda hic tahsis yapilmiyor.
///
/// Mermi izi anlik tam boy cizgi degil: kisa parlak bir parca baslangictan hedefe
/// dogru ilerler. Referans gorsellerdeki "ucusan mermi" hissi bundan geliyor.
/// </summary>
public class ShotTracer : MonoBehaviour
{
    private const int PoolSize = 24;
    private const float TravelSeconds = 0.055f;
    private const float FadeSeconds = 0.05f;
    private const float SegmentLength = 2.4f;

    private static readonly Color DefaultHeadColor = new(1f, 0.95f, 0.45f, 1f);

    private static ShotTracer _instance;
    private static Material _sharedMaterial;

    private Entry[] _pool;
    private int _nextIndex;

    private struct Entry
    {
        public LineRenderer Line;
        public Transform Flash;
        public Transform Impact;
        public Vector3 Start;
        public Vector3 End;
        public float Elapsed;
        public bool Active;
        public bool Hit;
        public Color Head;
    }

    public static void Show(Vector3 start, Vector3 end)
    {
        Show(start, end, false, DefaultHeadColor);
    }

    public static void Show(Vector3 start, Vector3 end, bool hitSomething)
    {
        Show(start, end, hitSomething, DefaultHeadColor);
    }

    /// <summary>Iz rengi silah tanimindan gelir; airdrop silahi farkli renkte parlar.</summary>
    public static void Show(Vector3 start, Vector3 end, bool hitSomething, Color tracerColor)
    {
        Ensure();
        _instance.Emit(start, end, hitSomething, tracerColor);
    }

    private static void Ensure()
    {
        if (_instance != null)
            return;

        GameObject host = new("ShotTracerPool");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<ShotTracer>();
        _instance.BuildPool();
    }

    private void BuildPool()
    {
        _pool = new Entry[PoolSize];

        for (int i = 0; i < PoolSize; i++)
        {
            GameObject tracerObject = new($"Tracer_{i}");
            tracerObject.transform.SetParent(transform, false);

            LineRenderer line = tracerObject.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = 0.085f;
            line.endWidth = 0.02f;
            line.numCapVertices = 2;
            line.useWorldSpace = true;
            line.sharedMaterial = GetMaterial();
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;

            _pool[i] = new Entry
            {
                Line = line,
                Flash = CreateSprite($"Flash_{i}", 0.42f, new Color(1f, 0.92f, 0.5f, 1f)),
                Impact = CreateSprite($"Impact_{i}", 0.30f, new Color(1f, 0.75f, 0.25f, 1f)),
                Active = false
            };
        }
    }

    /// <summary>Namlu parlamasi ve isabet kivilcimi icin yassi, isiktan etkilenmeyen disk.</summary>
    private Transform CreateSprite(string objectName, float size, Color color)
    {
        GameObject sprite = new(objectName);
        sprite.transform.SetParent(transform, false);

        MeshFilter filter = sprite.AddComponent<MeshFilter>();
        filter.sharedMesh = NewBattle.Gameplay.OverlayMeshLibrary.Disc;

        MeshRenderer renderer = sprite.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = NewBattle.Gameplay.OverlayMeshLibrary.OverlayMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        sprite.transform.localScale = Vector3.one * size;
        sprite.SetActive(false);

        MaterialPropertyBlock block = new();
        block.SetColor(NewBattle.Gameplay.OverlayMeshLibrary.BaseColorId, color);
        block.SetFloat(NewBattle.Gameplay.OverlayMeshLibrary.FillId, 1f);
        renderer.SetPropertyBlock(block);

        return sprite.transform;
    }

    private void Emit(Vector3 start, Vector3 end, bool hitSomething, Color tracerColor)
    {
        int index = _nextIndex;
        _nextIndex = (_nextIndex + 1) % _pool.Length;

        _pool[index].Start = start;
        _pool[index].End = end;
        _pool[index].Elapsed = 0f;
        _pool[index].Active = true;
        _pool[index].Hit = hitSomething;
        _pool[index].Head = tracerColor.a > 0.01f ? tracerColor : DefaultHeadColor;

        _pool[index].Line.enabled = true;
        _pool[index].Line.SetPosition(0, start);
        _pool[index].Line.SetPosition(1, start);

        _pool[index].Flash.position = start;
        _pool[index].Flash.gameObject.SetActive(true);

        _pool[index].Impact.position = end;
        _pool[index].Impact.gameObject.SetActive(hitSomething);
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].Active)
                continue;

            _pool[i].Elapsed += deltaTime;
            float elapsed = _pool[i].Elapsed;

            // Namlu parlamasi sadece ilk anda gorunur.
            if (_pool[i].Flash.gameObject.activeSelf && elapsed > FadeSeconds)
                _pool[i].Flash.gameObject.SetActive(false);

            if (elapsed >= TravelSeconds + FadeSeconds)
            {
                Retire(i);
                continue;
            }

            UpdateSegment(i, elapsed);
        }
    }

    private void UpdateSegment(int index, float elapsed)
    {
        Vector3 start = _pool[index].Start;
        Vector3 end = _pool[index].End;

        float travel = Mathf.Clamp01(elapsed / TravelSeconds);
        Vector3 direction = end - start;
        float totalDistance = direction.magnitude;

        if (totalDistance < 0.001f)
        {
            Retire(index);
            return;
        }

        direction /= totalDistance;

        // Parlak parcanin basi hedefe dogru ilerler, kuyruk sabit uzunlukta geride kalir.
        float headDistance = totalDistance * travel;
        float tailDistance = Mathf.Max(0f, headDistance - SegmentLength);

        Vector3 head = start + direction * headDistance;
        Vector3 tail = start + direction * tailDistance;

        _pool[index].Line.SetPosition(0, tail);
        _pool[index].Line.SetPosition(1, head);

        // Hedefe varinca iz soner, kivilcim kisa sure daha kalir.
        float alpha = elapsed <= TravelSeconds
            ? 1f
            : 1f - Mathf.Clamp01((elapsed - TravelSeconds) / FadeSeconds);

        Color head2 = _pool[index].Head;
        head2.a = alpha;

        // Kuyruk basin daha sicak ve saydam hali: tek renk tanimindan turetiliyor,
        // boylece her silah icin ayri iki renk girmek gerekmiyor.
        Color tail2 = head2 * 0.85f;
        tail2.a = alpha * 0.25f;

        _pool[index].Line.startColor = tail2;
        _pool[index].Line.endColor = head2;
    }

    private void Retire(int index)
    {
        _pool[index].Active = false;
        _pool[index].Line.enabled = false;
        _pool[index].Flash.gameObject.SetActive(false);
        _pool[index].Impact.gameObject.SetActive(false);
    }

    private static Material GetMaterial()
    {
        if (_sharedMaterial != null)
            return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        _sharedMaterial = new Material(shader)
        {
            name = "RuntimeShotTracerMaterial"
        };

        return _sharedMaterial;
    }
}
