using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;
#endif

// Authors either two ordinary quad GameObjects or one central card. Add to an empty "Eyes" object under the goblin (beside/under Head), then press
// Create / Update Eye Cards. The inspector sliders write real child transforms, in this object's local space.
// Local +Z points out of the face; rotate/move the Eyes parent to fit the model. Each card keeps full 0-1 UVs.
// Generated children are driven by these sliders: make placement changes here, rather than on the children.
// They remain normal MeshFilter/MeshRenderer objects for prefab authoring and later render-group extraction.
// Horde_RenderGroup combines the actual card meshes/transforms once per type, then carries the result on Head.
// The authored objects stay separate. There is no per-goblin or per-frame component work.
// GC: editor creation allocates card GameObjects/components plus Unity Undo/inspector data; mesh and material
// are shared Unity assets, never cloned. Baking allocates one combined mesh and a two-element combine array per
// pair per render group, a combine array and centre list, disposed by that group. No event or UpdateManager subscriptions.
[DisallowMultipleComponent]
[AddComponentMenu("Goblins/Horde/Eye Cards")]
public class Horde_EyeCards : MonoBehaviour
{
    [Header("Layout")]
    [Tooltip("One central card instead of the pair. Use a Variation Group to choose between layouts on spawn.")]
    [SerializeField] private bool singleEye;

    [Header("Central Eye")]
    [Tooltip("Central card width in local metres.")]
    [SerializeField, Range(0.01f, 2f)] private float centreWidth = 0.35f;
    [Tooltip("Central card height in local metres.")]
    [SerializeField, Range(0.01f, 2f)] private float centreHeight = 0.35f;
    [Tooltip("Central eye sideways offset in local metres.")]
    [SerializeField, Range(-1f, 1f)] private float centreX;
    [Tooltip("Central eye vertical offset in local metres.")]
    [SerializeField, Range(-1f, 1f)] private float centreY;
    [Tooltip("Central eye forward offset in local metres; keep it clear of the face.")]
    [SerializeField, Range(-1f, 1f)] private float centreForward = 0.411f;
    [Tooltip("Central card tilt around local X, degrees.")]
    [SerializeField, Range(-90f, 90f)] private float centrePitch;
    [Tooltip("Central card turn around local Y, degrees.")]
    [SerializeField, Range(-180f, 180f)] private float centreYaw;
    [Tooltip("Central card roll around local Z, degrees.")]
    [SerializeField, Range(-180f, 180f)] private float centreRoll;
    [SerializeField, HideInInspector] private Transform centreCard;

    [Header("Placement")]
    [Tooltip("Distance between the centres of the two eyes, along local X, metres.")]
    [SerializeField, Range(0f, 2f)] private float spacing = 0.3f;
    [Tooltip("Both eye centres above this object's origin, along local Y, metres.")]
    [SerializeField, Range(-1f, 1f)] private float height;
    [Tooltip("Both eye centres forward from this object's origin, along local +Z, metres. Keep clear of the face.")]
    [SerializeField, Range(-1f, 1f)] private float forward = 0.25f;

    [Header("Size")]
    [Tooltip("Width of each card, metres, before its individual size multiplier.")]
    [SerializeField, Range(0.01f, 2f)] private float width = 0.2f;
    [Tooltip("Height of each card, metres, before its individual size multiplier.")]
    [SerializeField, Range(0.01f, 2f)] private float eyeHeight = 0.25f;
    [Tooltip("Size multiplier for Eye_Left (the card on local -X).")]
    [SerializeField, Range(0.25f, 2f)] private float leftSize = 1f;
    [Tooltip("Size multiplier for Eye_Right (the card on local +X).")]
    [SerializeField, Range(0.25f, 2f)] private float rightSize = 1f;

    [Header("Rotation")]
    [Tooltip("Tilt both cards up/down around local X, degrees.")]
    [SerializeField, Range(-90f, 90f)] private float pitch;
    [Tooltip("Turn the eyes away from each other to fit a rounded face; mirrored around local Y, degrees.")]
    [SerializeField, Range(-90f, 90f)] private float splay;
    [Tooltip("Tilt the eyes in opposite directions around local Z, degrees.")]
    [SerializeField, Range(-90f, 90f)] private float roll;

    [Header("Preview")]
    [Tooltip("Shared Goblins/Eyes material for both MeshRenderers. Empty uses the project's Goblin_Eyes material.")]
    [SerializeField] private Material material;

    [SerializeField, HideInInspector] private Transform leftCard;
    [SerializeField, HideInInspector] private Transform rightCard;

    public bool OwnsRenderer(MeshRenderer renderer)
        => renderer.transform == leftCard || renderer.transform == rightCard || renderer.transform == centreCard;

    // Reads authored children, including any direct transform edits. Never re-runs the placement sliders at bake.
    public bool TryBake(out Mesh combined, out Material shared)
    {
        combined = null;
        shared = null;
        Transform[] cards = singleEye ? new[] { centreCard } : new[] { leftCard, rightCard };
        var combine = new CombineInstance[cards.Length];
        var centres = new System.Collections.Generic.List<Vector4>();
        Matrix4x4 toEyes = transform.worldToLocalMatrix;
        for (int c = 0; c < cards.Length; c++)
        {
            Transform card = cards[c];
            if (card == null || !card.IsChildOf(transform) || !card.gameObject.activeSelf ||
                !card.TryGetComponent(out MeshRenderer renderer) || !renderer.enabled ||
                !card.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
                return false;
            if (c == 0) shared = renderer.sharedMaterial;
            if (shared == null || shared != renderer.sharedMaterial || shared.shader.name != "Goblins/Eyes" ||
                !filter.sharedMesh.isReadable)
            {
                Debug.LogWarning($"{name}: eye cards need readable meshes and one shared Goblins/Eyes material.", this);
                return false;
            }
            Matrix4x4 local = toEyes * card.localToWorldMatrix;
            combine[c] = new CombineInstance { mesh = filter.sharedMesh, transform = local };
            Vector3 centre = local.MultiplyPoint3x4(filter.sharedMesh.bounds.center);
            // A central eye has side zero, so unequal-eye variation never enlarges it.
            float side = singleEye ? 0f : (c == 0 ? -1f : 1f);
            for (int i = 0; i < filter.sharedMesh.vertexCount; i++)
                centres.Add(new Vector4(centre.x, centre.y, centre.z, side));
        }
        combined = new Mesh { name = $"{name} (Combined Eye Cards)" };
        combined.CombineMeshes(combine, true, true, false);
        combined.SetUVs(1, centres);
        // Eye variation is capped at 2. Enlarge the bounds to cover scaling around the individual eye centres.
        Bounds bounds = combined.bounds;
        bounds.Expand(bounds.size);
        combined.bounds = bounds;
        return true;
    }

#if UNITY_EDITOR
    [InspectorButton("Create / Update Eye Cards")]
    public void Rebuild()
    {
        // Open prefab assets in Prefab Mode; never create unsaved children directly on a persistent asset.
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorUtility.IsPersistent(this))
            return;

        Undo.RecordObject(this, "Update Eye Cards");
        if (singleEye)
        {
            centreCard = EnsureCard(centreCard, "Eye_Centre");
            ApplyCard(centreCard, 0f, 1f);
            centreCard.localPosition = new Vector3(centreX, centreY, centreForward);
            centreCard.localRotation = Quaternion.Euler(centrePitch, centreYaw, centreRoll) * Quaternion.Euler(0f, 180f, 0f);
            centreCard.localScale = new Vector3(centreWidth, centreHeight, 1f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(centreCard);
        }
        else
        {
            leftCard = EnsureCard(leftCard, "Eye_Left");
            rightCard = EnsureCard(rightCard, "Eye_Right");
            ApplyCard(leftCard, -1f, leftSize);
            ApplyCard(rightCard, 1f, rightSize);
        }
        SetCardActive(leftCard, !singleEye);
        SetCardActive(rightCard, !singleEye);
        SetCardActive(centreCard, singleEye);
        PrefabUtility.RecordPrefabInstancePropertyModifications(this);
        EditorUtility.SetDirty(this);
    }

    public void ConfigureCyclopsFrom(Horde_EyeCards pair)
    {
        singleEye = true;
        centreWidth = pair.width * 1.4f;
        centreHeight = pair.eyeHeight * 1.4f;
        centreY = pair.height;
        centreForward = pair.forward;
        centrePitch = pair.pitch;
        material = pair.material;
        Rebuild();
    }

    private static void SetCardActive(Transform card, bool active)
    {
        if (card == null || card.gameObject.activeSelf == active) return;
        Undo.RecordObject(card.gameObject, "Set Eye Layout");
        card.gameObject.SetActive(active);
        PrefabUtility.RecordPrefabInstancePropertyModifications(card.gameObject);
    }

    private Transform EnsureCard(Transform card, string cardName)
    {
        if (card == null || card.parent != transform)
            card = transform.Find(cardName);

        if (card == null)
        {
            GameObject created = GameObject.CreatePrimitive(PrimitiveType.Quad);
            created.name = cardName;
            Undo.RegisterCreatedObjectUndo(created, "Create Eye Card");
            Undo.SetTransformParent(created.transform, transform, "Parent Eye Card");
            created.layer = gameObject.layer;
            card = created.transform;
            // CreatePrimitive adds a collider; the eye cards are purely visual.
            if (created.TryGetComponent(out Collider collider))
                Undo.DestroyObjectImmediate(collider);
        }

        // Repair missing components without replacing the authored GameObject.
        if (!card.TryGetComponent(out MeshFilter filter))
            filter = Undo.AddComponent<MeshFilter>(card.gameObject);
        if (filter.sharedMesh == null)
        {
            Undo.RecordObject(filter, "Set Eye Card Mesh");
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
        }
        if (!card.TryGetComponent<MeshRenderer>(out _))
            Undo.AddComponent<MeshRenderer>(card.gameObject);

        return card;
    }

    private void ApplyCard(Transform card, float side, float scale)
    {
        Undo.RecordObject(card, "Place Eye Card");
        card.localPosition = new Vector3(side * spacing * 0.5f, height, forward);
        // Unity's Quad faces -Z. Turn it to +Z first, then apply the authored tilt and mirrored rotation.
        card.localRotation = Quaternion.Euler(pitch, side * splay, side * roll) * Quaternion.Euler(0f, 180f, 0f);
        card.localScale = new Vector3(width * scale, eyeHeight * scale, 1f);
        PrefabUtility.RecordPrefabInstancePropertyModifications(card);

        MeshRenderer renderer = card.GetComponent<MeshRenderer>();
        Undo.RecordObject(renderer, "Set Eye Card Material");
        Material preview = material;
        if (preview == null)
            preview = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Goblins/Shader/Goblin_Eyes.mat");
        if (preview == null && GraphicsSettings.defaultRenderPipeline != null)
            preview = GraphicsSettings.defaultRenderPipeline.defaultMaterial;
        if (preview != null)
            renderer.sharedMaterial = preview;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
    }
#endif
}
