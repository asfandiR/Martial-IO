using UnityEngine;

[CreateAssetMenu(menuName = "Game/Effector Data")]
public class EffectorSO : ScriptableObject
{
    public enum BoostedStat
    {
        X2Damage,
        HalfCooldown,
        X2CritChance,
        Healing,
        Shielding
    }

    public enum EffectorRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [SerializeField] private string effectorId;
    public string effectorName;
    public Sprite icon;
    [TextArea] public string description;
    public BoostedStat boostedStat = BoostedStat.X2Damage;
    [Range(1f, 15f)] public float boostPercent = 10f;
    public EffectorRarity rarity = EffectorRarity.Common;

    public string EffectorId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(effectorId))
                return effectorId.Trim();

            return name;
        }
    }

    public float DurationSeconds
    {
        get
        {
            switch (rarity)
            {
                case EffectorRarity.Rare:
                    return 30f;
                case EffectorRarity.Epic:
                    return 60f;
                case EffectorRarity.Legendary:
                    return 120f;
                default:
                    return 15f;
            }
        }
    }
}
