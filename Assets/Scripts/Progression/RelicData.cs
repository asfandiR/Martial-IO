using UnityEngine;

[CreateAssetMenu(menuName = "Game/Relic Data")]
public class RelicData : ScriptableObject
{
    public enum RelicStatType
    {
        MaxHp,
        SwordSpeed,
        Damage,
        Cooldown,
        ProjectileSpeed,
        CritChance,
        CritDamage
    }

    [SerializeField] private string relicId;
    public string relicName;
    public Sprite icon;
    public RelicStatType boostedStat = RelicStatType.Damage;

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
