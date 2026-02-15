using System;
using System.Collections;
using UnityEngine;

// Central game state machine.
// States: Menu, Gameplay, Pause, GameOver, LevelUp
// Responsibilities:
// - Track current state
// - Broadcast state changes
// - Gate input/time based on state
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Menu,
        Gameplay,
        Pause,
        GameOver,
        LevelUp
    }

    public static GameManager Instance { get; private set; }

    [Header("Time Scale")]
    [SerializeField, Range(0f, 0.2f)] private float pausedTimeScale = 0f;
    [SerializeField, Range(0.01f, 0.5f)] private float smoothStopDuration = 0.12f;
    [SerializeField, Range(0.01f, 0.5f)] private float smoothResumeDuration = 0.1f;

    public GameState CurrentState { get; private set; } = GameState.Menu;
    public event Action<GameState> OnStateChanged;

    private Coroutine timeScaleRoutine;
    private const float BaseFixedDeltaTime = 0.02f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        ApplyTimeScale(CurrentState, immediate: true);
        OnStateChanged?.Invoke(CurrentState);
    }

    public void SetState(GameState newState)
    {
        if (CurrentState == newState) return;
        CurrentState = newState;
        ApplyTimeScale(newState);
        OnStateChanged?.Invoke(newState);
    }

    private void ApplyTimeScale(GameState state, bool immediate = false)
    {
        float target = state == GameState.Pause || state == GameState.GameOver || state == GameState.LevelUp
            ? Mathf.Clamp01(pausedTimeScale)
            : 1f;

        float duration = target < Time.timeScale ? smoothStopDuration : smoothResumeDuration;

        if (immediate || duration <= 0.001f)
        {
            SetTimeScaleInstant(target);
            return;
        }

        if (timeScaleRoutine != null)
            StopCoroutine(timeScaleRoutine);

        timeScaleRoutine = StartCoroutine(SmoothTimeScaleRoutine(target, duration));
    }

    private IEnumerator SmoothTimeScaleRoutine(float target, float duration)
    {
        float start = Time.timeScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float current = Mathf.Lerp(start, target, t);
            SetTimeScaleInstant(current);
            yield return null;
        }

        SetTimeScaleInstant(target);
        timeScaleRoutine = null;
    }

    private static void SetTimeScaleInstant(float value)
    {
        float clamped = Mathf.Clamp01(value);
        Time.timeScale = clamped;
        Time.fixedDeltaTime = BaseFixedDeltaTime * clamped;
    }

    public void StartGameplay()
    {
        SetState(GameState.Gameplay);
    }

    public void Pause()
    {
        SetState(GameState.Pause);
    }

    public void Resume()
    {
        SetState(GameState.Gameplay);
    }

    public void GameOver()
    {
        SetState(GameState.GameOver);
    }

    public void LevelUp()
    {
        SetState(GameState.LevelUp);
    }

    public void BackToMenu()
    {
        SetState(GameState.Menu);
    }
}
