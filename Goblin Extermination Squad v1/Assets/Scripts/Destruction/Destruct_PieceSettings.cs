using UnityEngine;

// How a breakable piece reacts to hits, in one asset so many buildings share it and it can be tuned in play mode.
// Distances and speeds are world units, so a house-sized piece wants bigger radii than a crate: make one asset per
// size class (small props, houses, towers).
[CreateAssetMenu(menuName = "Goblins/Destruction/Piece Settings", fileName = "Destruct_PieceSettings")]
public class Destruct_PieceSettings : ScriptableObject
{
    [Header("Crack")]
    [Tooltip("How much a hit cracks, per share of max health it takes. 2 means losing half its health fully cracks " +
             "where it was hit.")]
    [Min(0f)] public float crackPerDamage = 2f;

    [Tooltip("World distance around a hit that cracks. Shards further away loosen less.")]
    [Min(0.01f)] public float crackRadius = 3f;

    [Tooltip("Share of each crack that loosens every shard, wherever the hit landed, so the whole thing looks weaker.")]
    [Range(0f, 1f)] public float crackFill = 0.1f;

    [Tooltip("How far a fully cracked shard sits out from the centre, as a share of its distance from it.")]
    [Min(0f)] public float crackSpread = 0.06f;

    [Tooltip("Degrees a fully cracked shard tips over.")]
    [Range(0f, 45f)] public float crackTilt = 6f;

    [Tooltip("How far shards jump out on a hit before settling, as a multiple of the crack they took.")]
    [Min(0f)] public float crackPop = 2f;

    [Tooltip("How quickly shards creep out to their new crack, per second.")]
    [Min(0.01f)] public float crackSpeed = 3f;

    [Tooltip("How quickly the jump settles back, per second.")]
    [Min(0.01f)] public float popSettle = 10f;

    [Header("Break Off")]
    [Tooltip("Most shards one hit knocks off, nearest the hit first.")]
    [Min(0)] public int chunksPerHit = 1;

    [Tooltip("Only shards within this world distance of the hit can be knocked off.")]
    [Min(0.01f)] public float breakOffRadius = 2.5f;

    [Tooltip("Speed of a knocked-off chunk along the shot, m/s. Low and they drop off rather than fly.")]
    [Min(0f)] public float breakOffSpeed = 3f;

    [Tooltip("Speed out of the surface, away from the piece's centre, m/s.")]
    [Min(0f)] public float breakOffPush = 2f;

    [Tooltip("Dust and bits where a chunk comes off. Optional.")]
    public Effect_Hit breakOffEffect;
    public SoundConfig breakOffSound;

    [Header("Shatter")]
    [Tooltip("Speed along the killing shot for the shards nearest it, m/s.")]
    [Min(0f)] public float shatterSpeed = 8f;

    [Tooltip("How much slower the shards furthest from the hit go. At 1 they just collapse under gravity.")]
    [Range(0f, 1f)] public float shatterFalloff = 0.8f;

    [Tooltip("Burst away from the hit point, m/s, strongest near it.")]
    [Min(0f)] public float shatterBurst = 6f;

    [Tooltip("Burst away from the piece's centre for every shard, m/s, so the whole thing bulges as it goes.")]
    [Min(0f)] public float shatterOutward = 2f;

    [Tooltip("Upward kick near the hit, m/s. Sells an explosion.")]
    [Min(0f)] public float shatterUp = 3f;

    [Tooltip("Random speed per shard, as a share of the shatter speed.")]
    [Range(0f, 1f)] public float shatterScatter = 0.3f;

    [Tooltip("Random spin, degrees/second.")]
    [Min(0f)] public float shatterSpin = 180f;

    [Tooltip("Played at the piece's centre when it shatters (dust cloud, debris burst). Optional.")]
    public Effect_Hit shatterEffect;
    public SoundConfig shatterSound;

    [Tooltip("Camera trauma when it shatters, 0-1.")]
    [Range(0f, 1f)] public float shatterTrauma = 0.4f;

    [Header("Support")]
    [Tooltip("Shards whose lowest point is within this world distance of the piece's lowest point stand on the ground. " +
             "Anything that loses its path through touching shards to one of them falls.")]
    [Min(0f)] public float groundTolerance = 0.5f;

    [Tooltip("When less than this share of the ground shards is left, the whole thing gives way, however it's still " +
             "connected. Stops a house standing on one sliver. 0 turns it off.")]
    [Range(0f, 1f)] public float minBaseLeft = 0.35f;

    [Tooltip("Seconds an unsupported shard shudders before it drops.")]
    [Min(0f)] public float collapseDelay = 0.12f;

    [Tooltip("Random extra delay per shard, seconds, so it crumbles rather than dropping as one block.")]
    [Min(0f)] public float collapseStagger = 0.2f;

    [Tooltip("Extra delay at the top of what's falling, seconds. Positive crumbles from the bottom up, negative from " +
             "the top down.")]
    public float collapseRipple = 0.25f;

    [Tooltip("How hard an unsupported shard shudders while it waits, as a crack jump.")]
    [Min(0f)] public float collapseShudder = 0.6f;

    [Tooltip("Random speed a falling shard starts with, m/s. Gravity does the rest.")]
    [Min(0f)] public float collapseScatter = 0.8f;

    [Tooltip("Speed out from the piece's centre, m/s, so walls fall outward rather than straight down.")]
    [Min(0f)] public float collapseOutward = 0.6f;

    [Tooltip("Random spin, degrees/second.")]
    [Min(0f)] public float collapseSpin = 60f;

    [Tooltip("Played at the piece's centre when it gives way. Optional.")]
    public Effect_Hit collapseEffect;
    public SoundConfig collapseSound;

    [Tooltip("Camera trauma when it gives way, 0-1.")]
    [Range(0f, 1f)] public float collapseTrauma = 0.3f;

    [Header("Debris Physics")]
    [Tooltip("Fastest overlapping shards are pushed apart by physics, m/s. Shards touch when they break, and a high " +
             "value makes them pop apart on their own.")]
    [Min(0.01f)] public float depenetrationSpeed = 1f;
}
