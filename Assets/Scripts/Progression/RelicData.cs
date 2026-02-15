using UnityEngine;

[CreateAssetMenu(menuName = "Game/Relic Data")]
public class RelicData : ScriptableObject
{
    [SerializeField] private string relicId;
    public string relicName;
    public Sprite icon;

    [Header("Passive Multipliers")]
    [Min(0.1f)] public float maxHpMultiplier = 1f;
    [Min(0.1f)] public float moveSpeedMultiplier = 1f;
    [Min(0.1f)] public float damageMultiplier = 1f;
    [Min(0.1f)] public float cooldownMultiplier = 1f;
    [Min(0.1f)] public float projectileSpeedMultiplier = 1f;
    [Min(0.1f)] public float critChanceMultiplier = 1f;
    [Min(0.1f)] public float critDamageMultiplier = 1f;

    public string RelicId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(relicId))
                return relicId.Trim();

            return name;
        }
    }
}
