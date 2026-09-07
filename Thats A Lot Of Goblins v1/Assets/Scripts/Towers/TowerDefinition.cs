using UnityEngine;

[CreateAssetMenu(fileName = "New Tower Definition", menuName = "TD/Tower Definition")]
public class TowerDefinition : ScriptableObject
{
    public Tower prefab;
    public int cost;
    public Vector2Int size;

    [Header("UI")]
    public string displayName;
    public Sprite icon;
}