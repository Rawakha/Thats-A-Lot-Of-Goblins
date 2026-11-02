using UnityEngine;

// What happened, so the HUD knows which message set speaks. New contexts (freeze, fire, a goblin wedding...) are new
// values here plus a UI_MessageSet for them.
public enum UI_MessageContext
{
    Explosion       // a bomb or blast killed goblins
}

// The words and look for one context's messages ("BOOOOOM" for explosions), in one asset so the jokes are easy to
// write and tune. UI_ContextMessages picks a line at random (never the same one twice in a row) when its context
// happens with at least Min Kills, no more often than Cooldown. Sometimes a dark line plays instead: a rarer, bigger,
// longer message ("WIPED OUT THE DURSLEYS").
//
// GC: none at runtime; read-only data.
[CreateAssetMenu(menuName = "Goblins/Message Set", fileName = "UI_MessageSet")]
public class UI_MessageSet : ScriptableObject
{
    [Tooltip("Which event this set speaks for.")]
    public UI_MessageContext context;

    [Header("When")]
    [Tooltip("Fewest kills for a message to play. Smaller moments stay quiet.")]
    [Min(0)] public int minKills = 3;

    [Tooltip("Seconds after a message before this set can speak again, so blasts going off together don't spam.")]
    [Min(0f)] public float cooldown = 0.6f;

    [Header("Lines")]
    [Tooltip("Picked at random, never the same one twice in a row.")]
    public string[] lines =
    {
        "BOOOOOM",
        "KABOOM",
        "KA-BLAMMO",
        "SPLAT",
        "YEET",
        "AIRMAIL",
        "SPECIAL DELIVERY",
        "FREE FLIGHTS",
        "GOBLIN CONFETTI",
        "NOBODY ORDERED THAT",
        "SEE YOU NEVER",
        "LIFTOFF",
        "PARTY'S OVER"
    };

    [Header("Look")]
    public Color colour = new Color(1f, 0.7f, 0.15f);

    [Tooltip("Scale at Min Kills.")]
    [Min(0f)] public float baseSize = 1f;

    [Tooltip("Extra scale per kill above Min Kills, so a huge blast shouts.")]
    [Min(0f)] public float sizePerKill = 0.04f;

    [Tooltip("The biggest a normal line gets.")]
    [Min(0f)] public float maxSize = 1.8f;

    [Header("Dark Lines")]
    [Tooltip("Chance (0-1) a dark line plays instead of a normal one, when there are enough kills.")]
    [Range(0f, 1f)] public float darkChance = 0.15f;

    [Tooltip("Fewest kills for a dark line: roughly a whole family.")]
    [Min(0)] public int darkMinKills = 4;

    [Tooltip("Rare, bigger, longer. Picked at random, never the same one twice in a row.")]
    public string[] darkLines =
    {
        "WIPED OUT THE DURSLEYS",
        "FAMILY REUNION CANCELLED",
        "THE WILL IS NOW VOID",
        "NOBODY LEFT TO INHERIT",
        "THE FAMILY TREE IS A STUMP",
        "THE GROUP CHAT WENT QUIET",
        "NO ONE'S PICKING UP THE KIDS",
        "SOMEONE'S MUM IS CALLING",
        "CLOSED CASKET",
        "BULK DISCOUNT AT THE FUNERAL HOME",
        "CHRISTMAS IS CANCELLED",
        "THE DOG IS WAITING BY THE DOOR"
    };

    public Color darkColour = new Color(0.85f, 0.15f, 0.2f);

    [Tooltip("Dark lines are this much bigger than a normal line would be.")]
    [Min(0f)] public float darkSize = 1.3f;

    [Tooltip("Extra seconds a dark line stays, so it can be read.")]
    [Min(0f)] public float darkHold = 1f;
}
