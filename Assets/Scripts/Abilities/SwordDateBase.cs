using System.Collections.Generic;
using UnityEngine;

// Stores sword sprites and optional color cycles for sword skin progression.
[CreateAssetMenu(menuName = "Game/Sword DateBase")]
public class SwordDateBase : ScriptableObject
{
    [SerializeField] private List<Sprite> swordSprites = new List<Sprite>(400);
    [SerializeField] private List<Color> cycleColors = new List<Color>(8);

    public int SwordCount => swordSprites != null ? swordSprites.Count : 0;

    public Sprite GetSwordSprite(int index)
    {
        if (swordSprites == null || swordSprites.Count == 0) return null;
        if (index < 0 || index >= swordSprites.Count) return null;
        return swordSprites[index];
    }

    public bool TryGetCycleColor(int cycle, out Color color)
    {
        color = Color.white;
        if (cycle <= 0) return true;
        if (cycleColors == null || cycleColors.Count == 0) return false;

        int i = (cycle - 1) % cycleColors.Count;
        color = cycleColors[i];
        return true;
    }
}
