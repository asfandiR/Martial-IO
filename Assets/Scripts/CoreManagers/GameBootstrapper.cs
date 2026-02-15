using UnityEngine;

// Creates and wires core game systems at startup.
// Responsibilities:
// - Ensure singletons/managers exist
// - Load initial scene/state
// - Handle boot order and dependencies
public class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private bool startInGameplay = true;

    private void Awake()
    {
        EnsureSingleton<GameManager>("GameManager");
        EnsureSingleton<ObjectPooler>("ObjectPooler");
        EnsureSingleton<LevelManager>("LevelManager");
    }

    private void Start()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        if (startInGameplay)
            gm.StartGameplay();
        else
            gm.BackToMenu();
    }

    private static void EnsureSingleton<T>(string name) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null) return;

        var go = new GameObject(name);
        go.AddComponent<T>();
    }
}
