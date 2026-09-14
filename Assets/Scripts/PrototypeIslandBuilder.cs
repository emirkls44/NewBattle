using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class PrototypeIslandBuilder : MonoBehaviour
{
    [SerializeField] private bool buildOnAwake = true;

    private Transform _root;
    private Material _waterMaterial;
    private Material _grassMaterial;
    private Material _roadMaterial;
    private Material _buildingMaterial;
    private Material _rockMaterial;
    private Material _treeMaterial;
    private Material _trunkMaterial;
    private Material _crateMaterial;

    private void Awake()
    {
        if (buildOnAwake)
            BuildIsland();
    }

    [ContextMenu("Build Prototype Island")]
    public void BuildIsland()
    {
        Transform existing = transform.Find("GeneratedIsland");
        if (existing != null)
            return;

        GameObject rootObject = new("GeneratedIsland");
        rootObject.transform.SetParent(transform, false);
        _root = rootObject.transform;

        CreateMaterials();
        CreateTerrain();
        CreateRoads();
        CreateBuildings();
        CreateRocks();
        CreateTrees();
        CreateCrates();
    }

    private void CreateMaterials()
    {
        _waterMaterial = CreateMaterial("Water", new Color(0.12f, 0.48f, 0.68f));
        _grassMaterial = CreateMaterial("Grass", new Color(0.48f, 0.68f, 0.25f));
        _roadMaterial = CreateMaterial("Road", new Color(0.62f, 0.56f, 0.43f));
        _buildingMaterial = CreateMaterial("Building", new Color(0.76f, 0.43f, 0.28f));
        _rockMaterial = CreateMaterial("Rock", new Color(0.43f, 0.42f, 0.39f));
        _treeMaterial = CreateMaterial("Tree", new Color(0.20f, 0.48f, 0.18f));
        _trunkMaterial = CreateMaterial("Trunk", new Color(0.38f, 0.22f, 0.10f));
        _crateMaterial = CreateMaterial("Crate", new Color(0.58f, 0.33f, 0.13f));
    }

    private void CreateTerrain()
    {
        CreatePrimitive(
            PrimitiveType.Cube,
            "Water",
            new Vector3(0f, -1.25f, 0f),
            new Vector3(110f, 1f, 110f),
            _waterMaterial,
            false
        );

        GameObject island = CreatePrimitive(
            PrimitiveType.Cylinder,
            "Island",
            new Vector3(0f, -0.5f, 0f),
            new Vector3(70f, 0.5f, 70f),
            _grassMaterial,
            false
        );

        // Cylinder primitive'in CapsuleCollider'i buyutulunce dev bir kureye donusur.
        // Statik ada zemini icin gorunur mesh ile birebir MeshCollider kullaniyoruz.
        Collider oldCollider = island.GetComponent<Collider>();
        if (oldCollider != null)
            DestroyImmediate(oldCollider);

        MeshCollider islandCollider = island.AddComponent<MeshCollider>();
        islandCollider.sharedMesh = island.GetComponent<MeshFilter>().sharedMesh;
    }

    private void CreateRoads()
    {
        CreateBox("MainRoad", new Vector3(0f, 0.03f, 0f), new Vector3(7f, 0.06f, 58f), _roadMaterial, false);
        CreateBox("CrossRoad", new Vector3(0f, 0.035f, 2f), new Vector3(55f, 0.07f, 6f), _roadMaterial, false);
        CreateBox("NorthRoad", new Vector3(-17f, 0.04f, 17f), new Vector3(22f, 0.08f, 4f), _roadMaterial, false);
        CreateBox("SouthRoad", new Vector3(16f, 0.04f, -18f), new Vector3(20f, 0.08f, 4f), _roadMaterial, false);
    }

    private void CreateBuildings()
    {
        Vector3[] positions =
        {
            new(-18f, 1.5f, 20f), new(-10f, 1.5f, 20f),
            new(14f, 2f, 17f), new(22f, 1.5f, 13f),
            new(-20f, 1.5f, -15f), new(-13f, 2f, -21f),
            new(15f, 1.5f, -18f), new(23f, 2f, -20f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            float height = positions[i].y * 2f;
            Vector3 scale = i % 2 == 0
                ? new Vector3(6f, height, 5f)
                : new Vector3(4.5f, height, 6f);

            CreateBox($"Building_{i + 1}", positions[i], scale, _buildingMaterial, true);
        }
    }

    private void CreateRocks()
    {
        Vector3[] positions =
        {
            new(-26f, 1f, 4f), new(-22f, 1.2f, 11f), new(25f, 1f, 3f),
            new(20f, 1.1f, -8f), new(4f, 1f, 25f), new(-3f, 1.2f, -27f),
            new(9f, 0.9f, -11f), new(-9f, 1f, 10f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 scale = new(2.5f + i % 3, 1.8f + i % 2, 2.2f + (i + 1) % 3);
            CreatePrimitive(PrimitiveType.Sphere, $"Rock_{i + 1}", positions[i], scale, _rockMaterial, true);
        }
    }

    private void CreateTrees()
    {
        Vector3[] positions =
        {
            new(-27f, 0f, 24f), new(-23f, 0f, 28f), new(-29f, 0f, 17f),
            new(27f, 0f, 25f), new(23f, 0f, 29f), new(29f, 0f, 18f),
            new(-27f, 0f, -24f), new(-22f, 0f, -29f), new(27f, 0f, -26f),
            new(5f, 0f, 29f), new(-7f, 0f, 28f), new(5f, 0f, -29f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 position = positions[i];
            CreatePrimitive(PrimitiveType.Cylinder, $"TreeTrunk_{i + 1}", position + Vector3.up * 1.5f, new Vector3(0.65f, 1.5f, 0.65f), _trunkMaterial, true);
            CreatePrimitive(PrimitiveType.Sphere, $"TreeTop_{i + 1}", position + Vector3.up * 3.7f, new Vector3(3.2f, 2.5f, 3.2f), _treeMaterial, false);
        }
    }

    private void CreateCrates()
    {
        Vector3[] positions =
        {
            new(-6f, 0.75f, 17f), new(7f, 0.75f, 14f),
            new(-17f, 0.75f, -7f), new(18f, 0.75f, -5f),
            new(-5f, 0.75f, -18f), new(8f, 0.75f, 6f)
        };

        for (int i = 0; i < positions.Length; i++)
            CreateBox($"Crate_{i + 1}", positions[i], new Vector3(1.5f, 1.5f, 1.5f), _crateMaterial, true);
    }

    private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Material material, bool collision)
    {
        return CreatePrimitive(PrimitiveType.Cube, objectName, position, scale, material, collision);
    }

    private GameObject CreatePrimitive(
        PrimitiveType primitiveType,
        string objectName,
        Vector3 position,
        Vector3 scale,
        Material material,
        bool collision)
    {
        GameObject created = GameObject.CreatePrimitive(primitiveType);
        created.name = objectName;
        created.transform.SetParent(_root, false);
        created.transform.position = position;
        created.transform.localScale = scale;
        created.GetComponent<Renderer>().sharedMaterial = material;

        Collider collider = created.GetComponent<Collider>();
        if (!collision && collider != null)
            DestroyImmediate(collider);

        return created;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new(shader)
        {
            name = materialName,
            color = color
        };

        return material;
    }
}
