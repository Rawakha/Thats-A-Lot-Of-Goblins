using System;
using System.Collections.Generic;
using UnityEngine;

public enum Level_EgyptOpeningKind { Door, Window }

// One storey of an Egyptian house: a box of walls with a flat roof. Floor 0 is the base, centred on the house. Floors above
// stand on the roof below and can be smaller and shifted (Offset), leaving a roof terrace.
[Serializable]
public class Level_EgyptHouseFloor
{
    [Tooltip("Width of this storey, metres (along the house's X axis).")]
    [Min(2f)] public float width = 6f;
    [Tooltip("Depth of this storey, metres (along the house's Z axis).")]
    [Min(2f)] public float depth = 5f;
    [Tooltip("Height of the walls, metres.")]
    [Min(2f)] public float height = 3f;
    [Tooltip("Where this storey's middle is, from the middle of the base. Ignored for floor 0. Keep it inside the floor below.")]
    public Vector2 offset;
}

// A door or window cut into one wall of one storey, with a dark frame and a wooden panel in it.
[Serializable]
public class Level_EgyptHouseOpening
{
    public Level_EgyptOpeningKind kind;
    [Tooltip("Which storey it is on. 0 is the ground floor.")]
    [Min(0)] public int floor;
    [Tooltip("Where it is, in the house's space. The Scene handle keeps it on the nearest wall of its storey.")]
    public Vector3 position;
    [Tooltip("Width of the opening, metres.")]
    [Min(0.4f)] public float width = 1.3f;
    [Tooltip("Height of the bottom edge above the storey's floor, metres. Doors use 0.")]
    [Min(0f)] public float bottom;
    [Tooltip("Height of the top edge above the storey's floor, metres.")]
    [Min(0.5f)] public float top = 2.3f;
}

// A modular Egyptian house: stacked storeys with flat roofs, parapets and a dark ledge between floors, with doors and
// windows you place on the walls and an optional outside stair up to the first roof. Everything is editable stones on the
// level tool foundation (Level_Tool), baked to one mesh. Rotate the object with Unity's rotate tool to face it any way.
//
// Setup: GameObject > Level > Egyptian > House. White square moves it. Per storey: orange cubes set width, pink cubes depth,
// yellow cone height, and (upper floors) a white square shifts the floor on the roof below. Add Door / Add Window in the
// inspector place one on the wall nearest where the Scene view looks; its white square slides it along the wall, orange
// cubes set its width, yellow cones its edges, Ctrl/Cmd shows a red dot to delete. Add Floor stacks another storey, and
// Stairs adds an outside stair whose white square slides it along the ground-floor walls.
// Doors and windows are filled with wooden panels, so the house is solid and goblins path round it (Building layer).
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor only.
public class Level_EgyptHouse : Level_Tool
{
    private const int MaxFloors = 6;

    [Header("Floors")]
    [SerializeField] private List<Level_EgyptHouseFloor> floors = new List<Level_EgyptHouseFloor>
    {
        new Level_EgyptHouseFloor { width = 6f, depth = 5f, height = 3f },
        new Level_EgyptHouseFloor { width = 3f, depth = 3f, height = 2.8f, offset = new Vector2(-1.5f, -1f) }
    };
    [Tooltip("Thickness of the walls, metres.")]
    [SerializeField, Min(0.2f)] private float wallThickness = 0.4f;

    [Header("Openings")]
    [SerializeField] private List<Level_EgyptHouseOpening> openings = new List<Level_EgyptHouseOpening>();
    [Tooltip("Width of the dark frame round every door and window, metres.")]
    [SerializeField, Min(0.02f)] private float frameWidth = 0.12f;

    [Header("Roofs")]
    [Tooltip("Thickness of the dark ledge between storeys and under the roof, metres.")]
    [SerializeField, Min(0.05f)] private float ledgeThickness = 0.25f;
    [Tooltip("How far the ledge sticks out past the walls, metres.")]
    [SerializeField, Range(0f, 0.5f)] private float ledgeOverhang = 0.15f;
    [Tooltip("Thickness of the roof surface, metres.")]
    [SerializeField, Min(0.02f)] private float roofThickness = 0.1f;
    [Tooltip("Height of the low wall round each roof, metres. 0 removes it.")]
    [SerializeField, Min(0f)] private float parapetHeight = 0.5f;
    [Tooltip("Thickness of the parapet, metres.")]
    [SerializeField, Min(0.1f)] private float parapetThickness = 0.3f;

    [Header("Stairs")]
    [Tooltip("An outside stair up the ground floor's wall to its roof.")]
    [SerializeField] private bool stairs;
    [Tooltip("Where the foot of the stair is, in the house's space. The Scene handle keeps it on the nearest ground-floor wall.")]
    [SerializeField] private Vector3 stairsPosition = new Vector3(2f, 0f, 2.5f);
    [Tooltip("Climb the other way along the wall.")]
    [SerializeField] private bool stairsReverse;
    [Tooltip("Width of the stair out from the wall, metres.")]
    [SerializeField, Min(0.4f)] private float stairsWidth = 1.2f;
    [Tooltip("Height of each step, metres.")]
    [SerializeField, Min(0.1f)] private float stepHeight = 0.4f;
    [Tooltip("Length of each step along the wall, metres.")]
    [SerializeField, Min(0.2f)] private float stepDepth = 0.5f;

    public List<Level_EgyptHouseFloor> Floors => floors;
    public List<Level_EgyptHouseOpening> Openings => openings;
    public bool HasStairs => stairs;

    // A storey's box: its middle, size, where its walls start and the top of its roof (where the next storey starts)
    private struct Box
    {
        public float cx, cz, width, depth, bottom, height, roofTop;
    }

    private Box[] Boxes()
    {
        Box[] boxes = new Box[floors.Count];
        float y = 0f;

        for (int i = 0; i < boxes.Length; i++)
        {
            Level_EgyptHouseFloor floor = floors[i];
            float top = y + floor.height + ledgeThickness + roofThickness;
            boxes[i] = new Box
            {
                cx = i == 0 ? 0f : floor.offset.x,
                cz = i == 0 ? 0f : floor.offset.y,
                width = floor.width,
                depth = floor.depth,
                bottom = y,
                height = floor.height,
                roofTop = top
            };
            y = top;
        }

        return boxes;
    }

    // One of a storey's four walls (0 front +Z, 1 right +X, 2 back -Z, 3 left -X): its middle on the ground, which way it faces
    // (look's forward points out) and how long it is. Side walls are shorter, so they fit between the front and back.
    private static void WallFrame(Box box, int wall, float thickness, out Vector3 centre, out Quaternion look, out float length)
    {
        switch (wall)
        {
            case 0:
                centre = new Vector3(box.cx, 0f, box.cz + box.depth * 0.5f - thickness * 0.5f);
                look = Quaternion.LookRotation(Vector3.forward);
                length = box.width;
                break;
            case 1:
                centre = new Vector3(box.cx + box.width * 0.5f - thickness * 0.5f, 0f, box.cz);
                look = Quaternion.LookRotation(Vector3.right);
                length = box.depth - 2f * thickness;
                break;
            case 2:
                centre = new Vector3(box.cx, 0f, box.cz - box.depth * 0.5f + thickness * 0.5f);
                look = Quaternion.LookRotation(Vector3.back);
                length = box.width;
                break;
            default:
                centre = new Vector3(box.cx - box.width * 0.5f + thickness * 0.5f, 0f, box.cz);
                look = Quaternion.LookRotation(Vector3.left);
                length = box.depth - 2f * thickness;
                break;
        }
    }

    // The wall of a storey nearest a point, and how far along it (from its middle, along look's right)
    private int FindWall(Box box, Vector3 point, out float along)
    {
        int best = 0;
        float bestDistance = float.MaxValue;
        along = 0f;

        for (int wall = 0; wall < 4; wall++)
        {
            WallFrame(box, wall, wallThickness, out Vector3 centre, out Quaternion look, out float length);
            Vector3 tangent = look * Vector3.right;
            float reach = Mathf.Clamp(Vector3.Dot(new Vector3(point.x, 0f, point.z) - centre, tangent), -length * 0.5f, length * 0.5f);
            float distance = (new Vector3(point.x, 0f, point.z) - (centre + tangent * reach)).sqrMagnitude;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = wall;
                along = reach;
            }
        }

        return best;
    }

    // Where an opening is on the outside of its wall, and which way it looks, in the house's space
    public bool OpeningFrame(int index, out Vector3 point, out Quaternion look)
    {
        point = default;
        look = Quaternion.identity;

        if (index < 0 || index >= openings.Count || openings[index] == null || floors.Count == 0)
            return false;

        Level_EgyptHouseOpening opening = openings[index];
        Box[] boxes = Boxes();
        Box box = boxes[Mathf.Clamp(opening.floor, 0, boxes.Length - 1)];
        int wall = FindWall(box, opening.position, out float along);
        WallFrame(box, wall, wallThickness, out Vector3 centre, out look, out float length);
        along = Mathf.Clamp(along, -length * 0.5f + opening.width * 0.5f + 0.3f, length * 0.5f - opening.width * 0.5f - 0.3f);
        point = centre + look * Vector3.right * along + look * Vector3.forward * (wallThickness * 0.5f) + Vector3.up * box.bottom;
        return true;
    }

    // Where the stair's foot is on the ground floor's wall, and which way the wall faces
    public bool StairsFrame(out Vector3 point, out Quaternion look)
    {
        point = default;
        look = Quaternion.identity;

        if (!stairs || floors.Count == 0)
            return false;

        Box box = Boxes()[0];
        int wall = FindWall(box, stairsPosition, out float along);
        WallFrame(box, wall, wallThickness, out Vector3 centre, out look, out float length);
        along = Mathf.Clamp(along, -length * 0.5f + 0.5f, length * 0.5f - 0.5f);
        point = centre + look * Vector3.right * along + look * Vector3.forward * (wallThickness * 0.5f);
        return true;
    }

#if UNITY_EDITOR
    // Moving takes a point and the wall nearest it is used when building, so the opening always sits on a wall
    public void MoveOpening(int index, Vector3 local)
    {
        if (index < 0 || index >= openings.Count)
            return;

        UnityEditor.Undo.RecordObject(this, "Move Opening");
        openings[index].position = new Vector3(local.x, 0f, local.z);
        RequestRefresh();
    }

    public void MoveStairs(Vector3 local)
    {
        UnityEditor.Undo.RecordObject(this, "Move Stairs");
        stairsPosition = new Vector3(local.x, 0f, local.z);
        RequestRefresh();
    }

    public void AddOpening(Level_EgyptOpeningKind kind, Vector3 local)
    {
        UnityEditor.Undo.RecordObject(this, "Add Opening");
        bool door = kind == Level_EgyptOpeningKind.Door;
        openings.Add(new Level_EgyptHouseOpening
        {
            kind = kind,
            position = new Vector3(local.x, 0f, local.z),
            width = door ? 1.3f : 1f,
            bottom = door ? 0f : 1f,
            top = door ? 2.3f : 2.1f
        });
        RequestRefresh();
    }

    public void AddFloor()
    {
        if (floors.Count >= MaxFloors)
            return;

        UnityEditor.Undo.RecordObject(this, "Add Floor");
        Level_EgyptHouseFloor top = floors[floors.Count - 1];
        floors.Add(new Level_EgyptHouseFloor
        {
            width = Mathf.Max(2f, top.width - 1.5f),
            depth = Mathf.Max(2f, top.depth - 1.5f),
            height = top.height,
            offset = floors.Count == 1 ? Vector2.zero : top.offset
        });
        RequestRefresh();
    }

    // A new random house: one to three storeys (upper ones smaller and shifted onto the roof below), a door, windows on each
    // floor, sometimes a door onto a terrace, and sometimes an outside stair. A different one every press; a single Undo step.
    // The wall thickness, roofs and frames are left as they are.
    public void Randomise()
    {
        UnityEditor.Undo.RecordObject(this, "Randomise House");
        System.Random rng = new System.Random(Guid.NewGuid().GetHashCode());
        float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        float Half(float value) => Mathf.Round(value * 2f) / 2f;
        float Tenth(float value) => Mathf.Round(value * 10f) / 10f;

        floors.Clear();
        openings.Clear();
        double chance = rng.NextDouble();
        int count = chance < 0.4 ? 1 : chance < 0.8 ? 2 : 3;
        Vector2 previousCentre = Vector2.zero;

        for (int i = 0; i < count; i++)
        {
            Level_EgyptHouseFloor floor = new Level_EgyptHouseFloor();

            if (i == 0)
            {
                floor.width = Half(Range(5f, 9f));
                floor.depth = Half(Range(4f, 7f));
                floor.height = Tenth(Range(2.8f, 3.4f));
            }
            else
            {
                Level_EgyptHouseFloor below = floors[i - 1];
                floor.width = Mathf.Max(2.5f, Half(below.width * Range(0.45f, 0.8f)));
                floor.depth = Mathf.Max(2.5f, Half(below.depth * Range(0.45f, 0.8f)));
                floor.height = Tenth(Range(2.6f, 3.2f));

                // Somewhere on the roof below, without hanging off it
                float room = Mathf.Max(0f, (below.width - floor.width) * 0.5f);
                float roomDeep = Mathf.Max(0f, (below.depth - floor.depth) * 0.5f);
                floor.offset = previousCentre + new Vector2(Mathf.Clamp(Half(Range(-room, room)), -room, room),
                    Mathf.Clamp(Half(Range(-roomDeep, roomDeep)), -roomDeep, roomDeep));
                previousCentre = floor.offset;
            }

            floors.Add(floor);
        }

        // Doors and windows: each takes a spot on a wall that doesn't overlap one already there
        Box[] boxes = Boxes();
        List<Vector3> used = new List<Vector3>(); // floor, wall, along
        List<float> usedWidth = new List<float>();

        bool Place(int floor, int wall, float width, out float along)
        {
            WallFrame(boxes[floor], wall, wallThickness, out _, out _, out float length);
            float room = length * 0.5f - width * 0.5f - 0.5f;
            along = 0f;

            if (room < 0f)
                return false;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                along = Half(Range(-room, room));
                bool clear = true;

                for (int u = 0; u < used.Count && clear; u++)
                    clear = (int)used[u].x != floor || (int)used[u].y != wall || Mathf.Abs(used[u].z - along) >= (usedWidth[u] + width) * 0.5f + 0.5f;

                if (clear)
                {
                    used.Add(new Vector3(floor, wall, along));
                    usedWidth.Add(width);
                    return true;
                }
            }

            return false;
        }

        Vector3 PointOn(int floor, int wall, float along)
        {
            WallFrame(boxes[floor], wall, wallThickness, out Vector3 centre, out Quaternion look, out _);
            return centre + look * Vector3.right * along;
        }

        // The front door, usually on the front wall
        int doorWall = rng.Next(3) == 0 ? rng.Next(4) : 0;
        float doorWidth = Tenth(Range(1.2f, 1.5f));

        if (Place(0, doorWall, doorWidth, out float doorAlong))
            openings.Add(new Level_EgyptHouseOpening
            {
                kind = Level_EgyptOpeningKind.Door, floor = 0, position = PointOn(0, doorWall, doorAlong), width = doorWidth,
                bottom = 0f, top = Mathf.Min(Tenth(Range(2.2f, 2.5f)), floors[0].height - 0.3f)
            });

        for (int f = 0; f < floors.Count; f++)
        {
            // A door onto the terrace from the top floor, now and then
            if (f > 0 && f == floors.Count - 1 && rng.NextDouble() < 0.4)
            {
                int wall = rng.Next(4);
                float width = Tenth(Range(1.1f, 1.3f));

                if (Place(f, wall, width, out float along))
                    openings.Add(new Level_EgyptHouseOpening
                    {
                        kind = Level_EgyptOpeningKind.Door, floor = f, position = PointOn(f, wall, along), width = width,
                        bottom = 0f, top = Mathf.Min(2.2f, floors[f].height - 0.3f)
                    });
            }

            int windows = f == 0 ? rng.Next(1, 4) : rng.Next(1, 3);

            for (int w = 0; w < windows; w++)
            {
                int wall = rng.Next(4);
                float width = Tenth(Range(0.8f, 1.2f));

                if (!Place(f, wall, width, out float along))
                    continue;

                float bottom = Tenth(Range(0.9f, 1.2f));
                openings.Add(new Level_EgyptHouseOpening
                {
                    kind = Level_EgyptOpeningKind.Window, floor = f, position = PointOn(f, wall, along), width = width,
                    bottom = bottom, top = Mathf.Min(bottom + Tenth(Range(0.9f, 1.2f)), floors[f].height - 0.3f)
                });
            }
        }

        // An outside stair on a different wall from the door, on taller houses and now and then on a one-storey one
        stairs = count > 1 || rng.NextDouble() < 0.4;

        if (stairs)
        {
            int wall = (doorWall + rng.Next(1, 4)) % 4;
            WallFrame(boxes[0], wall, wallThickness, out _, out _, out float length);
            float room = Mathf.Max(0f, length * 0.5f - 1.5f);
            stairsPosition = PointOn(0, wall, Half(Range(-room, room)));
            stairsReverse = rng.Next(2) == 0;
        }

        seed = rng.Next(1, 100000);
        UnityEditor.EditorUtility.SetDirty(this);
        RequestRefresh();
    }

    // Takes a storey out. Its doors and windows go with it, and those above come down a floor. The last floor can't go.
    public void RemoveFloor(int index)
    {
        if (floors.Count <= 1 || index < 0 || index >= floors.Count)
            return;

        UnityEditor.Undo.RecordObject(this, "Delete Floor");
        floors.RemoveAt(index);

        for (int i = openings.Count - 1; i >= 0; i--)
        {
            if (openings[i] == null)
                continue;

            if (openings[i].floor == index)
                openings.RemoveAt(i);
            else if (openings[i].floor > index)
                openings[i].floor--;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        RequestRefresh();
    }

    public void AddStairs(Vector3 local)
    {
        UnityEditor.Undo.RecordObject(this, "Add Stairs");
        stairs = true;
        stairsPosition = new Vector3(local.x, 0f, local.z);
        RequestRefresh();
    }
#endif

    // A box standing on y0, spanning x0..x1 along a wall's tangent, y0..y1 up, and z (centre zc, thickness) out of its face,
    // in the wall's frame at origin. Skipped if too small to see.
    private static void AddBox(List<Level_ToolPiece> pieces, string id, Quaternion look, Vector3 origin, float x0, float x1, float y0,
        float y1, float zc, float thickness, string band, string label)
    {
        if (x1 - x0 < 0.03f || y1 - y0 < 0.03f)
            return;

        pieces.Add(new Level_ToolPiece(id, origin + look * new Vector3((x0 + x1) * 0.5f, y0, zc), look,
            new Vector3(x1 - x0, y1 - y0, thickness), band, label));
    }

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        if (floors == null || floors.Count == 0 || floors.Count > MaxFloors || floors.Contains(null))
            throw new InvalidOperationException($"A house needs 1 to {MaxFloors} floors.");

        Box[] boxes = Boxes();

        for (int f = 0; f < boxes.Length; f++)
        {
            Box box = boxes[f];

            if (box.width < wallThickness * 4f || box.depth < wallThickness * 4f)
                throw new InvalidOperationException($"Floor {f} is too small for its wall thickness.");

            for (int wall = 0; wall < 4; wall++)
                BuildWall(pieces, box, f, wall);

            // The dark ledge, the roof surface, and a low wall round the edge
            float ledgeY = box.bottom + box.height;
            Vector3 middle = new Vector3(box.cx, 0f, box.cz);
            pieces.Add(new Level_ToolPiece($"F{f}/Ledge", middle + Vector3.up * ledgeY, Quaternion.identity,
                new Vector3(box.width + ledgeOverhang * 2f, ledgeThickness, box.depth + ledgeOverhang * 2f), "Frame", "House Ledge"));
            pieces.Add(new Level_ToolPiece($"F{f}/Roof", middle + Vector3.up * (ledgeY + ledgeThickness), Quaternion.identity,
                new Vector3(box.width, roofThickness, box.depth), "Trim", "House Roof"));

            if (parapetHeight > 0.05f)
            {
                for (int wall = 0; wall < 4; wall++)
                {
                    WallFrame(box, wall, parapetThickness, out Vector3 centre, out Quaternion look, out float length);
                    AddBox(pieces, $"F{f}/Parapet{wall}", look, centre + Vector3.up * box.roofTop, -length * 0.5f, length * 0.5f, 0f,
                        parapetHeight, 0f, parapetThickness, "Main", "House Parapet");
                }
            }
        }

        if (stairs)
            BuildStairs(pieces, boxes[0]);
    }

    // One wall: blocks between the openings, blocks above and below each, and the opening's frame and panel
    private void BuildWall(List<Level_ToolPiece> pieces, Box box, int floor, int wall)
    {
        WallFrame(box, wall, wallThickness, out Vector3 centre, out Quaternion look, out float length);
        Vector3 origin = centre + Vector3.up * box.bottom;
        float half = length * 0.5f;

        // This wall's openings, left to right
        List<int> mine = new List<int>();

        for (int i = 0; i < openings.Count; i++)
        {
            Level_EgyptHouseOpening opening = openings[i];

            if (opening == null || Mathf.Clamp(opening.floor, 0, floors.Count - 1) != floor)
                continue;

            if (FindWall(box, opening.position, out _) == wall)
                mine.Add(i);
        }

        mine.Sort((a, b) =>
        {
            FindWall(box, openings[a].position, out float x);
            FindWall(box, openings[b].position, out float y);
            return x.CompareTo(y);
        });

        float cursor = -half;
        int block = 0;

        foreach (int i in mine)
        {
            Level_EgyptHouseOpening opening = openings[i];
            FindWall(box, opening.position, out float along);
            float width = Mathf.Min(opening.width, length - 0.6f);
            along = Mathf.Clamp(along, -half + width * 0.5f + 0.3f, half - width * 0.5f - 0.3f);
            float x0 = along - width * 0.5f;
            float x1 = along + width * 0.5f;
            float top = Mathf.Min(opening.top, box.height - 0.3f);
            float bottom = Mathf.Clamp(opening.bottom, 0f, top - 0.4f);

            if (x0 < cursor + 0.05f)
                continue; // overlaps the one before

            string id = $"F{floor}/W{wall}";
            AddBox(pieces, $"{id}/{block++}", look, origin, cursor, x0, 0f, box.height, 0f, wallThickness, "Main", "House Wall");
            AddBox(pieces, $"{id}/{block++}", look, origin, x0, x1, 0f, bottom, 0f, wallThickness, "Main", "House Wall");
            AddBox(pieces, $"{id}/{block++}", look, origin, x0, x1, top, box.height, 0f, wallThickness, "Main", "House Wall");

            // Dark frame (a little proud of the wall), and the wooden panel set back in the opening
            string o = $"F{floor}/O{i}";
            float frameDepth = wallThickness + 0.1f;
            AddBox(pieces, $"{o}/Left", look, origin, x0 - frameWidth, x0, bottom, top + frameWidth, 0f, frameDepth, "Frame", "Frame");
            AddBox(pieces, $"{o}/Right", look, origin, x1, x1 + frameWidth, bottom, top + frameWidth, 0f, frameDepth, "Frame", "Frame");
            AddBox(pieces, $"{o}/Top", look, origin, x0 - frameWidth, x1 + frameWidth, top, top + frameWidth, 0f, frameDepth, "Frame", "Frame");

            if (bottom > 0.05f)
                AddBox(pieces, $"{o}/Sill", look, origin, x0 - frameWidth, x1 + frameWidth, bottom - frameWidth, bottom, 0f, frameDepth, "Frame", "Frame");

            AddBox(pieces, $"{o}/Panel", look, origin, x0, x1, bottom, top, 0f, 0.12f, "Wood", opening.kind == Level_EgyptOpeningKind.Door ? "Door" : "Window");
            cursor = x1;
        }

        AddBox(pieces, $"F{floor}/W{wall}/{block}", look, origin, cursor, half, 0f, box.height, 0f, wallThickness, "Main", "House Wall");
    }

    // Solid steps climbing the outside of a ground-floor wall to its roof
    private void BuildStairs(List<Level_ToolPiece> pieces, Box box)
    {
        int wall = FindWall(box, stairsPosition, out float along);
        WallFrame(box, wall, wallThickness, out Vector3 centre, out Quaternion look, out float length);
        along = Mathf.Clamp(along, -length * 0.5f + 0.5f, length * 0.5f - 0.5f);
        float direction = stairsReverse ? -1f : 1f;
        int steps = Mathf.Max(2, Mathf.CeilToInt(box.roofTop / stepHeight));

        for (int k = 0; k < steps; k++)
        {
            // Step k is as tall as the climb so far, so the stair is solid down to the ground
            float x = along + direction * (k + 0.5f) * stepDepth;
            AddBox(pieces, $"Stair {k}", look, centre, x - stepDepth * 0.5f, x + stepDepth * 0.5f, 0f, box.roofTop * (k + 1) / steps,
                wallThickness * 0.5f + stairsWidth * 0.5f, stairsWidth, "Main", "House Stair");
        }
    }
}
