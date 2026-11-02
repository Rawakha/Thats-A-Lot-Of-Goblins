using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Builds a blockout of a small town at the world origin from primitives, for testing flying, strafing and
// destruction: a crossroads with a ring road, a town square with a church and well, houses along the streets,
// barns and haystacks in the back lots, fields, a windmill, a watchtower and trees round the edge.
// Tools > Goblins > Build Town Blockout. Building again replaces it (undoable); the seed changes the layout.
//
// Everything breakable is a plain scaled cube under "Buildings", so select those and use
// GameObject > Goblins > Make Destructible. Colours are simple URP Lit materials saved in Assets/Art/Blockout.
//
// Sizes are tuned for the plane: ~32 m/s at ~15 m cruise, so the town is ~170 m across, a few seconds to cross.
public static class Level_TownBlockout
{
    private const string RootName = "--- Town Blockout";
    private const string MaterialFolder = "Assets/Art/Blockout";

    private const float RoadWidth = 8f;
    private const float RingHalf = 70f;         // the ring road's distance from the centre
    private const float SquareHalf = 18f;       // the town square's half size

    private static System.Random random;
    private static readonly List<Rect> occupied = new List<Rect>();
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

    private static readonly Color[] WallColours =
    {
        new Color(0.85f, 0.78f, 0.62f),     // sandstone
        new Color(0.78f, 0.55f, 0.42f),     // brick
        new Color(0.9f, 0.88f, 0.82f),      // whitewash
        new Color(0.62f, 0.66f, 0.7f),      // grey stone
        new Color(0.72f, 0.6f, 0.45f),      // timber
        new Color(0.66f, 0.5f, 0.55f)       // plum plaster
    };

    [MenuItem("Tools/Goblins/Build Town Blockout")]
    private static void Build() => Build(1);

    [MenuItem("Tools/Goblins/Build Town Blockout (New Seed)")]
    private static void BuildNewSeed() => Build(Random.Range(1, 100000));

    [MenuItem("Tools/Goblins/Clear Town Blockout")]
    private static void Clear()
    {
        GameObject existing = GameObject.Find(RootName);

        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    private static void Build(int seed)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Build Town Blockout");
        Clear();

        random = new System.Random(seed);
        occupied.Clear();
        materials.Clear();

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Town Blockout");

        Transform roads = Group(root, "Roads");
        Transform buildings = Group(root, "Buildings");
        Transform props = Group(root, "Props");
        Transform nature = Group(root, "Nature");

        BuildRoads(roads);
        BuildSquare(buildings, props);
        BuildStreets(buildings);
        BuildBackLots(buildings, props);
        BuildOutskirts(buildings, props, nature);

        Selection.activeGameObject = root;
        Debug.Log($"Town blockout built (seed {seed}): {buildings.childCount} buildings. Select them under Buildings " +
                  "and use GameObject > Goblins > Make Destructible.", root);
    }

    #region Layout
    // A crossroads through the centre and a square ring road round it
    private static void BuildRoads(Transform parent)
    {
        float length = RingHalf * 2f + RoadWidth;

        Flat(parent, "Main Street", new Vector3(0f, 0f, 0f), new Vector2(RoadWidth, length), "Road");
        Flat(parent, "Cross Street", new Vector3(0f, 0f, 0f), new Vector2(length, RoadWidth), "Road");
        Flat(parent, "Ring North", new Vector3(0f, 0f, RingHalf), new Vector2(length, RoadWidth), "Road");
        Flat(parent, "Ring South", new Vector3(0f, 0f, -RingHalf), new Vector2(length, RoadWidth), "Road");
        Flat(parent, "Ring East", new Vector3(RingHalf, 0f, 0f), new Vector2(RoadWidth, length), "Road");
        Flat(parent, "Ring West", new Vector3(-RingHalf, 0f, 0f), new Vector2(RoadWidth, length), "Road");

        // Roads out of town
        Flat(parent, "North Road", new Vector3(0f, 0f, RingHalf + 45f), new Vector2(RoadWidth, 90f), "Dirt");
        Flat(parent, "East Road", new Vector3(RingHalf + 45f, 0f, 0f), new Vector2(90f, RoadWidth), "Dirt");

        Reserve(Vector2.zero, new Vector2(RoadWidth, length));
        Reserve(Vector2.zero, new Vector2(length, RoadWidth));
        Reserve(new Vector2(0f, RingHalf), new Vector2(length, RoadWidth));
        Reserve(new Vector2(0f, -RingHalf), new Vector2(length, RoadWidth));
        Reserve(new Vector2(RingHalf, 0f), new Vector2(RoadWidth, length));
        Reserve(new Vector2(-RingHalf, 0f), new Vector2(RoadWidth, length));
        Reserve(new Vector2(0f, RingHalf + 45f), new Vector2(RoadWidth, 90f));
        Reserve(new Vector2(RingHalf + 45f, 0f), new Vector2(90f, RoadWidth));
    }

    // Paved square in the middle: a church on its north-west corner, a well and market stalls
    private static void BuildSquare(Transform buildings, Transform props)
    {
        Flat(props, "Town Square", new Vector3(0f, 0.01f, 0f), new Vector2(SquareHalf * 2f, SquareHalf * 2f), "Paving");
        Reserve(Vector2.zero, new Vector2(SquareHalf * 2f, SquareHalf * 2f));

        // Church: a long nave and a tall tower at its west end
        Vector3 nave = new Vector3(-SquareHalf - 12f, 0f, SquareHalf + 14f);
        Block(buildings, "Church Nave", nave + new Vector3(0f, 0f, 2f), new Vector3(11f, 10f, 22f), "Stone");
        Block(buildings, "Church Tower", nave + new Vector3(0f, 0f, -12f), new Vector3(6f, 24f, 6f), "Stone");
        Reserve(new Vector2(nave.x, nave.z - 3f), new Vector2(13f, 32f));

        Cylinder(props, "Well", new Vector3(6f, 0f, -6f), 2.4f, 1.1f, "Stone");

        for (int i = 0; i < 4; i++)
        {
            Vector3 stall = new Vector3(-9f + i * 5f, 0f, 9f);
            Block(props, $"Market Stall {i + 1}", stall, new Vector3(3.5f, 2.6f, 2.5f), i % 2 == 0 ? "Canvas Red" : "Canvas Blue");
        }
    }

    // Houses facing both sides of the main and cross streets, between the square and the ring road
    private static void BuildStreets(Transform buildings)
    {
        int count = 0;

        for (int axis = 0; axis < 2; axis++)
        for (int side = -1; side <= 1; side += 2)
        for (int direction = -1; direction <= 1; direction += 2)
        {
            // Walk outward from the square along one half of one street, on one side of it
            for (float along = SquareHalf + 6f; along < RingHalf - 10f; along += 12f + Range(0f, 3f))
            {
                Vector3 size = new Vector3(Range(6f, 10f), Pick(5f, 6.5f, 8f, 11f), Range(7f, 10f));
                float back = RoadWidth * 0.5f + 3f + size.z * 0.5f;

                // Facing the street: along the street is x for the main street (axis 0), z for the cross street
                Vector3 position = axis == 0
                    ? new Vector3(side * back, 0f, direction * along)
                    : new Vector3(direction * along, 0f, side * back);

                Vector3 footprint = axis == 0 ? new Vector3(size.z, size.y, size.x) : size;

                if (TryReserve(position, footprint, 1.5f))
                    Block(buildings, $"House {++count:00}", position, footprint, Wall());
            }
        }
    }

    // Behind the street fronts: back-row houses, barns, sheds and haystacks inside the ring road
    private static void BuildBackLots(Transform buildings, Transform props)
    {
        int houses = 0;
        int barns = 0;
        int stacks = 0;

        for (int attempt = 0; attempt < 400; attempt++)
        {
            Vector3 position = new Vector3(Range(-RingHalf + 8f, RingHalf - 8f), 0f, Range(-RingHalf + 8f, RingHalf - 8f));
            float roll = (float)random.NextDouble();

            if (roll < 0.55f)
            {
                Vector3 size = new Vector3(Range(6f, 9f), Pick(4.5f, 6f, 7.5f), Range(6f, 9f));

                if (TryReserve(position, size, 2f))
                    Block(buildings, $"Back House {++houses:00}", position, size, Wall());
            }
            else if (roll < 0.7f)
            {
                Vector3 size = new Vector3(Range(10f, 13f), Range(7f, 9f), Range(15f, 19f));

                if (Random01() < 0.5f)
                    size = new Vector3(size.z, size.y, size.x);

                if (TryReserve(position, size, 3f))
                    Block(buildings, $"Barn {++barns:00}", position, size, "Barn");
            }
            else if (roll < 0.85f)
            {
                if (TryReserve(position, new Vector3(3.5f, 2f, 3.5f), 1f))
                    Haystack(props, $"Haystack {++stacks:00}", position);
            }
            else
            {
                Vector3 size = new Vector3(Range(3f, 4.5f), 3f, Range(3f, 4.5f));

                if (TryReserve(position, size, 1.5f))
                    Block(buildings, $"Shed {houses + barns + stacks:00}", position, size, "Timber");
            }
        }
    }

    // Outside the ring road: fields, a windmill, a watchtower, a farmstead and trees
    private static void BuildOutskirts(Transform buildings, Transform props, Transform nature)
    {
        // Fields in the corners outside the ring
        string[] crops = { "Field Wheat", "Field Green", "Field Earth" };
        int fields = 0;

        for (int qx = -1; qx <= 1; qx += 2)
        for (int qz = -1; qz <= 1; qz += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                Vector2 size = new Vector2(Range(22f, 34f), Range(18f, 28f));
                Vector3 position = new Vector3(qx * Range(RingHalf + 22f, RingHalf + 70f), 0f, qz * Range(RingHalf + 22f, RingHalf + 70f));

                if (TryReserve(position, new Vector3(size.x, 0f, size.y), 2f))
                    Flat(props, $"Field {++fields:00}", position + new Vector3(0f, 0.005f, 0f), size, crops[random.Next(crops.Length)]);
            }
        }

        // Windmill north-west, watchtower south-east, a farmstead on the east road
        Vector3 mill = new Vector3(-RingHalf - 25f, 0f, RingHalf + 25f);
        Reserve(new Vector2(mill.x, mill.z), new Vector2(12f, 12f));
        Windmill(buildings, props, mill);

        Vector3 tower = new Vector3(RingHalf + 18f, 0f, -RingHalf - 18f);
        Reserve(new Vector2(tower.x, tower.z), new Vector2(8f, 8f));
        Block(buildings, "Watchtower", tower, new Vector3(5f, 18f, 5f), "Timber");

        Vector3 farm = new Vector3(RingHalf + 55f, 0f, 16f);
        Reserve(new Vector2(farm.x, farm.z), new Vector2(40f, 20f));
        Block(buildings, "Farmhouse", farm, new Vector3(9f, 7f, 8f), Wall());
        Block(buildings, "Farm Barn", farm + new Vector3(16f, 0f, 2f), new Vector3(16f, 9f, 11f), "Barn");
        Fence(props, farm + new Vector3(8f, 0f, -9f), 34f, true);

        // Trees: scattered round the outskirts, denser in a wood to the south-west
        int trees = 0;

        for (int attempt = 0; attempt < 500 && trees < 90; attempt++)
        {
            Vector3 position;

            if (attempt % 3 == 0)
                position = new Vector3(Range(-RingHalf - 70f, -RingHalf - 15f), 0f, Range(-RingHalf - 70f, -RingHalf - 10f));
            else
                position = new Vector3(Range(-RingHalf - 90f, RingHalf + 90f), 0f, Range(-RingHalf - 90f, RingHalf + 90f));

            // Keep the town inside the ring clear
            if (Mathf.Abs(position.x) < RingHalf + 6f && Mathf.Abs(position.z) < RingHalf + 6f)
                continue;

            if (TryReserve(position, new Vector3(3f, 0f, 3f), 1f))
                Tree(nature, $"Tree {++trees:00}", position);
        }
    }
    #endregion

    #region Pieces
    private static GameObject Block(Transform parent, string name, Vector3 groundPosition, Vector3 size, string material)
    {
        GameObject block = Primitive(PrimitiveType.Cube, parent, name, material);
        block.transform.localPosition = groundPosition + new Vector3(0f, size.y * 0.5f, 0f);
        block.transform.localScale = size;
        return block;
    }

    // A flat slab for roads, paving and fields, just thick enough not to z-fight the ground
    private static void Flat(Transform parent, string name, Vector3 position, Vector2 size, string material)
    {
        GameObject flat = Primitive(PrimitiveType.Cube, parent, name, material);
        flat.transform.localPosition = position + new Vector3(0f, 0.02f, 0f);
        flat.transform.localScale = new Vector3(size.x, 0.04f, size.y);
    }

    private static void Cylinder(Transform parent, string name, Vector3 groundPosition, float diameter, float height, string material)
    {
        GameObject cylinder = Primitive(PrimitiveType.Cylinder, parent, name, material);
        cylinder.transform.localPosition = groundPosition + new Vector3(0f, height * 0.5f, 0f);
        cylinder.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);    // Unity's cylinder is 2 tall
    }

    private static void Haystack(Transform parent, string name, Vector3 groundPosition)
    {
        GameObject stack = Primitive(PrimitiveType.Cylinder, parent, name, "Hay");
        float diameter = Range(2.2f, 3f);
        stack.transform.localPosition = groundPosition + new Vector3(0f, diameter * 0.5f, 0f);
        stack.transform.localRotation = Quaternion.Euler(0f, Range(0f, 180f), 90f);      // round bale on its side
        stack.transform.localScale = new Vector3(diameter, 0.9f, diameter);
    }

    private static void Tree(Transform parent, string name, Vector3 groundPosition)
    {
        GameObject tree = new GameObject(name);
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = groundPosition;

        float height = Range(5f, 9f);
        float crown = Range(3.5f, 5.5f);

        GameObject trunk = Primitive(PrimitiveType.Cylinder, tree.transform, "Trunk", "Bark");
        trunk.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(0.6f, height * 0.5f, 0.6f);

        GameObject leaves = Primitive(PrimitiveType.Sphere, tree.transform, "Crown", random.Next(3) == 0 ? "Leaves Dark" : "Leaves");
        leaves.transform.localPosition = new Vector3(0f, height, 0f);
        leaves.transform.localScale = new Vector3(crown, crown * Range(0.9f, 1.3f), crown);
    }

    private static void Windmill(Transform buildings, Transform props, Vector3 groundPosition)
    {
        Block(buildings, "Windmill", groundPosition, new Vector3(6f, 14f, 6f), "Whitewash");

        // Sails: two crossed blades on the front, a little off the wall
        Vector3 hub = groundPosition + new Vector3(0f, 11f, -3.6f);

        for (int i = 0; i < 2; i++)
        {
            GameObject blade = Primitive(PrimitiveType.Cube, props, $"Windmill Sail {i + 1}", "Timber");
            blade.transform.localPosition = hub;
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, 45f + i * 90f);
            blade.transform.localScale = new Vector3(1.2f, 16f, 0.2f);
        }
    }

    private static void Fence(Transform parent, Vector3 start, float length, bool alongX)
    {
        GameObject fence = Primitive(PrimitiveType.Cube, parent, "Fence", "Timber");
        Vector3 offset = alongX ? new Vector3(length * 0.5f, 0.6f, 0f) : new Vector3(0f, 0.6f, length * 0.5f);
        fence.transform.localPosition = start + offset;
        fence.transform.localScale = alongX ? new Vector3(length, 1.2f, 0.25f) : new Vector3(0.25f, 1.2f, length);
    }

    private static GameObject Primitive(PrimitiveType type, Transform parent, string name, string material)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.GetComponent<MeshRenderer>().sharedMaterial = GetMaterial(material);
        return primitive;
    }

    private static Transform Group(GameObject root, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(root.transform, false);
        return group.transform;
    }
    #endregion

    #region Space
    private static void Reserve(Vector2 centre, Vector2 size)
    {
        occupied.Add(new Rect(centre - size * 0.5f, size));
    }

    // Takes the footprint (x and z of size) plus a gap if nothing's there yet
    private static bool TryReserve(Vector3 centre, Vector3 size, float gap)
    {
        Vector2 footprint = new Vector2(size.x + gap * 2f, size.z + gap * 2f);
        Rect rect = new Rect(new Vector2(centre.x, centre.z) - footprint * 0.5f, footprint);

        for (int i = 0; i < occupied.Count; i++)
        {
            if (occupied[i].Overlaps(rect))
                return false;
        }

        Reserve(new Vector2(centre.x, centre.z), new Vector2(size.x, size.z));
        return true;
    }
    #endregion

    #region Helpers
    private static float Random01() => (float)random.NextDouble();

    private static float Range(float min, float max) => min + (max - min) * Random01();

    private static float Pick(params float[] values) => values[random.Next(values.Length)];

    private static string Wall() => "Wall " + random.Next(WallColours.Length);

    // One saved URP Lit material per colour name, reused across builds
    private static Material GetMaterial(string key)
    {
        if (materials.TryGetValue(key, out Material cached))
            return cached;

        string path = $"{MaterialFolder}/Blockout {key}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                return null;    // primitive keeps its default material

            EnsureFolder(MaterialFolder);
            material = new Material(shader);
            material.SetColor("_BaseColor", Colour(key));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
        }

        materials.Add(key, material);
        return material;
    }

    private static Color Colour(string key)
    {
        if (key.StartsWith("Wall "))
            return WallColours[int.Parse(key.Substring(5)) % WallColours.Length];

        switch (key)
        {
            case "Road": return new Color(0.28f, 0.28f, 0.3f);
            case "Dirt": return new Color(0.5f, 0.4f, 0.3f);
            case "Paving": return new Color(0.62f, 0.6f, 0.56f);
            case "Stone": return new Color(0.55f, 0.56f, 0.58f);
            case "Whitewash": return new Color(0.92f, 0.9f, 0.86f);
            case "Timber": return new Color(0.45f, 0.32f, 0.22f);
            case "Barn": return new Color(0.6f, 0.2f, 0.16f);
            case "Hay": return new Color(0.86f, 0.74f, 0.4f);
            case "Bark": return new Color(0.35f, 0.25f, 0.18f);
            case "Leaves": return new Color(0.3f, 0.55f, 0.25f);
            case "Leaves Dark": return new Color(0.2f, 0.4f, 0.2f);
            case "Field Wheat": return new Color(0.82f, 0.72f, 0.38f);
            case "Field Green": return new Color(0.45f, 0.62f, 0.3f);
            case "Field Earth": return new Color(0.45f, 0.33f, 0.24f);
            case "Canvas Red": return new Color(0.75f, 0.25f, 0.22f);
            case "Canvas Blue": return new Color(0.25f, 0.4f, 0.7f);
            default: return Color.magenta;
        }
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";

            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
    #endregion
}
