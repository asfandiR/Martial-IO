using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Handles run score, best score persistence, and Play Again button on death screen.
public class RunScoreUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text hudScoreText; // Ссылка на текст в HUD (во время игры)
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private Button playAgainButton;

    [Header("Tracking")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private HealthSystem playerHealth;
    [SerializeField] private bool useSurvivalTimeAsScore = true;

    private float survivalTime;
    private int bonusScore;
    private int lastShownScore = -1;
    private bool runFinished;
    private SaveSystem saveSystem;

    public int CurrentScore => Mathf.Max(0, Mathf.FloorToInt(survivalTime) + bonusScore);
    public int BestScore
    {
        get
        {
            ResolveSaveSystem();
            return saveSystem != null ? saveSystem.BestScore : 0;
        }
    }

    private void Awake()
    {
        ResolveSaveSystem();
        ResolvePlayerRefs();

        if (playAgainButton != null)
        {
            playAgainButton.onClick.RemoveListener(PlayAgain);
            playAgainButton.onClick.AddListener(PlayAgain);
        }

        UpdateBestScoreText(BestScore);
        UpdateScoreText(0);
    }

    private void OnEnable()
    {
        ResolvePlayerRefs();
        if (playerHealth != null)
            playerHealth.OnDeath += HandlePlayerDeath;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnDeath -= HandlePlayerDeath;

        if (playAgainButton != null)
            playAgainButton.onClick.RemoveListener(PlayAgain);
    }

    private void Update()
    {
        if (runFinished) return;

        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Gameplay)
            return;

        if (useSurvivalTimeAsScore)
            survivalTime += Time.deltaTime;

        int score = CurrentScore;
        if (score != lastShownScore)
            UpdateScoreText(score);
    }

    public void AddScore(int value)
    {
        if (value <= 0 || runFinished) return;

        bonusScore += value;
        int score = CurrentScore;
        if (score != lastShownScore)
            UpdateScoreText(score);
    }

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(sceneIndex);

        if (GameManager.Instance != null)
            GameManager.Instance.StartGameplay();
    }

    private void HandlePlayerDeath()
    {
        if (runFinished) return;
        runFinished = true;

        int finalScore = CurrentScore;
        UpdateScoreText(finalScore);

        int best = BestScore;
        if (finalScore > best)
        {
            ResolveSaveSystem();
            saveSystem?.TrySetBestScore(finalScore);
            best = finalScore;
        }

        UpdateBestScoreText(best);

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }

    private void ResolvePlayerRefs()
    {
        if (playerController == null)
            playerController = Object.FindFirstObjectByType<PlayerController>();

        if (playerHealth == null && playerController != null)
            playerHealth = playerController.GetComponent<HealthSystem>();
    }

    private void UpdateScoreText(int score)
    {
        lastShownScore = score;
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
            
        if (hudScoreText != null)
            hudScoreText.text = $"{score}";
    }

    private void UpdateBestScoreText(int best)
    {
        if (bestScoreText != null)
            bestScoreText.text = $"Best: {best}";
    }

    private void ResolveSaveSystem()
    {
        if (saveSystem == null)
            saveSystem = SaveSystem.Instance ?? Object.FindFirstObjectByType<SaveSystem>();
    }
}
