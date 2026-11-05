using UnityEngine;

// Every sound a goblin makes, in one asset. Assign it to a Horde_GoblinType's Audio; types can share one. Empty slots stay
// silent. Horde_System caps how many play per frame, so a big blast doesn't stack hundreds.
//
// Setup: Create > Goblins/Goblin Audio, fill in the Sound Datas, drag it into the goblin type.
//
// GC: none (read-only data). No subscriptions.
[CreateAssetMenu(menuName = "Goblins/Goblin Audio", fileName = "Goblin Audio")]
public class Horde_GoblinAudio : ScriptableObject
{
    [Header("Hurt")]
    [Tooltip("Played where a goblin is hurt, on every hit (the killing one too).")]
    public SoundData damageSound;

    [Tooltip("Extra sounds a hit might set off as well, each on its own chance (a groan, a yelp).")]
    public Horde_ChanceSound[] extraDamageSounds;

    [Header("Death")]
    [Tooltip("A normal death: the body stays and topples (scraps may tear off it).")]
    public SoundData corpseDeathSound;

    [Tooltip("A death in pieces: the head pops off.")]
    public SoundData gibDeathSound;

    [Tooltip("Blown apart by a blast: the whole goblin bursts into pieces. Empty uses Gib Death Sound.")]
    public SoundData blownApartSound;

    [Header("Flung")]
    [Tooltip("Looping scream while flung through the air, following the goblin and quieter as it comes down. Limited by the AudioManager's tracked sound slots.")]
    public SoundData screamSound;

    [Tooltip("Height above the ground at which the scream is at full volume, metres.")]
    [Min(0.1f)] public float screamFullHeight = 6f;

    [Tooltip("Scream volume just before it lands, as a share of full.")]
    [Range(0f, 1f)] public float screamMinVolume = 0.25f;
}
