using UnityEngine;
using System.Collections;

// Auto-releases temporary VFX either back to ObjectPooler or by Destroy.
[DisallowMultipleComponent]
public class TimedAutoRelease : MonoBehaviour
{
    [SerializeField] private float fallbackLifetimeSeconds = 1.5f;

    private Coroutine releaseRoutine;

    private void OnEnable()
    {
        StartReleaseCountdown(fallbackLifetimeSeconds);
    }

    private void OnDisable()
    {
        if (releaseRoutine != null)
        {
            StopCoroutine(releaseRoutine);
            releaseRoutine = null;
        }
    }

    public void Arm(float lifetimeSeconds)
    {
        StartReleaseCountdown(lifetimeSeconds);
    }

    private void StartReleaseCountdown(float lifetimeSeconds)
    {
        if (releaseRoutine != null)
            StopCoroutine(releaseRoutine);

        float delay = Mathf.Max(0.05f, lifetimeSeconds);
        releaseRoutine = StartCoroutine(ReleaseAfterDelay(delay));
    }

    private IEnumerator ReleaseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        releaseRoutine = null;
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
