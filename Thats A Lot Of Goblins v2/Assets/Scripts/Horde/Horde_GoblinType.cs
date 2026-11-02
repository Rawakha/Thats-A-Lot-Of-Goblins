using UnityEngine;

// One kind of goblin. The prefab is the goblin, authored like any prefab:
//   - the root holds the physics (its own Rigidbody and collider, or the defaults below are added),
//   - Body and Head are siblings under it, each with its mesh,
//   - anything else with a MeshRenderer (a hat under Head, a gun under Body) is part of the goblin, exactly where it
//     sits in the prefab. Disabled renderers are left out, so a part can be switched off to try without it.
// At runtime the prefab is read once into a list of parts (Horde_RenderGroup); goblins keep no GameObjects for them
// and are drawn instanced. Everything under Head scales with the head; everything else with the body, and the head
// stays on the neck as the body grows or shrinks.
// More kinds of goblin are more of these assets; behaviours (shooters, carriers) will hang off them later.
[CreateAssetMenu(menuName = "Goblins/Horde/Goblin Type", fileName = "Horde_GoblinType")]
public class Horde_GoblinType : ScriptableObject
{
    [Header("Model")]
    [Tooltip("The goblin prefab: a root with Body and Head children and any other meshes. Add Horde_Goblin to it to " +
             "pick Body and Head by hand; otherwise children called Body and Head are used.")]
    public GameObject prefab;

    [Header("Size")]
    [Tooltip("Body scale on top of the prefab's. The head moves with it to stay on the neck.")]
    public Vector3 bodyScale = Vector3.one;

    [Tooltip("Head scale on top of the prefab's. Everything under Head (hats) scales with it.")]
    public Vector3 headScale = Vector3.one;

    [Header("Variation: Size")]
    [Tooltip("Each goblin's overall body size, picked between these. Multiplies Body Scale.")]
    public Vector2 bodySizeRange = new Vector2(0.9f, 1.15f);

    [Tooltip("Each goblin's body height against its width: below 1 squat, above 1 tall and thin. Its volume is kept.")]
    public Vector2 bodyShapeRange = new Vector2(0.9f, 1.1f);

    [Tooltip("Each goblin's head size, picked between these, separately from the body. Multiplies Head Scale.")]
    public Vector2 headSizeRange = new Vector2(0.85f, 1.2f);

    [Header("Eyes")]
    [Tooltip("Per-goblin eye size range, on top of the authored cards. Both eyes share one roll. " +
             "1,1 preserves the authored size; shader scales around each card's centre.")]
    public Vector2 eyeSizeRange = new Vector2(0.9f, 1.1f);

    [Tooltip("Chance on spawn that one randomly chosen eye is bigger. Both still blink and look together.")]
    [Range(0f, 1f)] public float eyeAsymmetryChance = 0.25f;
    [Tooltip("Extra size for the larger eye: 0.3 means 30% bigger. Final per-card variation is capped at 2x.")]
    public Vector2 eyeAsymmetryRange = new Vector2(0.15f, 0.35f);

    [Header("Variation: Colour")]
    [Tooltip("Each goblin starts from one of these, then has its hue, saturation and brightness nudged. Needs the " +
             "Goblins/Goblin shader (it reads _GoblinColorFlash per instance). Empty: every goblin uses the material's " +
             "Color.")]
    public Color[] baseColors =
    {
        // v1's goblin greens
        new Color(0.43529412f, 0.48235294f, 0.16470589f),
        new Color(0.3529412f, 0.48235294f, 0.16470589f),
        new Color(0.24705882f, 0.48235294f, 0.2901961f),
        new Color(0.24705882f, 0.48235294f, 0.3796876f)
    };
    [Tooltip("Hue shift, either way. 0.03 is subtle.")]
    public Vector2 hueRange = new Vector2(-0.03f, 0.03f);
    [Tooltip("Saturation multiplier.")]
    public Vector2 saturationRange = new Vector2(0.9f, 1.1f);
    [Tooltip("Brightness multiplier.")]
    public Vector2 valueRange = new Vector2(0.9f, 1.1f);

    // Rolls one goblin's look: body and head scale (on top of Body / Head Scale) and colour (linear, flash 0)
    public void RollLook(out Vector3 body, out Vector3 head, out Vector4 colour)
    {
        float size = Random.Range(bodySizeRange.x, bodySizeRange.y);
        float shape = Mathf.Max(0.05f, Random.Range(bodyShapeRange.x, bodyShapeRange.y));
        float width = 1f / Mathf.Sqrt(shape);
        body = Vector3.Scale(bodyScale, new Vector3(width, shape, width) * size);
        head = headScale * Random.Range(headSizeRange.x, headSizeRange.y);

        // Black tells the Goblin shader to use its material's own Color
        if (baseColors == null || baseColors.Length == 0)
        {
            colour = Vector4.zero;
            return;
        }

        Color.RGBToHSV(baseColors[Random.Range(0, baseColors.Length)], out float h, out float s, out float v);
        h = Mathf.Repeat(h + Random.Range(hueRange.x, hueRange.y), 1f);
        s = Mathf.Clamp01(s * Random.Range(saturationRange.x, saturationRange.y));
        v *= Random.Range(valueRange.x, valueRange.y);

        Color tint = Color.HSVToRGB(h, s, v, true).linear;
        colour = new Vector4(tint.r, tint.g, tint.b, 0f);
    }

    [Header("Animation")]
    [Tooltip("How its body and head hop, lean and bobble. Empty: goblins hold their prefab pose.")]
    public Horde_AnimationSettings animation;

    [Header("Health")]
    [Tooltip("Damage it takes to kill. A machine gun bullet does 1 by default.")]
    [Min(0.01f)] public float maxHealth = 3f;

    [Tooltip("Hit reaction force per point of damage. Bigger = bigger flinch.")]
    [Min(0f)] public float hitForce = 1f;

    [Tooltip("Played where a goblin is hurt, on every hit (the killing one too). Limited per frame by Horde_System's " +
             "Max Damage Sounds Per Frame, so a blast or a long burst doesn't stack hundreds. Optional.")]
    public SoundData damageSound;

    [Tooltip("Extra sounds a hit might set off as well, each on its own chance (a groan, a yelp). Every hit rolls " +
             "each one. Limited per frame by Horde_System's Max Extra Damage Sounds Per Frame.")]
    public Horde_ChanceSound[] extraDamageSounds;

    [Header("Death")]
    [Tooltip("Degrees a killed goblin tips over, falling away from the killing hit, pivoting at its feet.")]
    [Range(0f, 120f)] public float deathTipAngle = 85f;

    [Tooltip("How fast it tips: the tip spring's stiffness. Higher topples quicker.")]
    [Min(1f)] public float deathTipStiffness = 60f;

    [Tooltip("Tip spring bounce: lower bounces more when it hits the ground.")]
    [Range(0.05f, 1.5f)] public float deathTipDamping = 0.5f;

    [Tooltip("Starting tip speed, degrees/second, so it gets going at once rather than easing over.")]
    [Min(0f)] public float deathTipKick = 250f;

    [Tooltip("How far out from the feet the edge it tips over is, metres: the body's half-width. 0 measures it from " +
             "the prefab's body. Too small and it sinks into the ground as it falls; too big and it floats.")]
    [Min(0f)] public float deathTipEdge = 0f;

    [Tooltip("How much it bounces back up off the ground if it falls flat (90°). 0 stops dead.")]
    [Range(0f, 1f)] public float deathTipBounce = 0.3f;

    [Tooltip("Seconds it lies there before it starts to shrink, so the fall can be seen. A goblin killed in the air " +
             "starts counting once it has landed and come to rest.")]
    [Min(0f)] public float deathShrinkDelay = 0.3f;

    [Tooltip("Seconds it takes to shrink away, after the delay.")]
    [Min(0.01f)] public float deathDuration = 0.35f;

    [Tooltip("How it shrinks (DOTween ease, from full size to nothing). InBack swells a little first, then pops.")]
    public DG.Tweening.Ease deathEase = DG.Tweening.Ease.InBack;

    [Tooltip("Overshoot for Back eases, amplitude for Elastic ones.")]
    public float deathEaseOvershoot = 1.70158f;

    [Header("Blown Apart")]
    [Tooltip("Goblins in the middle of a blast pop out of existence this fast, seconds (along the death ease: InBack " +
             "swells, then gone).")]
    [Min(0.01f)] public float burstDuration = 0.12f;

    [Tooltip("Played where a goblin is blown apart (gore, confetti, a puff). Optional.")]
    public Effect_Hit burstEffect;
    public SoundData burstSound;

    [Header("Flung")]
    [Tooltip("Looping scream while flung through the air (like v1), following the goblin and quieter as it comes " +
             "down. Limited by the AudioManager's tracked sound slots, so a big blast doesn't stack hundreds.")]
    public SoundData screamSound;

    [Tooltip("Height above the ground at which the scream is at full volume, metres.")]
    [Min(0.1f)] public float screamFullHeight = 6f;

    [Tooltip("Scream volume just before it lands, as a share of full.")]
    [Range(0f, 1f)] public float screamMinVolume = 0.25f;

    [Tooltip("Thrown goblins get one torque impulse round the axis square to the throw (so they tip forward over the " +
             "way they're thrown), this much per m/s of the throw: v1's flick torque. Physics does the rest.")]
    [Min(0f)] public float launchTorque = 0.25f;

    [Header("Rendering")]
    public UnityEngine.Rendering.ShadowCastingMode shadows = UnityEngine.Rendering.ShadowCastingMode.On;
    public bool receiveShadows = true;

    [Header("Pool")]
    [Tooltip("Goblins of this type made up front, when a spawner using it starts. More are made if it runs out.")]
    [Min(0)] public int poolSize = 200;

    [Header("Movement")]
    [Tooltip("Walking speed, m/s.")]
    [Min(0f)] public float moveSpeed = 3f;

    [Tooltip("Random spread of walking speed per goblin, as a share of Move Speed.")]
    [Range(0f, 0.5f)] public float speedVariation = 0.1f;

    [Tooltip("How quickly it gets up to speed and changes direction, m/s².")]
    [Min(0f)] public float acceleration = 15f;

    [Tooltip("Turning speed, degrees/second.")]
    [Min(0f)] public float turnSpeed = 360f;

    [Tooltip("Each goblin walks a few degrees off the flow, its own way, so they spread out instead of lining up.")]
    [Range(0f, 45f)] public float steerSpread = 12f;

    [Tooltip("Below this speed it stops turning, m/s, so standing goblins don't spin.")]
    [Min(0f)] public float turnMinSpeed = 0.2f;

    [Header("Physics")]
    [Tooltip("PhysX solver passes per step for each goblin, overriding the project default (6). Goblins only push " +
             "each other apart, so 1-2 is plenty and much cheaper in crowds.")]
    [Range(1, 10)] public int solverIterations = 2;

    [Tooltip("Velocity solver passes per step. 1 is enough for goblins.")]
    [Range(1, 10)] public int solverVelocityIterations = 1;

    [Tooltip("Used only when the prefab has no Rigidbody.")]
    [Min(0.01f)] public float mass = 1f;
    [Tooltip("Used only when the prefab has no collider: a capsule this wide and tall, standing on the root.")]
    [Min(0.01f)] public float radius = 0.35f;
    [Min(0.02f)] public float height = 1f;
}

// A sound with a chance of playing each time it could (a goblin's groan on some hits)
[System.Serializable]
public struct Horde_ChanceSound
{
    public SoundData sound;

    [Tooltip("Chance it plays each time, 0 never, 1 always.")]
    [Range(0f, 1f)] public float chance;
}
