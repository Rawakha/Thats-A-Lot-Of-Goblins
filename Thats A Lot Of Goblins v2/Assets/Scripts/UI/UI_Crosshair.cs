using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// The crosshair: a circle on the HUD that replaces the mouse cursor, so the important weapon feedback happens
// where the player is already looking.
//   - Follows the cursor (hidden while this is on). With a gamepad steering, or set to, it follows the shot point
//     (where the bullets go, aim assist included) or the aim point (the nose, no assist) instead.
//   - The inner fill is empty normally and fills over the main weapon's reload, so the player sees why the guns
//     stopped and when they're back.
//   - Every round fired punches it (its UI_Punch); a completed reload gives a bigger punch.
//   - Ability Ring (optional, testing, off by default: Show Ability Ring): when off, the ring image is simply whole in the
//     No Ability colour, a plain crosshair. When on, it takes the selected ability's colour and fills with its cooldown (empty just
//     used, whole when ready). Its Image Type must be Filled (Radial 360). Using the ability punches the ring (its
//     own UI_Punch) and drains it smoothly (a DOTween) instead of snapping empty; the cooldown then refills it.
// The outline and the fill are UI_Shadowed images, so their shadows follow and match.
//
// Setup: put this on the crosshair object under the HUD canvas (pivot in the middle); this object is what gets
// moved. Under it, a child with a UI_Punch (the punch moves that, so it never fights the placement) holding the
// Outline and Fill images. Set the Fill image's Image Type to Filled (Radial 360). Assign the plane's Weapon_Aim and
// Equip_Loadout, the plane's Plane_Input (for the cursor), the Fill's UI_Shadowed and the UI_Punch.
//
// GC: none per frame. One UpdateManager subscription (OnLateUpdate), made in Start after the camera's so the
// crosshair is placed from where the camera ends up this frame. Subscribes to the loadout's SlotChanged and the
// weapon's AmmoChanged events, and the belt's Used event. Each ability use starts one DOTween (allocates a little).
[DefaultExecutionOrder(100)]
public class UI_Crosshair : MonoBehaviour
{
    private enum Follow
    {
        Cursor,     // the mouse cursor; the shot point while a gamepad steers
        ShotPoint,  // where the bullets go (aim assist included)
        AimPoint    // where the nose points (no assist)
    }

    [Header("References")]
    [SerializeField] private Weapon_Aim aim;
    [SerializeField] private Equip_Loadout loadout;
    [Tooltip("For the cursor position and whether the mouse or a gamepad is steering.")]
    [SerializeField] private Plane_Input input;
    [Tooltip("The camera the world is seen through. Empty uses the main camera.")]
    [SerializeField] private Camera viewCamera;

    [Tooltip("The inner circle (UI_Shadowed on a Filled image): fills over the reload.")]
    [SerializeField] private UI_Shadowed fill;

    [Tooltip("Punched per round fired and on reload complete. Put it on a child of this object, not on this object.")]
    [SerializeField] private UI_Punch punch;

    [Header("Placement")]
    [Tooltip("Cursor: the mouse (the shot point while a gamepad steers). Shot Point: where the bullets go (assist included). Aim Point: where the nose points.")]
    [SerializeField] private Follow follow = Follow.Cursor;

    [Tooltip("Hide the system cursor while the crosshair is on (play mode only).")]
    [SerializeField] private bool hideCursor = true;

    [Tooltip("Keep the cursor inside the game window while the crosshair is on.")]
    [SerializeField] private bool confineCursor = false;

    [Tooltip("How quickly the crosshair catches up with its point, per second. 0 snaps.")]
    [SerializeField, Min(0f)] private float followSpeed = 0f;

    [Header("Punch")]
    [Tooltip("How hard each round punches, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float shotPunch = 1f;

    [Tooltip("How hard a completed reload punches, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float reloadPunch = 3f;

    [Header("Ability Ring")]
    [Tooltip("Off: the ring is a plain whole circle in the No Ability colour. On: it shows the selected ability's colour and cooldown.")]
    [SerializeField] private bool showAbilityRing;

    [Tooltip("Optional: an image (e.g. the Outline) tinted the selected ability's colour and filled with its cooldown. Set its Image Type to Filled, Radial 360.")]
    [SerializeField] private Image abilityRing;

    [Tooltip("The plane's ability belt, for the ring.")]
    [SerializeField] private Equip_AbilityBelt belt;

    [Tooltip("The ring's colour with no ability selected.")]
    [SerializeField] private Color noAbilityColour = Color.white;

    [Tooltip("Punched when the selected ability is used: a UI_Punch on the ring image.")]
    [SerializeField] private UI_Punch abilityRingPunch;

    [Tooltip("How hard using the ability punches the ring, as a share of its UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float abilityUsePunch = 1f;

    [Tooltip("Seconds the ring takes to drain from full to empty when the ability is used. 0 snaps.")]
    [Min(0f)] [SerializeField] private float drainDuration = 0.25f;

    [SerializeField] private Ease drainEase = Ease.OutQuad;

    [Header("Fill")]
    [Tooltip("The fill when not reloading: 0 empty, 1 full.")]
    [Range(0f, 1f)] [SerializeField] private float idleFill = 0f;

    private Canvas canvas;
    private RectTransform container;
    private RectTransform marker;
    private Camera canvasCamera;
    private Vector2 screen;
    private bool placed;
    private bool subscribed;

    private Weapon_IAmmo ammo;
    private int lastAmmo;
    private bool wasReloading;
    private float shownFill = -1f;
    private float shownRingFill = -1f;
    private Equip_Ability shownRingAbility;
    private bool ringShown;
    private float drain;
    private Tween drainTween;

    private void Awake()
    {
        marker = transform as RectTransform;
        canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            Debug.LogError($"{name}: UI_Crosshair must be under a Canvas.", this);
            enabled = false;
            return;
        }

        canvas = canvas.rootCanvas;
        container = (RectTransform)canvas.transform;
        canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private void Start()
    {
        if (viewCamera == null)
            viewCamera = Camera.main;

        if (aim == null)
            Debug.LogWarning($"{name}: No Weapon_Aim assigned, the crosshair won't move.", this);

        Subscribe();
    }

    private void OnEnable()
    {
        if (loadout != null)
        {
            loadout.SlotChanged += OnSlotChanged;
            Bind(loadout.Get(Equip_Slot.Weapon));
        }

        if (belt != null)
            belt.Used += OnAbilityUsed;

        // Start subscribes the first time (after the camera); later enables subscribe here
        if (placed)
            Subscribe();

        if (Application.isPlaying)
        {
            if (hideCursor)
                Cursor.visible = false;

            if (confineCursor)
                Cursor.lockState = CursorLockMode.Confined;
        }
    }

    private void OnDisable()
    {
        if (loadout != null)
            loadout.SlotChanged -= OnSlotChanged;

        Bind(null);

        if (belt != null)
            belt.Used -= OnAbilityUsed;

        drainTween?.Kill();
        drain = 0f;

        if (subscribed && UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;

        subscribed = false;

        // Give the system cursor back
        if (hideCursor)
            Cursor.visible = true;

        if (confineCursor)
            Cursor.lockState = CursorLockMode.None;
    }

    private void OnApplicationFocus(bool focused)
    {
        // Alt-tabbing back can bring the cursor back
        if (focused && hideCursor && isActiveAndEnabled)
            Cursor.visible = false;
    }

    private void Subscribe()
    {
        if (subscribed || UpdateManager.Instance == null)
            return;

        UpdateManager.Instance.OnLateUpdate += Tick;
        subscribed = true;
        placed = true;
    }

    private void OnSlotChanged(Equip_Slot slot)
    {
        if (slot == Equip_Slot.Weapon)
            Bind(loadout.Get(Equip_Slot.Weapon));
    }

    // Follows the main weapon's magazine (or none)
    private void Bind(Equip_Item item)
    {
        if (ammo != null)
            ammo.AmmoChanged -= OnAmmoChanged;

        ammo = item as Weapon_IAmmo;

        if (ammo != null)
        {
            ammo.AmmoChanged += OnAmmoChanged;
            lastAmmo = ammo.Ammo;
        }

        wasReloading = ammo != null && ammo.Reloading;
    }

    private void OnAmmoChanged()
    {
        bool reloading = ammo.Reloading;

        if (punch != null)
        {
            // Fewer rounds than before: a shot was fired
            if (ammo.Ammo < lastAmmo)
                punch.Punch(shotPunch);

            // A reload just finished
            if (wasReloading && !reloading)
                punch.Punch(reloadPunch);
        }

        lastAmmo = ammo.Ammo;
        wasReloading = reloading;
    }

    private void Tick()
    {
        ShowFill();
        ShowAbilityRing();

        float deltaTime = Time.unscaledDeltaTime;

        if (follow == Follow.Cursor && input != null && input.UsingPointer)
        {
            PlaceOnScreen(input.PointerPosition, deltaTime);
            return;
        }

        if (aim == null || viewCamera == null)
            return;

        Weapon_AimData data = aim.Data;
        Place(follow == Follow.AimPoint ? data.AimPoint : data.ShotPoint, deltaTime);
    }

    // The selected ability went off: punch the ring and drain it from full
    private void OnAbilityUsed(int index)
    {
        if (!showAbilityRing || abilityRing == null || index != belt.SelectedIndex)
            return;

        if (abilityRingPunch != null)
            abilityRingPunch.Punch(abilityUsePunch);

        drainTween?.Kill();

        if (drainDuration <= 0f)
        {
            drain = 0f;
            return;
        }

        drain = 1f;
        drainTween = DOTween.To(() => drain, value => drain = value, 0f, drainDuration).SetEase(drainEase);
    }

    // Colour when the selected ability changes, fill when its cooldown moves (or the drain, while it's higher)
    private void ShowAbilityRing()
    {
        if (!showAbilityRing)
        {
            if (abilityRing != null && !ringShown)
            {
                ringShown = true;
                abilityRing.color = noAbilityColour;
                abilityRing.fillAmount = 1f;
            }

            return;
        }

        if (abilityRing == null || belt == null)
            return;

        Equip_Ability ability = belt.SelectedAbility;

        if (!ringShown || ability != shownRingAbility)
        {
            ringShown = true;
            shownRingAbility = ability;
            abilityRing.color = ability != null ? ability.color : noAbilityColour;

            // A new ability shows its own cooldown straight away
            drainTween?.Kill();
            drain = 0f;
        }

        // Draining down while the cooldown fills up: the ring follows whichever is higher, so they meet smoothly
        float amount = ability != null ? Mathf.Max(drain, Mathf.Clamp01(ability.CooldownProgress)) : 1f;

        if (Mathf.Abs(amount - shownRingFill) < 0.001f)
            return;

        shownRingFill = amount;
        abilityRing.fillAmount = amount;
    }

    private void ShowFill()
    {
        if (fill == null)
            return;

        float amount = ammo != null && ammo.Reloading ? ammo.ReloadProgress : idleFill;

        if (Mathf.Abs(amount - shownFill) < 0.001f)
            return;

        shownFill = amount;
        fill.SetFill(amount);
    }

    // Moves over a world point (eased by follow speed, 0 snaps), hidden while the point is behind the camera
    private void Place(Vector3 world, float deltaTime)
    {
        Vector3 point = viewCamera.WorldToScreenPoint(world);

        if (point.z <= 0f)
        {
            screen = Vector2.zero;
            return;
        }

        PlaceOnScreen(point, deltaTime);
    }

    // Moves over a screen point (eased by follow speed, 0 snaps)
    private void PlaceOnScreen(Vector2 target, float deltaTime)
    {
        // The first frame jumps straight there
        screen = followSpeed > 0f && screen != Vector2.zero ? Core_Math.Damp(screen, target, followSpeed, deltaTime) : target;

        // Through world space, so the crosshair can sit anywhere under the canvas with any anchors
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(container, screen, canvasCamera, out Vector3 position))
            marker.position = position;
    }
}
