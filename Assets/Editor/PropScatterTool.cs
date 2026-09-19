using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NewBattle.EditorTools
{
    /// <summary>
    /// Sahne goruntusunde firca ile agac / kaya / cali dagitir.
    ///
    /// Neden arac: 300 agaci elle koymak hem saatler suruyor hem de elle
    /// dizildigi belli oluyor - insan eli farkinda olmadan duzenli araliklar
    /// birakiyor. Rastgele donus, olcu ve konum dogal bir dagilim veriyor.
    ///
    /// Neyi ELLE koymaya devam etmelisin: bolgeyi tanimlayan yapilar.
    /// Evler, kule, yol kenari. Onlar az ve karakterli olmali; dagitilmis
    /// bir ev haritayi anlamsizlastirir.
    ///
    /// Konan her nesne prefab ornegi olarak kaliyor; prefab'i sonradan
    /// degistirirsen haritadaki hepsi guncellenir.
    /// </summary>
    public class PropScatterTool : EditorWindow
    {
        [System.Serializable]
        private class Entry
        {
            public GameObject prefab;

            [Min(0f)]
            public float weight = 1f;
        }

        private readonly List<Entry> _palette = new() { new Entry() };

        private Transform _parent;

        private float _brushRadius = 6f;
        private int _perStroke = 4;
        private float _minSpacing = 1.8f;

        private Vector2 _scaleRange = new(0.85f, 1.2f);
        private float _maxTilt = 4f;
        private bool _randomYaw = true;

        private bool _painting;
        private Vector2 _scroll;

        /// <summary>
        /// Ayni firca darbesinde ayni yere ust uste koymayi engellemek icin
        /// konan noktalari tutuyoruz. Her seferinde sahneyi taramak yerine
        /// bunu kullanmak, yuzlerce prop konduktan sonra fircanin
        /// yavaslamasini onluyor.
        /// </summary>
        private readonly List<Vector3> _placed = new();

        [MenuItem("Tools/NewBattle/Prop Dagitici")]
        private static void Open()
        {
            GetWindow<PropScatterTool>("Prop Dagitici").minSize =
                new Vector2(430f, 460f);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            _painting = false;
        }

        // ------------------------------------------------------------------
        // Pencere
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.HelpBox(
                "Prefab'lari sec, hedef klasoru ver, 'Fircayi Ac' de ve " +
                "Scene penceresinde surukle.\n\n" +
                "Shift + surukle = fircanin altindakileri siler.",
                MessageType.Info);

            EditorGUILayout.Space(6f);

            DrawPalette();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Hedef", EditorStyles.boldLabel);

            _parent = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Ust nesne",
                    "Konan proplar bunun altina girer. Bolge basina bir bos " +
                    "nesne ac: Zone_KampciCenneti gibi."),
                _parent, typeof(Transform), true);

            if (_parent == null)
            {
                EditorGUILayout.HelpBox(
                    "Ust nesne bos. Proplar sahnenin kokune konur ve " +
                    "hiyerarsi kisa surede okunmaz hale gelir.",
                    MessageType.Warning);

                if (GUILayout.Button("Bos bir bolge nesnesi olustur"))
                    CreateZoneRoot();
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Firca", EditorStyles.boldLabel);

            _brushRadius = EditorGUILayout.Slider("Yaricap", _brushRadius, 1f, 40f);
            _perStroke = EditorGUILayout.IntSlider("Darbe basina", _perStroke, 1, 30);

            _minSpacing = EditorGUILayout.Slider(
                new GUIContent("En az aralik",
                    "Bundan yakin iki prop konmaz. Ic ice gecmis agaclar " +
                    "hem cirkin hem de botlari sikistirir."),
                _minSpacing, 0.2f, 12f);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Cesitlilik", EditorStyles.boldLabel);

            EditorGUILayout.MinMaxSlider(
                new GUIContent("Olcu araligi"),
                ref _scaleRange.x, ref _scaleRange.y, 0.4f, 2.5f);

            EditorGUILayout.LabelField(
                $"    {_scaleRange.x:0.00}x - {_scaleRange.y:0.00}x");

            _randomYaw = EditorGUILayout.Toggle("Rastgele donus", _randomYaw);

            _maxTilt = EditorGUILayout.Slider(
                new GUIContent("Egilme (derece)",
                    "Kucuk bir egim dogal duruyor. Evlerde 0 birak."),
                _maxTilt, 0f, 15f);

            EditorGUILayout.Space(12f);

            GUI.backgroundColor = _painting ? new Color(1f, 0.5f, 0.5f) : Color.white;

            if (GUILayout.Button(_painting ? "Fircayi Kapat" : "Fircayi Ac",
                    GUILayout.Height(36f)))
            {
                _painting = !_painting;
                _placed.Clear();

                if (_painting)
                    RememberExisting();

                SceneView.RepaintAll();
            }

            GUI.backgroundColor = Color.white;

            if (_painting && !HasUsablePrefab())
            {
                EditorGUILayout.HelpBox(
                    "Paletle en az bir prefab olmali.", MessageType.Error);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Dagittiktan sonra 'Harita Navigasyonu > NavMesh Uret' demeyi " +
                "unutma. Yoksa botlar yeni agaclari gormez.",
                MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("Palet", EditorStyles.boldLabel);

            int removeAt = -1;

            for (int i = 0; i < _palette.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                _palette[i].prefab = (GameObject)EditorGUILayout.ObjectField(
                    _palette[i].prefab, typeof(GameObject), false);

                _palette[i].weight = EditorGUILayout.FloatField(
                    _palette[i].weight, GUILayout.Width(50f));

                if (GUILayout.Button("-", GUILayout.Width(24f)))
                    removeAt = i;

                EditorGUILayout.EndHorizontal();
            }

            if (removeAt >= 0 && _palette.Count > 1)
                _palette.RemoveAt(removeAt);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Satir ekle"))
                _palette.Add(new Entry());

            if (GUILayout.Button("Secilenleri ekle"))
                AddSelection();

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                "Sagdaki sayi agirlik: buyuk olan daha sik cikar.",
                EditorStyles.miniLabel);
        }

        private void AddSelection()
        {
            foreach (Object o in Selection.objects)
            {
                if (o is not GameObject go || !AssetDatabase.Contains(go))
                    continue;

                _palette.Add(new Entry { prefab = go });
            }

            _palette.RemoveAll(e => e.prefab == null);

            if (_palette.Count == 0)
                _palette.Add(new Entry());
        }

        private void CreateZoneRoot()
        {
            GameObject root = new("Zone_YeniBolge");
            Undo.RegisterCreatedObjectUndo(root, "Bolge olustur");
            _parent = root.transform;
            Selection.activeObject = root;
        }

        // ------------------------------------------------------------------
        // Sahne fircasi
        // ------------------------------------------------------------------

        private void OnSceneGUI(SceneView view)
        {
            if (!_painting)
                return;

            Event e = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            // Firca acikken tiklamanin nesne SECMESINI engelliyoruz; yoksa
            // her darbe ayni zamanda secimi degistirir ve calismak imkansiz
            // hale gelir.
            HandleUtility.AddDefaultControl(controlId);

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 5000f,
                    ~0, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            bool erasing = e.shift;

            Handles.color = erasing
                ? new Color(1f, 0.35f, 0.35f, 0.9f)
                : new Color(0.4f, 1f, 0.5f, 0.9f);

            Handles.DrawWireDisc(hit.point, hit.normal, _brushRadius);
            Handles.DrawWireDisc(hit.point, hit.normal, _brushRadius * 0.5f);

            view.Repaint();

            bool stroke = e.type == EventType.MouseDown || e.type == EventType.MouseDrag;

            if (!stroke || e.button != 0 || e.alt)
                return;

            if (erasing)
                Erase(hit.point);
            else
                Paint(hit.point);

            GUIUtility.hotControl = controlId;
            e.Use();
        }

        private void Paint(Vector3 center)
        {
            if (!HasUsablePrefab())
                return;

            for (int i = 0; i < _perStroke; i++)
            {
                Vector2 offset = Random.insideUnitCircle * _brushRadius;
                Vector3 probe = center + new Vector3(offset.x, 0f, offset.y);

                if (!GroundAt(probe, out Vector3 point, out Vector3 normal))
                    continue;

                if (TooClose(point))
                    continue;

                GameObject prefab = PickPrefab();

                if (prefab == null)
                    continue;

                GameObject instance =
                    (GameObject)PrefabUtility.InstantiatePrefab(prefab, _parent);

                if (instance == null)
                    continue;

                instance.transform.position = point;
                instance.transform.rotation = BuildRotation(normal);

                float scale = Random.Range(_scaleRange.x, _scaleRange.y);
                instance.transform.localScale = Vector3.one * scale;

                Undo.RegisterCreatedObjectUndo(instance, "Prop dagit");
                _placed.Add(point);
            }
        }

        /// <summary>
        /// Zemin yuksekligini yukaridan bakan bir isinla buluyor.
        ///
        /// Fircanin degdigi noktanin yuksekligini dogrudan kullanmiyoruz:
        /// egimli ya da katmanli arazide firca dairesinin kenari bambaska
        /// bir yukseklikte olabiliyor ve proplar havada kaliyor.
        /// </summary>
        private static bool GroundAt(Vector3 probe, out Vector3 point, out Vector3 normal)
        {
            Ray down = new(probe + Vector3.up * 500f, Vector3.down);

            if (Physics.Raycast(down, out RaycastHit hit, 2000f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
                return true;
            }

            point = probe;
            normal = Vector3.up;
            return false;
        }

        private Quaternion BuildRotation(Vector3 normal)
        {
            float yaw = _randomYaw ? Random.Range(0f, 360f) : 0f;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);

            if (_maxTilt <= 0f)
                return rotation;

            // Egimi zemin normalinden bagimsiz, kucuk ve rastgele tutuyoruz.
            // Zemine tam hizalamak agaclari yamacta yatik gosterir; gercek
            // agaclar dik buyur.
            Vector3 tilt = new(
                Random.Range(-_maxTilt, _maxTilt),
                0f,
                Random.Range(-_maxTilt, _maxTilt));

            return Quaternion.Euler(tilt) * rotation;
        }

        private bool TooClose(Vector3 point)
        {
            float sqr = _minSpacing * _minSpacing;

            foreach (Vector3 p in _placed)
            {
                if ((p - point).sqrMagnitude < sqr)
                    return true;
            }

            return false;
        }

        private void Erase(Vector3 center)
        {
            if (_parent == null)
                return;

            float sqr = _brushRadius * _brushRadius;

            for (int i = _parent.childCount - 1; i >= 0; i--)
            {
                Transform child = _parent.GetChild(i);

                if ((child.position - center).sqrMagnitude > sqr)
                    continue;

                _placed.RemoveAll(p => (p - child.position).sqrMagnitude < 0.0001f);
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Hedefin altindaki mevcut proplari aralik kontrolune dahil eder.
        ///
        /// Olmasaydi fircayi her acisinda daha once konmus agaclarin
        /// uzerine yenilerini koyardi.
        /// </summary>
        private void RememberExisting()
        {
            if (_parent == null)
                return;

            foreach (Transform child in _parent)
                _placed.Add(child.position);
        }

        private bool HasUsablePrefab()
        {
            foreach (Entry e in _palette)
            {
                if (e.prefab != null && e.weight > 0f)
                    return true;
            }

            return false;
        }

        private GameObject PickPrefab()
        {
            float total = 0f;

            foreach (Entry e in _palette)
            {
                if (e.prefab != null)
                    total += Mathf.Max(0f, e.weight);
            }

            if (total <= 0f)
                return null;

            float roll = Random.Range(0f, total);

            foreach (Entry e in _palette)
            {
                if (e.prefab == null)
                    continue;

                roll -= Mathf.Max(0f, e.weight);

                if (roll <= 0f)
                    return e.prefab;
            }

            return null;
        }
    }
}
