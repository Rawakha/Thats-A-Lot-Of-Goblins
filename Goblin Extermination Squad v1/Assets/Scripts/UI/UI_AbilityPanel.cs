using System.Collections.Generic;
using UnityEngine;

// The HUD's ability panel: one row (UI_AbilitySlot) per ability on the plane's belt, in belt order, with the
// selected one highlighted. Rows are made from the template the first time they're needed and reused after that:
// when an ability leaves the belt its row is hidden, not destroyed. The rows sit under the template's parent, so a
// layout group there arranges them (tick Reverse Arrangement to stack the first ability at the bottom).
// Switching abilities plays Switch Sound and springs the rows; using one punches its row (see UI_AbilitySlot).
//
// Setup: put this on the Ability Panel, assign the plane's Equip_AbilityBelt and the Ability UI template (it's
// hidden at start and only copied).
//
// GC: instantiates a row the first time the belt grows past the rows made so far; rows' text builds a string when
// abilities or charges change. Nothing per frame. Subscribes to the belt's Changed, SelectionChanged and Used events,
// and one UpdateManager subscription (OnLateUpdate) for the cooldown fills (each icon only redraws when it changes).
public class UI_AbilityPanel : MonoBehaviour
{
    [SerializeField] private Equip_AbilityBelt belt;

    [Tooltip("The row to copy for each ability. (Hidden at start).")]
    [SerializeField] private UI_AbilitySlot template;

    [Tooltip("Played when the selected ability changes (a click). Not on the first ability at the start.")]
    [SerializeField] private SoundData switchSound;

    private readonly List<UI_AbilitySlot> rows = new List<UI_AbilitySlot>(8);
    private Equip_Ability lastAbility;

    private void Awake()
    {
        if (template != null)
            template.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (belt == null || template == null)
        {
            Debug.LogWarning($"{name}: UI_AbilityPanel needs an Equip_AbilityBelt and a template row.", this);
            return;
        }

        belt.Changed += Refresh;
        belt.SelectionChanged += OnSelectionChanged;
        belt.Used += OnUsed;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate += ShowCooldowns;

        lastAbility = belt.SelectedAbility;
        Refresh();
    }

    private void OnDisable()
    {
        if (belt == null)
            return;

        belt.Changed -= Refresh;
        belt.SelectionChanged -= OnSelectionChanged;
        belt.Used -= OnUsed;

        if (UpdateManager.Instance != null)
            UpdateManager.Instance.OnLateUpdate -= ShowCooldowns;
    }

    // Every row's icon fill, selected or not
    private void ShowCooldowns()
    {
        int count = Mathf.Min(belt.Count, rows.Count);

        for (int i = 0; i < count; i++)
            rows[i].ShowCooldown(belt.AbilityAt(i).CooldownProgress);
    }

    private void OnUsed(int index)
    {
        if (index >= 0 && index < rows.Count && rows[index].gameObject.activeSelf)
            rows[index].PlayUse();
    }

    // Rows to match the belt: names, charges and colours, then which one is selected
    private void Refresh()
    {
        int count = belt.Count;

        while (rows.Count < count)
        {
            UI_AbilitySlot row = Instantiate(template, template.transform.parent, false);
            row.name = template.name + " " + rows.Count;
            rows.Add(row);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            bool used = i < count;

            if (rows[i].gameObject.activeSelf != used)
                rows[i].gameObject.SetActive(used);

            if (used)
                rows[i].Show(belt.AbilityAt(i), belt.ChargesAt(i), belt.IsInfinite(i));
        }

        ShowSelection(false);
    }

    // A real switch: click and spring. Not the very first ability, and not a reshuffle that kept the same one.
    private void OnSelectionChanged()
    {
        Equip_Ability selected = belt.SelectedAbility;
        bool switched = selected != null && lastAbility != null && selected != lastAbility;

        if (switched)
            Utilities.TryPlaySound(switchSound, belt.transform.position);

        lastAbility = selected;
        ShowSelection(true);
    }

    private void ShowSelection(bool animate)
    {
        int count = Mathf.Min(belt.Count, rows.Count);

        for (int i = 0; i < count; i++)
            rows[i].SetSelected(i == belt.SelectedIndex, animate);
    }
}
