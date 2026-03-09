using UnityEngine;

// Handles HUD and menus.
// Integrates with UIPanelManager to manage system UI panels.
public class UIManager : MonoBehaviour
{
    private const string HudPanelId = "HUDPanel";
    private const string LevelUpPanelId = "LevelUpPanel";
    private const string DeathPanelId = "DeathPanel";

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

    private void Start()
    {
        // Register system panels with the manager
        if (UIPanelManager.Instance != null)
        {
            if (hudScreen != null)
                UIPanelManager.Instance.RegisterPanel(HudPanelId, hudScreen, UIPanelManager.PanelType.System);
            if (levelUpScreen != null)
                UIPanelManager.Instance.RegisterPanel(LevelUpPanelId, levelUpScreen, UIPanelManager.PanelType.System);
            if (deathScreen != null)
                UIPanelManager.Instance.RegisterPanel(DeathPanelId, deathScreen, UIPanelManager.PanelType.System);
        }
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
