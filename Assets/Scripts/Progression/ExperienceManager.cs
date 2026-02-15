using System;
using UnityEngine;

// XP collection and level-up flow.
// Responsibilities:
// - Track XP
// - Trigger level-ups
public class ExperienceManager : MonoBehaviour
{
    [SerializeField] private int startingLevel = 1;
    [SerializeField] private int baseXpToLevel = 10;
    [SerializeField] private int xpGrowthPerLevel = 5;
    [SerializeField] private bool pauseGameOnLevelUp = true;

    public int CurrentLevel { get; private set; }
    public int CurrentXp { get; private set; }
    public int XpToNext { get; private set; }

    public event Action<int> OnLevelUp;
    public event Action<int, int> OnXpChanged;
    private PlayerController playerController;
    private HealthSystem playerHealth;

    private void Awake()
    {
        CurrentLevel = Mathf.Max(1, startingLevel);
        XpToNext = CalculateXpToNext(CurrentLevel);
        CurrentXp = 0;
    }

    private void Start()
    {
        TryBindPlayer();
        OnXpChanged?.Invoke(CurrentXp, XpToNext);
    }

    private void OnDisable()
    {
        if (playerController != null)
            playerController.OnCollectXp -= AddXp;
    }

    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        CurrentXp += amount;

        while (CurrentXp >= XpToNext)
        {
            CurrentXp -= XpToNext;
            CurrentLevel += 1;
            XpToNext = CalculateXpToNext(CurrentLevel);
            RestorePlayerHealthOnLevelUp();
            OnLevelUp?.Invoke(CurrentLevel);

            if (pauseGameOnLevelUp && GameManager.Instance != null)
                GameManager.Instance.LevelUp();
        }

        OnXpChanged?.Invoke(CurrentXp, XpToNext);
    }

    public void ResetProgress()
    {
        CurrentLevel = Mathf.Max(1, startingLevel);
        CurrentXp = 0;
        XpToNext = CalculateXpToNext(CurrentLevel);
        OnXpChanged?.Invoke(CurrentXp, XpToNext);
    }

    private int CalculateXpToNext(int level)
    {
        return Mathf.Max(1, baseXpToLevel + (level - 1) * xpGrowthPerLevel);
    }

    private void TryBindPlayer()
    {
        if (playerController != null) return;

        playerController = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (playerController != null)
        {
            playerController.OnCollectXp += AddXp;
            playerHealth = playerController.GetComponent<HealthSystem>();
        }
    }

    private void RestorePlayerHealthOnLevelUp()
    {
        if (playerHealth == null)
            TryBindPlayer();
        if (playerHealth == null) return;

        // Full restore on level up.
        playerHealth.Heal(playerHealth.MaxHp);
    }
}
