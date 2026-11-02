using UnityEngine;
using UnityEngine.UI;

// The weapon panel on the HUD: the equipped weapon's name, the rounds left in its magazine and the reload bar. Name
// and ammo are UI_Shadowed texts, so each shadow always shows and follows exactly what its text does.
//
// It follows the loadout's Weapon slot: equip something else and the name and ammo switch to it. Weapons with a
// magazine implement Weapon_IAmmo (the machine gun does); one without shows No Ammo Text. Weapon dots come later.
//
// Shot punch: each round fired punches the ammo text through its UI_Punch (scale, rotation and position, all tuned
// there); its shadow follows. Kicks stack, so a fast gun keeps the counter buzzing; the punch's Max values cap it.
// Reload: the reload bar fills from empty to full over the reload; when it completes the counter gets a bigger
// punch (Reload Punch) and Reload Sound plays.
//
// Setup: put this on the Weapon Panel, assign the plane's Equip_Loadout, the Weapon Name and Ammo Count texts (each
// with a UI_Shadowed pointing at its shadow), the UI_Punch on Ammo Count and the Reload Bar slider.
//
// GC: none per shot (TMP's SetText with a number doesn't allocate). Subscribes to the loadout's SlotChanged and the
// weapon's AmmoChanged events; one UpdateManager subscription (OnLateUpdate) only while reloading, for the bar. The
// punch springs are UI_Punch's.
public class UI_WeaponPanel : MonoBehaviour
{
    [SerializeField] private Equip_Loadout loadout;
    [SerializeField] private Equip_Slot slot = Equip_Slot.Weapon;

    [Tooltip("Shows the weapon's name (UI_Shadowed on the text).")]
    [SerializeField] private UI_Shadowed nameText;

    [Tooltip("Shows the rounds left (UI_Shadowed on the text).")]
    [SerializeField] private UI_Shadowed ammoText;

    [Tooltip("Punched once per round fired and when a reload completes: the UI_Punch on Ammo Count.")]
    [SerializeField] private UI_Punch ammoPunch;

    [Tooltip("How hard each round punches, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float shotPunch = 1f;

    [Tooltip("Shown as the ammo for a weapon without a magazine (or with a bottomless one).")]
    [SerializeField] private string noAmmoText = "-";

    [Header("Reload")]
    [Tooltip("Fills from empty to full over the reload. Its Min and Max should be 0 and 1.")]
    [SerializeField] private Slider reloadBar;

    [Tooltip("Hide the reload bar when not reloading. Off: it sits full.")]
    [SerializeField] private bool hideBarWhenLoaded = true;

    [Tooltip("How hard the ammo counter punches when a reload completes, as a share of the UI_Punch's own kick.")]
    [Min(0f)] [SerializeField] private float reloadPunch = 3f;

    [Tooltip("Played when a reload completes. Empty is silent; make it 2D.")]
    [SerializeField] private SoundData reloadSound;

    private Equip_Item weapon;
    private Weapon_IAmmo ammo;
    private int lastAmmo;
    private bool wasReloading;
    private bool ticking;

    private void OnEnable()
    {
        if (loadout == null)
        {
            Debug.LogWarning($"{name}: UI_WeaponPanel has no Equip_Loadout assigned.", this);
            return;
        }

        loadout.SlotChanged += OnSlotChanged;
        Bind(loadout.Get(slot));
    }

    private void OnDisable()
    {
        if (loadout != null)
            loadout.SlotChanged -= OnSlotChanged;

        Bind(null);
        StopTicking();
    }

    private void OnSlotChanged(Equip_Slot changed)
    {
        if (changed == slot)
            Bind(loadout.Get(slot));
    }

    // Follows a new weapon (or none): its name now, and its ammo from now on
    private void Bind(Equip_Item item)
    {
        if (ammo != null)
            ammo.AmmoChanged -= OnAmmoChanged;

        weapon = item;
        ammo = item as Weapon_IAmmo;

        if (ammo != null)
        {
            ammo.AmmoChanged += OnAmmoChanged;
            lastAmmo = ammo.Ammo;
        }

        wasReloading = ammo != null && ammo.Reloading;

        if (nameText != null)
            nameText.SetText(weapon != null ? weapon.DisplayName : "");

        ShowAmmo();
        ShowReload();
    }

    private void OnAmmoChanged()
    {
        bool reloading = ammo.Reloading;

        // Fewer rounds than before: a shot was fired
        if (ammo.Ammo < lastAmmo && ammoPunch != null)
            ammoPunch.Punch(shotPunch);

        // A reload just finished: the big punch
        if (wasReloading && !reloading)
        {
            if (ammoPunch != null)
                ammoPunch.Punch(reloadPunch);

            Utilities.TryPlaySound(reloadSound, loadout.transform.position);
        }

        lastAmmo = ammo.Ammo;
        wasReloading = reloading;
        ShowAmmo();
        ShowReload();
    }

    private void ShowAmmo()
    {
        if (ammoText == null)
            return;

        if (ammo != null && ammo.MagazineSize > 0)
            ammoText.SetText("{0}", ammo.Ammo);
        else
            ammoText.SetText(noAmmoText);
    }

    // The bar's state, and its per-frame fill only while a reload runs
    private void ShowReload()
    {
        bool reloading = ammo != null && ammo.Reloading;

        if (reloading)
            StartTicking();
        else
            StopTicking();

        if (reloadBar == null)
            return;

        bool visible = reloading || !hideBarWhenLoaded;

        if (reloadBar.gameObject.activeSelf != visible)
            reloadBar.gameObject.SetActive(visible);

        reloadBar.SetValueWithoutNotify(reloading ? ammo.ReloadProgress : 1f);
    }

    private void Tick()
    {
        if (reloadBar != null && ammo != null)
            reloadBar.SetValueWithoutNotify(ammo.ReloadProgress);
    }

    private void StartTicking()
    {
        if (ticking || UpdateManager.Instance == null)
            return;

        ticking = true;
        UpdateManager.Instance.OnLateUpdate += Tick;
    }

    private void StopTicking()
    {
        if (!ticking)
            return;

        ticking = false;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= Tick;
    }
}
