using System.Collections.Generic;
using UnityEngine;

// Generates level-up choices.
// Responsibilities:
// - Random 3-card selection
// - Avoid duplicates
public class LevelUpGenerator : MonoBehaviour
{
    [SerializeField] private List<AbilityData> allAbilities = new List<AbilityData>(32);
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private int pickCount = 3;
    [SerializeField, Range(0f, 0.2f)] private float debuffOnlyRollChance = 0.05f;

    private readonly List<AbilityData> buffer = new List<AbilityData>(64);
    private readonly List<AbilityData> debuffBuffer = new List<AbilityData>(64);

    private void Awake()
    {
        if (abilityManager == null)
            abilityManager = Object.FindFirstObjectByType<AbilityManager>();
    }

    public List<AbilityData> GenerateChoices()
    {
        buffer.Clear();
        buffer.AddRange(allAbilities);

        if (abilityManager != null)
        {
            var owned = abilityManager.Abilities;
            for (int i = 0; i < owned.Count; i++)
                buffer.Remove(owned[i]);
        }

        debuffBuffer.Clear();
        for (int i = 0; i < buffer.Count; i++)
        {
            var ability = buffer[i];
            if (ability == null || string.IsNullOrWhiteSpace(ability.abilityName)) continue;

            if (ability.abilityName.IndexOf("DeBuff skill", System.StringComparison.OrdinalIgnoreCase) >= 0
                || ability.abilityName.IndexOf("Debuff skill", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                debuffBuffer.Add(ability);
            }
        }

        int count = Mathf.Clamp(pickCount, 0, buffer.Count);
        if (count <= 0) return new List<AbilityData>(0);

        if (debuffBuffer.Count >= count && Random.value <= debuffOnlyRollChance)
        {
            Shuffle(debuffBuffer);
            var debuffRoll = new List<AbilityData>(count);
            for (int i = 0; i < count; i++)
                debuffRoll.Add(debuffBuffer[i]);
            return debuffRoll;
        }

        Shuffle(buffer);
        var result = new List<AbilityData>(count);
        for (int i = 0; i < count; i++)
            result.Add(buffer[i]);

        return result;
    }

    private static void Shuffle(List<AbilityData> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
