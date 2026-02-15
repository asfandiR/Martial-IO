using UnityEngine;

// Handles HUD and menus.
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [SerializeField] private GameObject hudScreen;
    [SerializeField] private GameObject levelUpScreen;
    [SerializeField] private GameObject deathScreen;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnStateChanged -= HandleStateChanged;
    }

    private void Start()
    {
        SetActiveScreen(hud: true, levelUp: false, death: false);
    }

    private void HandleStateChanged(GameManager.GameState state)
    {
        switch (state)
        {
            case GameManager.GameState.LevelUp:
                SetActiveScreen(hud: false, levelUp: true, death: false);
                break;
            case GameManager.GameState.GameOver:
                SetActiveScreen(hud: false, levelUp: false, death: true);
                break;
            default:
                SetActiveScreen(hud: true, levelUp: false, death: false);
                break;
        }
    }

    public void ShowHUD()
    {
        SetActiveScreen(hud: true, levelUp: false, death: false);
    }

    public void ShowLevelUp()
    {
        SetActiveScreen(hud: false, levelUp: true, death: false);
    }

    public void ShowDeath()
    {
        SetActiveScreen(hud: false, levelUp: false, death: true);
    }

    private void SetActiveScreen(bool hud, bool levelUp, bool death)
    {
        if (hudScreen != null) hudScreen.SetActive(hud);
        if (levelUpScreen != null) levelUpScreen.SetActive(levelUp);
        if (deathScreen != null) deathScreen.SetActive(death);
    }
}
