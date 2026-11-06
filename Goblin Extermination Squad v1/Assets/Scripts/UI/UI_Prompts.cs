using System.Collections.Generic;
using UnityEngine;

// The prompt system: keeps the list of prompts that want to be on screen and shows only the highest priority one. When a
// higher prompt arrives the one showing slides out and the new one slides in; when it goes, the one below slides back.
// Suppress hides them all (e.g. during a fade) and they return afterwards. Nothing to set up and no scene object: prompts
// (UI_Prompt) register themselves, and everything is static.
//
// GC: none per frame; it only works when a prompt's wanted state changes. No subscriptions. The list is sized once.
public static class UI_Prompts
{
    private static readonly List<UI_Prompt> wanted = new List<UI_Prompt>(8);
    private static UI_Prompt shown;
    private static int counter;
    private static int suppressed;

    // Scene reloads and domain-reload-free play: start empty
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        wanted.Clear();
        shown = null;
        counter = 0;
        suppressed = 0;
    }

    public static UI_Prompt Shown => shown;

    // While anything suppresses, no prompt shows (they all slide away, keeping what they want) and they come back when the
    // last one lets go: for fades and cutscenes. Every Suppress(true) needs its Suppress(false).
    public static void Suppress(bool on)
    {
        suppressed = Mathf.Max(0, suppressed + (on ? 1 : -1));
        Refresh();
    }

    internal static void Add(UI_Prompt prompt)
    {
        if (wanted.Contains(prompt))
            return;

        prompt.Order = ++counter;
        wanted.Add(prompt);
        Refresh();
    }

    internal static void Remove(UI_Prompt prompt)
    {
        if (!wanted.Remove(prompt) && shown != prompt)
            return;

        Refresh();
    }

    // Shows the highest priority wanted prompt, newest among equals, and slides the others away
    private static void Refresh()
    {
        UI_Prompt best = null;

        for (int i = wanted.Count - 1; i >= 0; i--)
        {
            UI_Prompt prompt = wanted[i];

            if (prompt == null)
            {
                wanted.RemoveAt(i);
                continue;
            }

            if (suppressed > 0)
                continue;

            if (best == null || prompt.Priority > best.Priority
                || (prompt.Priority == best.Priority && prompt.Order > best.Order))
                best = prompt;
        }

        if (best == shown)
            return;

        UI_Prompt leaving = shown;
        shown = best;
        float delay = 0f;

        if (leaving != null)
        {
            delay = leaving.SwapDelay;
            leaving.Display(false, 0f);
        }

        if (shown != null)
            shown.Display(true, leaving != null ? delay : 0f);
    }
}
