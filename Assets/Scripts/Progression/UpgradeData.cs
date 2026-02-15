using UnityEngine;

// ScriptableObject for permanent upgrades.
[CreateAssetMenu(menuName = "Game/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    public string upgradeId;
    public int cost = 50;
    public string displayName;
}
