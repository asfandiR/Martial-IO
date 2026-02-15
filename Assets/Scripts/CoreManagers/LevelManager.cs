using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Handles level runtime logic.
// Responsibilities:
// - Level timer
// - Difficulty scaling over time
// - Expose current level pace to other systems
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField] private float difficultyStepInterval = 30f;
    [SerializeField] private float difficultyStepMultiplier = 0.1f;

    public float ElapsedTime { get; private set; }
    public int DifficultyStep { get; private set; }
    public float DifficultyMultiplier { get; private set; } = 1f;

    public event Action<int, float> OnDifficultyScaled;

    private float nextDifficultyTime;
    private GameManager gameManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        gameManager = GameManager.Instance;
        nextDifficultyTime = difficultyStepInterval;
    }

    private void Update()
    {
        if (gameManager != null && gameManager.CurrentState != GameManager.GameState.Gameplay)
            return;

        ElapsedTime += Time.deltaTime;
        if (ElapsedTime >= nextDifficultyTime)
        {
            DifficultyStep += 1;
            DifficultyMultiplier += difficultyStepMultiplier;
            nextDifficultyTime += difficultyStepInterval;
            OnDifficultyScaled?.Invoke(DifficultyStep, DifficultyMultiplier);
        }
    }

    public void ResetLevel()
    {
        ElapsedTime = 0f;
        DifficultyStep = 0;
        DifficultyMultiplier = 1f;
        nextDifficultyTime = difficultyStepInterval;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            ResetLevel();
    }
}
