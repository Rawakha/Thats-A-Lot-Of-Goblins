using System;
using UnityEngine;

// Keeps the plane in the area, and brings it in. Past the area's warning line it reports how far (WarningChanged). At the
// respawn line it takes every control from the player, the camera stops and the plane flies on off the screen, the
// screen fades to black, and the plane is put south of the area's spawn point. As the screen fades back in the plane
// flies up from the bottom, the camera catches it after a short delay, and control returns as it passes the spawn point.
// A new area enters the same way, from black. Goblins carry on throughout, so going off the edge costs time.
// All prompts slide away for the whole sequence (UI_Prompts.Suppress).
//
// Going to the next area (GoToNextArea, from the results screen): the screen fades to black, the Upgrade Hook runs (the
// pick-3 shop goes there; with nothing set it passes straight on), then the Run scene reloads with the health pool carried
// (Run_Carry), and the next area enters from black as above. With one area, the next area is the same one. As that area
// fades in, the health earned comes back (Run_State.RestoreHealth). The map limits stay on throughout.
//
// The end of an area, pass or fail (PlayEnding): a beat where nothing interrupts, in slow motion if the last goblin was
// killed. Then control is taken, the plane turns south and flies off the screen while the camera stops on the battlefield and
// the engine fades. Once it's off screen the plane stops, and the results screen comes up over the wreckage. Corpse and gib
// clean-up is paused from the start of this until the scene reloads.
//
// Later: the plane being shot down replaces the respawn line (EdgeCrossed is the place).
//
// Setup: put it on an object in the Run scene and assign the Plane, Camera Rig, Fade and Run State. The bounds and
// spawn come from each area's Area_Root.
//
// GC: none per frame. One UpdateManager subscription (OnUpdate), plus Area_Root.Changed. The
// plane's trails clear on the teleport.
public class Run_Edge : MonoBehaviour
{
    private enum Step
    {
        Playing,
        FlyingOff,
        FadingOut,
        Black,
        FlyingIn,
        Hook,
        Loading,
        EndBeat,
        EndFlying,
        Ended
    }

    [SerializeField] private Plane_Controller plane;
    [SerializeField] private Camera_Rig cameraRig;
    [SerializeField] private UI_Fade fade;

    [Tooltip("Gives back health as the next area fades in, and carries the health pool to it.")]
    [SerializeField] private Run_State run;

    [Header("Flying Off")]
    [Tooltip("How much the plane turns to head straight out through the edge rather than along it. 0 keeps its heading, " +
             "1 turns straight out.")]
    [Range(0f, 1f)] [SerializeField] private float steerOut = 0.6f;

    [Tooltip("Metres the camera drifts on as it stops, so it eases to a halt.")]
    [Min(0f)] [SerializeField] private float cameraCoast = 8f;

    [Tooltip("Seconds of watching the plane fly on before the fade starts.")]
    [Min(0f)] [SerializeField] private float flyOffTime = 0.7f;

    [Header("End Of Area")]
    [Tooltip("Seconds after the last goblin goes before control is taken, so the moment can land.")]
    [Min(0f)] [SerializeField] private float endBeat = 1f;

    [Tooltip("Game speed at the start of the beat when the last goblin was killed, easing back to normal over the beat. 1 for no slow motion.")]
    [Range(0.05f, 1f)] [SerializeField] private float endSlowMotion = 0.3f;

    [Tooltip("How far past the screen edge the plane must be before the results come up, as a share of the screen.")]
    [Min(0f)] [SerializeField] private float endOffscreenMargin = 0.1f;

    [Tooltip("The results come up after this many seconds even if the plane is still on screen.")]
    [Min(0f)] [SerializeField] private float endMaxFlyTime = 4f;

    [Header("Flying In")]
    [Tooltip("Seconds held on black.")]
    [Min(0f)] [SerializeField] private float blackTime = 0.15f;

    [Tooltip("Seconds from the fade in starting to the camera starting to catch the plane.")]
    [Min(0f)] [SerializeField] private float catchDelay = 0.8f;

    [Tooltip("Seconds the camera takes to catch the plane. Longer is lazier.")]
    [Min(0f)] [SerializeField] private float catchTime = 0.9f;

    private Plane_Input input;
    private Wave_Director director;
    private Area_Bounds bounds;
    private Area_Spawn spawn;
    private Step step;
    private float timer;
    private float warning;
    private bool arrive;
    private bool waitingToCatch;
    private bool promptsHidden;
    private bool leaving;
    private bool freshArea;
    private string areaPath;
    private Camera view;
    private Action endDone;
    private bool endPending;
    private bool endSlow;

    // Runs on black between areas, with a Continue to call when done: the pick-3 shop shows here and calls it once the
    // player has picked. Null (nothing set) passes straight on.
    public Action<Action> UpgradeHook;

    // How far past the warning line the plane is: 0 inside, up to 1 at the respawn line, 0 once control is taken
    public float Warning => warning;

    public event Action<float> WarningChanged;

    // The plane hit the respawn line
    public event Action EdgeCrossed;

    private void Awake()
    {
        Horde_DebrisBudget.CleanupPaused = false;

        if (plane == null || cameraRig == null || fade == null || run == null)
        {
            Debug.LogError($"{name}: Run_Edge needs a Plane, a Camera Rig, a Fade and a Run State.", this);
            enabled = false;

            if (fade != null)
                fade.Set(0f);

            return;
        }

        input = plane.GetComponent<Plane_Input>();
        view = cameraRig.GetComponent<Camera>();
    }

    private void OnEnable()
    {
        Area_Root.Changed += Bind;
        Bind(Area_Root.Current);

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate += Tick;
    }

    private void OnDisable()
    {
        HidePrompts(false);
        Area_Root.Changed -= Bind;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnUpdate -= Tick;
    }

    // A new area loaded (or the old one went, with null)
    private void Bind(Area_Root root)
    {
        director = root != null ? root.Director : null;
        bounds = root != null ? root.Bounds : null;
        spawn = root != null ? root.Spawn : null;
        leaving = false;

        if (root == null)
            return;

        areaPath = root.gameObject.scene.path;

        if (bounds == null || spawn == null)
        {
            Debug.LogWarning($"{name}: this area has no Area_Bounds or Area_Spawn. See its Area_Root checklist.", root);
            fade.FadeIn();
            return;
        }

        // On the next tick, once everything in the new scene is awake
        arrive = true;
    }

    private void Tick()
    {
        if (arrive)
        {
            arrive = false;
            freshArea = true;
            HidePrompts(true);
            SetLocked(true);
            fade.Cover();
            PlacePlane();
            step = Step.Black;
            timer = blackTime;
        }

        float deltaTime = Time.deltaTime;

        switch (step)
        {
            case Step.Playing:
                if (endPending)
                    BeginEnding();
                else
                    CheckEdge();

                break;

            case Step.FlyingOff:
                timer -= deltaTime;

                if (timer <= 0f)
                {
                    fade.FadeOut();
                    step = Step.FadingOut;
                }

                break;

            case Step.FadingOut:
                if (fade.Done && leaving)
                {
                    // On black, leaving: the pick-3 shop goes here, then the next area loads
                    step = Step.Hook;

                    if (UpgradeHook != null)
                        UpgradeHook(Continue);
                    else
                        Continue();
                }
                else if (fade.Done)
                {
                    PlacePlane();
                    step = Step.Black;
                    timer = blackTime;
                }

                break;

            case Step.Black:
                // Stays covered until the area has fully loaded
                if (Run_Boot.Loading)
                    break;

                timer -= deltaTime;

                if (timer <= 0f)
                {
                    fade.FadeIn();
                    step = Step.FlyingIn;
                    timer = catchDelay;
                    waitingToCatch = true;

                    // A new area is fading in: the health earned in the last one comes back now
                    if (freshArea)
                    {
                        freshArea = false;
                        run.RestoreHealth();
                    }
                }

                break;

            case Step.FlyingIn:
                FlyIn(deltaTime);
                break;

            case Step.EndBeat:
                TickEndBeat();
                break;

            case Step.EndFlying:
                TickEndFlying(deltaTime);
                break;
        }
    }

    private void CheckEdge()
    {
        if (bounds == null)
            return;

        Vector3 position = plane.transform.position;

        SetWarning(bounds.WarningAmount(position));

        if (!bounds.PastRespawn(position))
            return;

        // Off it goes: heading out through the edge, camera stopping, no controls
        Vector3 forward = plane.transform.forward;
        Vector3 away = Vector3.Slerp(forward, bounds.Outward(position), steerOut);

        // Every prompt slides away for the whole sequence, before the warning goes so nothing underneath comes back
        HidePrompts(true);
        SetWarning(0f);
        SetLocked(true, away);
        cameraRig.Hold(cameraCoast);

        step = Step.FlyingOff;
        timer = flyOffTime;
        EdgeCrossed?.Invoke();
    }

    // Plays the end of the area, then calls done. If the plane is mid-respawn it starts once the plane is back in play.
    public void PlayEnding(bool slowMotion, Action done)
    {
        endSlow = slowMotion;
        endDone = done;
        endPending = true;
        Horde_DebrisBudget.CleanupPaused = true;
    }

    private void BeginEnding()
    {
        endPending = false;
        SetWarning(0f);
        step = Step.EndBeat;
        timer = endBeat;

        if (endSlow)
            Time.timeScale = endSlowMotion;
    }

    // Unscaled, since this is what the slow motion is timed by
    private void TickEndBeat()
    {
        timer -= Time.unscaledDeltaTime;

        if (endSlow && endBeat > 0f)
            Time.timeScale = Mathf.Lerp(endSlowMotion, 1f, Mathf.Clamp01(1f - timer / endBeat));

        if (timer > 0f)
            return;

        Time.timeScale = 1f;
        HidePrompts(true);
        SetLocked(true, Vector3.back);
        cameraRig.Hold(cameraCoast);

        if (plane.TryGetComponent(out Plane_Audio planeAudio))
            planeAudio.FadeOut();

        step = Step.EndFlying;
        timer = endMaxFlyTime;
    }

    private void TickEndFlying(float deltaTime)
    {
        timer -= deltaTime;
        Vector3 point = view.WorldToViewportPoint(plane.transform.position);
        float past = Mathf.Max(point.x - 1f, -point.x, point.y - 1f, -point.y);

        if (past < endOffscreenMargin && timer > 0f)
            return;

        plane.Stop();
        step = Step.Ended;
        endDone?.Invoke();
    }

    // Covers the screen at once for a reload
    public void CoverForLoading() => fade.Cover();

    // Covered, and on to the next area
    public void GoToNextArea()
    {
        if (leaving)
            return;

        leaving = true;
        HidePrompts(true);
        SetWarning(0f);
        SetLocked(true, plane.transform.forward);
        fade.Cover();
        step = Step.FadingOut;
    }

    // The pick-3 shop (or nothing) is done: carry the run to the next area, which is the same one while there's only one
    private void Continue()
    {
        if (step != Step.Hook)
            return;

        step = Step.Loading;
        run.SaveCarry(areaPath);
        run.LoadNextArea();
    }

    private void FlyIn(float deltaTime)
    {
        if (waitingToCatch)
        {
            timer -= deltaTime;

            if (timer <= 0f)
            {
                waitingToCatch = false;
                cameraRig.CatchUp(catchTime);
            }
        }

        // Control returns as it passes the spawn point; the camera may still be catching up
        if (spawn == null || plane.transform.position.z >= spawn.Point.z)
        {
            if (waitingToCatch)
            {
                waitingToCatch = false;
                cameraRig.CatchUp(catchTime);
            }

            SetLocked(false);
            HidePrompts(false);
            step = Step.Playing;
        }
    }

    // All prompts away (and back, after) without ever getting the count out of step
    private void HidePrompts(bool hide)
    {
        if (hide == promptsHidden)
            return;

        promptsHidden = hide;
        UI_Prompts.Suppress(hide);
    }

    // South of the spawn point heading north, with the camera waiting on the spawn point
    private void PlacePlane()
    {
        if (spawn == null)
            return;

        plane.Teleport(spawn.Start, 0f);
        SetLocked(true, Vector3.forward);
        cameraRig.HoldAt(spawn.Point);
    }

    private void SetLocked(bool locked) => SetLocked(locked, Vector3.forward);

    private void SetLocked(bool locked, Vector3 direction)
    {
        if (locked)
            input.Lock(direction);
        else
            input.Unlock();

        if (director != null)
            director.InputLocked = locked;
    }

    private void SetWarning(float amount)
    {
        if (Mathf.Approximately(amount, warning))
            return;

        warning = amount;
        WarningChanged?.Invoke(amount);
    }
}
