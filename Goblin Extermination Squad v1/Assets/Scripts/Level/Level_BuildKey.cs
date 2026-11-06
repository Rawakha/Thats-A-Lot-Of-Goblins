#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// A fingerprint of what a level tool builds from, so its editor rebuilds can be skipped when nothing has changed.
// Unity calls OnValidate and OnEnable on every script reload, scene open and return from play; without this, each
// one rebuilt every ground, cliff and tree area, marked the scene unsaved, and gave re-made trees new file IDs.
//
// The key is the component's serialized fields (minus the ones a build writes itself: stats, the made mesh) plus
// anything extra the tool depends on, hashed. Tools keep it unserialized: after a reload or an undo it's empty or
// stale, and the first check after a load just adopts the current inputs, since what was saved was built from them.
//
// Editor only. GC: a JSON string per check, edit time only.
public static class Level_BuildKey
{
    public static string Of(Object source, string extra, params string[] ignore)
    {
        string json = EditorJsonUtility.ToJson(source);

        // Drop the build's own outputs: a number, or an object reference like {"instanceID":123}
        for (int i = 0; i < ignore.Length; i++)
            json = Regex.Replace(json, "\"" + ignore[i] + "\":(\\{[^}]*\\}|[^,}]*),?", "");

        return Hash128.Compute(json + extra).ToString();
    }

    // For keys saved with the scene, which must still match after a restart: only the tool's own fields, with object
    // references left out because they hold instance IDs. Put anything those references change into extra.
    public static string Stable(Object source, string extra, params string[] ignore)
    {
        string json = JsonUtility.ToJson(source);

        for (int i = 0; i < ignore.Length; i++)
            json = Regex.Replace(json, "\"" + ignore[i] + "\":(\\{[^}]*\\}|[^,}]*),?", "");

        json = Regex.Replace(json, "\\{\"\\w*[Ii][Dd]\":-?\\d+\\}", "{}");
        return Hash128.Compute(json + extra).ToString();
    }
}
#endif
