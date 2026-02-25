using UnityEngine;

// Auto-releases temporary VFX either back to ObjectPooler or by Destroy.
[DisallowMultipleComponent]
public class TimedAutoRelease : MonoBehaviour
{
    [SerializeField] private float fallbackLifetimeSeconds = 1.5f;

    private float timer;

    private void OnEnable()
    {
        timer = Mathf.Max(0.05f, fallbackLifetimeSeconds);
    }

    public void Arm(float lifetimeSeconds)
    {
        timer = Mathf.Max(0.05f, lifetimeSeconds);
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0f)
            return;

        Release();
    }

    private void Release()
    {
        if (!gameObject.activeInHierarchy)
            return;

        bool hasPoolMeta = GetComponent("PooledObject") != null;
        if (hasPoolMeta && ObjectPooler.Instance != null)
        {
            ObjectPooler.Instance.ReturnToPool(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
