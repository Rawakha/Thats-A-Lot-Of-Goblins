using DG.Tweening;
using UnityEngine;

// One ability's row on the HUD's ability panel: its coloured icon and "NAME [charges]" (no count for an infinite
// ability). Selected, it's fully visible, a little bigger and shows the full icon; otherwise it fades, drops back
// to normal size, shows the empty icon and dims its icon and name (UI_Style; shadows stay as they are). The icon is
// always tinted the ability's colour.
// Switching springs instead of snapping: the row (its UI_Punch) springs to its new size, popping past it when
// selected and squashing when left behind. On selection the icon and the name each get their own punch (a UI_Punch
// on each), the name a moment after the icon, so they land one after the other instead of as one block. Using the
// ability plays the same punch.
// UI_AbilityPanel makes these from a template and tells them what to show.
//
// Setup: put this on the Ability UI template and assign its Canvas Group, the Icon and the Name (each a UI_Shadowed on
// the main image / text, pointing at its shadow), the Full / Empty icon sprites, and a UI_Punch on the row, the icon
// and the name.
//
// GC: setting the text builds a string, only when the ability or its charges change (not per frame); each use
// starts one small DOTween for the drain. No
// subscriptions of its own (UI_Punch ticks only while it springs).
public class UI_AbilitySlot : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("The icon (UI_Shadowed on the icon image): tinted the ability's colour, its shadow follows.")]
    [SerializeField] private UI_Shadowed icon;

    [Tooltip("Shows \"NAME [charges]\" (UI_Shadowed on the text): its shadow follows.")]
    [SerializeField] private UI_Shadowed label;

    [Header("Icon")]
    [Tooltip("The icon while this ability is selected.")]
    [SerializeField] private Sprite fullIcon;

    [Tooltip("The icon while another ability is selected.")]
    [SerializeField] private Sprite emptyIcon;

    [Header("Springs")]
    [Tooltip("Springs the whole row between sizes. Empty just sets the scale.")]
    [SerializeField] private UI_Punch punch;

    [Tooltip("The icon's own punch on selection (UI_Punch on the icon image).")]
    [SerializeField] private UI_Punch iconPunch;

    [Tooltip("The name's own punch on selection (UI_Punch on the name text).")]
    [SerializeField] private UI_Punch labelPunch;

    [Header("Selected")]
    [Range(0f, 1f)] [SerializeField] private float selectedAlpha = 1f;

    [Tooltip("Size while selected, as a multiple of the row's own scale.")]
    [Min(0f)] [SerializeField] private float selectedScale = 1.05f;

    [Tooltip("Scale speed the whole row pops with as it becomes selected, on top of springing to Selected Scale.")]
    [Min(0f)] [SerializeField] private float rowPop = 1.5f;

    [Tooltip("The icon's punch on selection, as a share of its UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float iconPunchStrength = 1f;

    [Tooltip("The name's punch on selection, as a share of its UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float labelPunchStrength = 1f;

    [Tooltip("How hard using the ability punches, as a share of the switch punch (row pop, icon and name). 1 is the same as switching.")]
    [Min(0f)] [SerializeField] private float usePunch = 1f;

    [Tooltip("Seconds after the icon's punch that the name's lands.")]
    [Min(0f)] [SerializeField] private float labelDelay = 0.06f;

    [Header("Cooldown")]
    [Tooltip("Seconds the icon takes to drain from full to empty when the ability is used. 0 snaps.")]
    [Min(0f)] [SerializeField] private float drainDuration = 0.25f;

    [SerializeField] private Ease drainEase = Ease.OutQuad;

    [Header("Not Selected")]
    [Range(0f, 1f)] [SerializeField] private float unselectedAlpha = 0.5f;

    [Tooltip("Size while not selected, as a multiple of the row's own scale.")]
    [Min(0f)] [SerializeField] private float unselectedScale = 1f;

    [Tooltip("Squash when another ability takes over: scale speed pushed down, on top of springing to the smaller size.")]
    [Min(0f)] [SerializeField] private float deselectSquash = 1.5f;

    private bool shownSelected;
    private bool shownOnce;

    private float shownCooldown = -1f;
    private float drain;
    private Tween drainTween;

    private void OnDisable()
    {
        drainTween?.Kill();
        drain = 0f;
    }

    // The icon fills with the cooldown: empty just used, whole when ready. Set the icon Image's Image Type to Filled
    // (Radial 360) in its inspector; its UI_Shadowed's Copy Fill decides whether the shadow fills too.
    public void ShowCooldown(float progress)
    {
        // Draining down while the cooldown fills up: whichever is higher, so they meet smoothly
        progress = Mathf.Max(drain, Mathf.Clamp01(progress));

        if (icon == null || Mathf.Abs(progress - shownCooldown) < 0.001f)
            return;

        shownCooldown = progress;
        icon.SetFill(progress);
    }

    public void Show(Equip_Ability ability, int charges, bool infinite)
    {
        if (icon != null)
            icon.SetColor(ability.color);

        if (label != null)
            label.SetText(infinite ? ability.DisplayName : ability.DisplayName + " [" + charges + "]");
    }

    // The selection punch: the row pops, the icon punches, the name lands a moment later. Played on switching to this
    // ability and on using it.
    public void Punch(float strength = 1f)
    {
        if (punch != null)
            punch.KickScale(rowPop * strength);

        if (iconPunch != null)
            iconPunch.Punch(iconPunchStrength * strength);

        if (labelPunch != null)
            labelPunch.Punch(labelPunchStrength * strength, labelDelay);
    }

    // The punch for using the ability: the switch punch times Use Punch
    public void PunchUse() => Punch(usePunch);

    // The ability went off: the use punch, and the icon drains from full (a DOTween) instead of snapping empty
    public void PlayUse()
    {
        PunchUse();
        drainTween?.Kill();

        if (drainDuration <= 0f)
        {
            drain = 0f;
            return;
        }

        drain = 1f;
        drainTween = DOTween.To(() => drain, value => drain = value, 0f, drainDuration).SetEase(drainEase);
    }

    // Animate: spring and punch on a real switch; off (first show, refreshes) snaps straight to the state
    public void SetSelected(bool selected, bool animate)
    {
        bool first = !shownOnce;
        bool switched = !first && selected != shownSelected;
        shownSelected = selected;
        shownOnce = true;

        if (canvasGroup != null)
            canvasGroup.alpha = selected ? selectedAlpha : unselectedAlpha;

        float size = selected ? selectedScale : unselectedScale;

        // Same state as before (a charges refresh): leave any spring that's playing alone
        if (punch != null)
        {
            if (first || (switched && !animate))
            {
                punch.SetTargetScale(size, true);
            }
            else if (switched)
            {
                punch.SetTargetScale(size);

                if (!selected)
                    punch.KickScale(-deselectSquash);
            }
        }
        else
        {
            transform.localScale = Vector3.one * size;
        }

        if (switched && animate && selected)
            Punch();

        if (icon != null)
        {
            icon.SetSprite(selected ? fullIcon : emptyIcon);
            icon.SetDimmed(!selected);
        }

        if (label != null)
            label.SetDimmed(!selected);
    }
}
