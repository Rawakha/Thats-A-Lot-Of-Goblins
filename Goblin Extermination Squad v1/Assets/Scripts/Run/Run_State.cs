using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

// The run's state, in the Run scene. For now that's the health pool: each goblin reaching its goal hole is a leak, and
// reaching the first area's Starting Health (0 for no limit) marks a failed result, but play and spawning continue.
// The result is only shown once the final wave has spawned and every goblin has been killed or reached its goal hole.
// Debug waves count too (Count Debug Waves) unless you turn that off for recording clips; they never end an area.
// Clearing an area earns back a share of the lost health (Health Back on its Area_Settings, rounded up), but it only
// comes back when the next area starts (RestoreHealth, called as the screen fades in), so it lands as a pop on the
// counter. The pool carries over between areas through Run_Carry; only the first area sets it. Restart clears the carry
// and reloads the Run scene, which loads the start area again.
//
// Setup: put it on an object in the Run scene. It finds each area as it loads (Area_Root.Changed).
//
// GC: none per frame. No UpdateManager subscription; subscribes to Area_Root.Changed and to the current area's
// Wave_Director.GoblinReachedHole and AreaCleared.
public class Run_State : MonoBehaviour
{
    [Tooltip("Goblins from Debug Waves count as leaks too. Turn off to record clips without the run failing.")]
    [SerializeField] private bool countDebugWaves = true;

    [Header("Stats")]
    [SerializeField, ReadOnly] private int leaks;
    [SerializeField, ReadOnly] private int leakLimit;
    [SerializeField, ReadOnly] private int healthToCome;
    [SerializeField, ReadOnly] private bool failed;
    [SerializeField, ReadOnly] private bool cleared;

    private Wave_Director director;
    private Area_Settings area;
    private bool limitSet;
    private bool restarting;

    public int Leaks => leaks;

    // The most recent goblin to go was killed, rather than reaching a hole
    public bool LastGoblinKilled { get; private set; }

    // Goblins that got away in this area alone
    public int LeaksThisArea { get; private set; }
    public int LeakLimit => leakLimit;
    public int LeaksLeft => Mathf.Max(0, leakLimit - leaks);
    public bool Failed => failed;
    public bool Cleared => cleared;

    // The health that came back last (set as it does)
    public int LastHealthBack { get; private set; }
    public Area_Settings Area => area;
    public string AreaName => area != null ? area.DisplayName : string.Empty;

    public event Action LeaksChanged;
    public event Action LimitChanged;
    public event Action RunFailed;
    public event Action AreaCleared;

    // Health came back (after a clear, as the next area starts)
    public event Action HealthRestored;

    private void OnEnable()
    {
        // Coming from the last area: the health pool and what's still owed
        if (Run_Carry.Has && !limitSet)
        {
            limitSet = true;
            leaks = Run_Carry.Leaks;
            leakLimit = Run_Carry.Limit;
            healthToCome = Run_Carry.PendingBack;
        }

        Area_Root.Changed += Bind;
        Bind(Area_Root.Current);
    }

    private void OnDisable()
    {
        Area_Root.Changed -= Bind;
        Unbind();
    }

    private void Unbind()
    {
        if (director != null)
        {
            director.GoblinReachedHole -= OnReached;
            director.GoblinKilled -= OnKilled;
            director.AreaCleared -= OnAreaCleared;
        }

        director = null;
        area = null;
    }

    // A new area loaded (or the old one went, with null)
    private void Bind(Area_Root root)
    {
        Unbind();

        if (root == null)
            return;

        area = root.Settings;
        director = root.Director;

        if (area == null)
        {
            Debug.LogError($"{name}: the area has no Area Settings.", this);
            director = null;
            return;
        }

        director.GoblinReachedHole += OnReached;
        director.GoblinKilled += OnKilled;
        director.AreaCleared += OnAreaCleared;
        cleared = false;

        // Health carries over; the first area sets it
        if (!limitSet)
        {
            limitSet = true;
            leakLimit = area.startingHealth;
            LimitChanged?.Invoke();
        }
    }

    private void OnKilled(Horde_Goblin goblin) => LastGoblinKilled = true;

    private void OnReached(Wave_Hole hole, Horde_Goblin goblin)
    {
        LastGoblinKilled = false;

        if (countDebugWaves || !director.DebugWaves)
            AddLeak();
    }

    // Also a button, to test the HUD and the eventual result without waiting for goblins to walk
    [InspectorButton("Add Leak", true)]
    public void AddLeak()
    {
        if (failed || cleared)
            return;

        leaks++;
        LeaksThisArea++;
        LeaksChanged?.Invoke();
    }

    private void Fail()
    {
        failed = true;

        if (director != null)
            director.StopWaves();

        RunFailed?.Invoke();
    }

    private void OnAreaCleared()
    {
        if (failed || cleared)
            return;

        // The director only finishes the area after the final wave and all living goblins are gone.
        if (leakLimit > 0 && leaks >= leakLimit)
        {
            Fail();
            return;
        }

        cleared = true;
        healthToCome = Mathf.Min(leaks, Mathf.CeilToInt(leaks * area.healthBack));
        AreaCleared?.Invoke();
    }

    // The health earned by the last area comes back now: the counter pops. Run_Edge calls it as the next area fades in.
    public void RestoreHealth()
    {
        if (healthToCome <= 0)
            return;

        LastHealthBack = healthToCome;
        leaks -= healthToCome;
        healthToCome = 0;
        HealthRestored?.Invoke();
    }

    // Keeps the health pool for the next area, which is loaded after a reload of the Run scene (Run_Edge does the reload)
    public void SaveCarry(string nextAreaPath)
    {
        Run_Carry.Has = true;
        Run_Carry.Leaks = leaks;
        Run_Carry.Limit = leakLimit;
        Run_Carry.PendingBack = healthToCome;
        Run_Carry.AreaPath = nextAreaPath;
    }

    // Reloads the Run scene (and with it the start area) for a clean run: every pool, the debris, the health and the
    // waves start fresh
    public void Restart()
    {
        if (restarting)
            return;

        if (!Reload("restart"))
            return;

        Run_Carry.Clear();
    }

    // Reloads the Run scene to start the next area, with the carry saved
    public void LoadNextArea() => Reload("load the next area");

    private bool Reload(string what)
    {
        if (restarting)
            return false;

        int index = gameObject.scene.buildIndex;

        if (index < 0)
        {
            Debug.LogError($"{name}: the Run scene isn't in Build Settings, so it can't {what}.", this);
            return false;
        }

        restarting = true;
        DOTween.KillAll();
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(index); // Async so the loading dots keep moving; the cover hides the old scene
        return true;
    }
}
