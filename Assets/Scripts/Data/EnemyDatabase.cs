using System.Collections.Generic;
using UnityEngine;

// ScriptableObject-based monster stats database.
[CreateAssetMenu(menuName = "Game/Enemy Database")]
public class EnemyDatabase : ScriptableObject
{
    public List<EnemyData> enemies = new List<EnemyData>(32);

    public List<EnemyData> GetCandidates(int difficultyStep)
    {
        var result = new List<EnemyData>(enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (e == null) continue;
            if (difficultyStep >= e.minDifficultyStep)
                result.Add(e);
        }
        return result;
    }
}
