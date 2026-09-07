using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TowerShop : MonoBehaviour
{
    [SerializeField] private TowerDefinition[] towerDefinitions;

    [Header("Crate Spawning")]
    [SerializeField] private TowerGrabbable cratePrefab;
    [SerializeField] private float spawnHeight = 7f;

    [Header("UI")]
    [SerializeField] private HorizontalLayoutGroup buttonParent;
    [SerializeField] private CustomButton buttonPrefab;

    private void Awake()
    {
        CreateButtons();
    }

    private void CreateButtons()
    {
        if (towerDefinitions == null || towerDefinitions.Length == 0)
        {
            return;
        }

        for (int i = 0; i < towerDefinitions.Length; i++)
        {
            TowerDefinition definition = towerDefinitions[i];
            if (definition == null) continue;

            CustomButton button = Instantiate(buttonPrefab, buttonParent.transform);
            // Change the button icon and settings etc
            button.OnClicked.AddListener(() => TryBuy(button, definition));

            button.gameObject.SetActive(true);
        }
    }

    public bool TryBuy(CustomButton button, TowerDefinition def)
    {
        if (button == null || def == null || !RunManager.Instance.TrySpendGold(def.cost))
            return false;

        Vector3 screenPos = button.Rect.position;
        
        if (PlayerInput.Instance.TryGetGroundPoint(screenPos, out Vector3 groundPoint))
        {
            Vector3 spawnPoint = new Vector3(groundPoint.x, spawnHeight, groundPoint.z);
            TowerGrabbable crate = Instantiate(cratePrefab, spawnPoint, Quaternion.identity);
            // Configure crate to store the Tower Definition
            PlayerHand.Instance.ForceGrab(crate);
            return true;
        }

        return false;
    }
}