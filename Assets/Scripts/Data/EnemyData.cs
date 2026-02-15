using UnityEngine;

// ScriptableObject for a single enemy type.
[CreateAssetMenu(menuName = "Game/Enemy Data")]
public class EnemyData : ScriptableObject
{
    public string enemyId;
    public GameObject prefab;
    public float baseHp = 10f;
    public float baseSpeed = 2f;
    public float baseDamage = 1f;
    public int weight = 1;
    public int minDifficultyStep = 0;
}
