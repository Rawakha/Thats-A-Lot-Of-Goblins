using System;
using System.Collections.Generic;
using UnityEngine;

// An Egyptian pyramid built as stacked courses of sandstone, each a little narrower than the one below, with an optional
// pointed capstone. Built on the level tool foundation (Level_Tool), so every course is an editable stone and the whole
// thing bakes into one mesh. More courses with a smaller step look smooth; few courses look like a step pyramid.
//
// Weathering: Jitter shifts and turns each course slightly so the sides aren't razor straight, Trim Every colours every
// Nth course in the lighter trim colour, and Missing Courses takes the top off for a ruin (the capstone goes with it).
//
// Entrance: one door cut into a face at ground level, a recess with a lintel over it, anywhere around the base. It is
// where goblins can come out of and go into the pyramid: TryGetEntrance gives its position and facing in the world, for
// spawn points and goals later. The door's courses aren't jittered, so its edges stay clean.
//
// Setup: GameObject > Level > Egyptian > Pyramid. White square moves it, orange cubes set the base size, yellow cone sets the
// height, the blue ring turns it. Add Entrance in the inspector makes the door; its handles (white slides it round the
// base, orange cubes set its width, yellow cone its height) show when the pyramid is selected. It starts on the Building
// layer, so goblins path round it; rebake the flow field after moving it. Bake Into One Mesh when finished.
// GC: nothing at runtime, no ticks or subscriptions. Generating allocates working lists and objects in the editor only.
public class Level_EgyptPyramid : Level_Tool
{
    private const int MaxCourses = 80;

    [Header("Size")]
    [Tooltip("Width and depth of the base, metres.")]
    [SerializeField, Min(2f)] private float baseSize = 24f;
    [Tooltip("Height of the whole pyramid, metres, before any missing top courses.")]
    [SerializeField, Min(1f)] private float height = 16f;
    [Tooltip("Width of the top of the last course, metres. 0 comes to a point; more leaves a flat top.")]
    [SerializeField, Min(0f)] private float topSize = 2f;

    [Header("Courses")]
    [Tooltip("How many layers of stone. More looks smoother; fewer looks like steps.")]
    [SerializeField, Range(3, MaxCourses)] private int courses = 16;
    [Tooltip("Take this many courses off the top, for a broken ruin. The capstone goes too.")]
    [SerializeField, Min(0)] private int missingCourses;
    [Tooltip("Colour every Nth course in the lighter trim colour. 0 is off.")]
    [SerializeField, Min(0)] private int trimEvery = 4;

    [Header("Weathering")]
    [Tooltip("How far each course can shift sideways, metres.")]
    [SerializeField, Min(0f)] private float jitterOffset = 0.15f;
    [Tooltip("How far each course can turn, degrees.")]
    [SerializeField, Range(0f, 10f)] private float jitterAngle = 1.5f;

    [Header("Capstone")]
    [Tooltip("Put a pointed capstone on top (only when no courses are missing).")]
    [SerializeField] private bool capstone = true;
    [Tooltip("Height of the capstone, metres.")]
    [SerializeField, Min(0.2f)] private float capstoneHeight = 2f;

    [Header("Entrance")]
    [Tooltip("Cut a door into the pyramid at ground level.")]
    [SerializeField] private bool entrance;
    [Tooltip("Where the door is, in this object's space. The Scene handle keeps it on the base's edge; the face it is nearest is used.")]
    [SerializeField] private Vector3 entrancePosition = new Vector3(0f, 0f, -12f);
    [Tooltip("Width of the door, metres.")]
    [SerializeField, Min(0.5f)] private float entranceWidth = 3f;
    [Tooltip("Height of the door, metres.")]
    [SerializeField, Min(0.5f)] private float entranceHeight = 3.5f;
    [Tooltip("How far the door recess goes into the pyramid, metres.")]
    [SerializeField, Min(0.5f)] private float entranceDepth = 4f;

    [Header("Rotation")]
    [Tooltip("Spin around the vertical axis, degrees.")]
    [SerializeField, Range(-180f, 180f)] private float yaw;

    public float BaseSize => baseSize;
    public float Height => height;
    public float Yaw => yaw;
    public bool HasEntrance => entrance;
    public float EntranceWidth => entranceWidth;
    public float EntranceHeight => entranceHeight;
    public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f);

    // Which face the door is on (as a turn from the pyramid's own +Z face) and how far along that face it sits from the
    // face's middle, from where the door was placed. Also where the door's base centre is and which way it looks, in this
    // object's space.
    private void ResolveEntrance(out Quaternion face, out float along)
    {
        Vector3 local = Quaternion.Inverse(Rotation) * entrancePosition;
        float angle = Mathf.Abs(local.z) >= Mathf.Abs(local.x) ? (local.z >= 0f ? 0f : 180f) : (local.x > 0f ? 90f : 270f);
        face = Quaternion.Euler(0f, angle, 0f);
        float reach = Mathf.Max(0f, baseSize * 0.5f - entranceWidth * 0.5f - 0.5f);
        along = Mathf.Clamp(Vector3.Dot(local, face * Vector3.right), -reach, reach);
    }

    // The door's base centre on the face and which way it looks (out of the pyramid), in this object's space
    public void EntranceFrame(out Vector3 point, out Quaternion look)
    {
        ResolveEntrance(out Quaternion face, out float along);
        look = Rotation * face;
        point = look * new Vector3(along, 0f, baseSize * 0.5f);
    }

    // The nearest point on the edge of the base to a point (this object's space): where the door ends up when dragged there
    public Vector3 PlaceOnFace(Vector3 local)
    {
        Vector3 inPyramid = Quaternion.Inverse(Rotation) * new Vector3(local.x, 0f, local.z);
        float half = baseSize * 0.5f;

        if (Mathf.Abs(inPyramid.z) >= Mathf.Abs(inPyramid.x))
            inPyramid = new Vector3(Mathf.Clamp(inPyramid.x, -half, half), 0f, Mathf.Sign(inPyramid.z == 0f ? 1f : inPyramid.z) * half);
        else
            inPyramid = new Vector3(Mathf.Sign(inPyramid.x) * half, 0f, Mathf.Clamp(inPyramid.z, -half, half));

        return Rotation * inPyramid;
    }

    // The door in the world: its base centre, and the direction it faces. False if there's no door.
    public bool TryGetEntrance(out Vector3 position, out Vector3 outward)
    {
        position = transform.position;
        outward = transform.forward;

        if (!entrance)
            return false;

        EntranceFrame(out Vector3 point, out Quaternion look);
        position = transform.TransformPoint(point);
        outward = transform.TransformDirection(look * Vector3.forward);
        return true;
    }

#if UNITY_EDITOR
    // Adds the door at a point (this object's space), clamped onto the base's edge
    public void AddEntrance(Vector3 local)
    {
        UnityEditor.Undo.RecordObject(this, "Add Pyramid Entrance");
        entrance = true;
        entrancePosition = PlaceOnFace(local);
        RequestRefresh();
    }

    public void MoveEntrance(Vector3 local)
    {
        UnityEditor.Undo.RecordObject(this, "Move Pyramid Entrance");
        entrancePosition = PlaceOnFace(local);
        RequestRefresh();
    }
#endif

    protected override void BuildPieces(List<Level_ToolPiece> pieces)
    {
        if (topSize >= baseSize)
            throw new InvalidOperationException("Top Size must be smaller than Base Size.");

        if (missingCourses >= courses)
            throw new InvalidOperationException("Missing Courses must be fewer than Courses, or nothing is left.");

        Quaternion rotation = Rotation;
        float courseHeight = height / courses;
        Unity.Mathematics.Random random = Unity.Mathematics.Random.CreateFromIndex((uint)Mathf.Abs(seed) + 1u);
        int built = courses - missingCourses;

        Quaternion face = Quaternion.identity;
        float along = 0f;
        float doorHeight = 0f;
        float doorWidth = 0f;
        float doorInner = 0f;

        if (entrance)
        {
            ResolveEntrance(out face, out along);
            doorHeight = Mathf.Min(entranceHeight, height * 0.8f);
            doorWidth = Mathf.Min(entranceWidth, baseSize * 0.6f);
            doorInner = baseSize * 0.5f - Mathf.Min(entranceDepth, baseSize * 0.8f);
        }

        for (int i = 0; i < built; i++)
        {
            // Width at the middle of the course, so a smooth pyramid's slope runs through the steps
            float width = Mathf.Lerp(baseSize, topSize, (i + 0.5f) / courses);
            Vector3 shift = new Vector3(random.NextFloat(-jitterOffset, jitterOffset), 0f, random.NextFloat(-jitterOffset, jitterOffset));
            Quaternion turn = Quaternion.Euler(0f, random.NextFloat(-jitterAngle, jitterAngle), 0f);
            string band = trimEvery > 0 && i % trimEvery == trimEvery - 1 ? "Trim" : "Main";
            float bottom = i * courseHeight;
            float slab = courseHeight + 0.02f; // slight overlap with the course below, so no gap shows

            // Courses the door cuts into are built square to the door, so its edges stay clean
            bool cut = entrance && bottom < doorHeight && width * 0.5f > doorInner;

            if (!cut)
            {
                pieces.Add(new Level_ToolPiece($"Course {i}", rotation * (shift + Vector3.up * bottom), rotation * turn,
                    new Vector3(width, slab, width), band, "Pyramid Course"));
                continue;
            }

            // The slab minus the door's box: a block to each side, one behind, and a lintel above the door if the door ends in
            // this course. All in the door's own frame (x across, z out of the face), then turned into place.
            float half = width * 0.5f;
            float x0 = Mathf.Max(along - doorWidth * 0.5f, -half);
            float x1 = Mathf.Min(along + doorWidth * 0.5f, half);
            Quaternion turned = rotation * face;

            if (x1 - x0 < 0.05f)
            {
                pieces.Add(new Level_ToolPiece($"Course {i}", rotation * Vector3.up * bottom, rotation, new Vector3(width, slab, width), band, "Pyramid Course"));
                continue;
            }

            AddBlock(pieces, $"Course {i} Left", turned, face, -half, x0, -half, half, bottom, slab, band);
            AddBlock(pieces, $"Course {i} Right", turned, face, x1, half, -half, half, bottom, slab, band);
            AddBlock(pieces, $"Course {i} Back", turned, face, x0, x1, -half, doorInner, bottom, slab, band);

            if (bottom + courseHeight > doorHeight + 0.05f)
                AddBlock(pieces, $"Course {i} Lintel", turned, face, x0, x1, doorInner, half, doorHeight, bottom + courseHeight - doorHeight + 0.02f, "Trim");
        }

        if (capstone && missingCourses == 0)
        {
            float capWidth = Mathf.Max(topSize, 0.5f);
            pieces.Add(new Level_ToolPiece("Capstone", rotation * Vector3.up * height, rotation,
                new Vector3(capWidth, capstoneHeight, capWidth), "Accent", "Pyramid Capstone", Level_BlockMesh.Shape.PointedCap));
        }
    }

    // A block spanning x0..x1 across and z0..z1 out of the door's face, standing on y, in the door's frame. Skipped if it's
    // too small to see.
    private void AddBlock(List<Level_ToolPiece> pieces, string id, Quaternion turned, Quaternion face, float x0, float x1, float z0,
        float z1, float y, float blockHeight, string band)
    {
        if (x1 - x0 < 0.05f || z1 - z0 < 0.05f)
            return;

        Vector3 centre = new Vector3((x0 + x1) * 0.5f, y, (z0 + z1) * 0.5f);
        pieces.Add(new Level_ToolPiece(id, Rotation * (face * centre), turned, new Vector3(x1 - x0, blockHeight, z1 - z0), band, "Pyramid Course"));
    }
}
