using UnityEngine;

// Simple XP pickup.
public class XPGem : MonoBehaviour
{
    [SerializeField] private int value = 1;
    [Header("Magnet")]
    [SerializeField] private float magnetStartSpeed = 2.5f;
    [SerializeField] private float magnetAcceleration = 20f;
    [SerializeField] private float maxMagnetSpeed = 12f;
    [Header("Lifetime")]
    [SerializeField] private float despawnAfterSeconds = 20f;

    private Transform magnetTarget;
    private float collectDistance = 1f;
    private float magnetSpeed;
    private bool collected;
    private float despawnAtTime;
    private bool hasDespawnTimer;

    private void OnEnable()
    {
        collected = false;
        magnetTarget = null;
        magnetSpeed = magnetStartSpeed;
        SetDespawnTimer(despawnAfterSeconds);
    }

    private void Update()
    {
        if (collected) return;

        if (hasDespawnTimer && Time.time >= despawnAtTime)
        {
            ReturnToPoolOrDestroy();
            return;
        }

        if (magnetTarget == null) return;

        Vector3 toTarget = magnetTarget.position - transform.position;
        toTarget.z = 0f;
        float distance = toTarget.magnitude;
        if (distance <= 0.001f)
            return;

        magnetSpeed = Mathf.Min(maxMagnetSpeed, magnetSpeed + magnetAcceleration * Time.deltaTime);
        float step = magnetSpeed * Time.deltaTime;

        if (distance <= collectDistance)
        {
            transform.position = magnetTarget.position;
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, magnetTarget.position, step);
    }

    public void SetDespawnTimer(float seconds)
    {
        if (seconds <= 0f)
        {
            hasDespawnTimer = false;
            despawnAtTime = 0f;
            return;
        }

        hasDespawnTimer = true;
        despawnAtTime = Time.time + seconds;
    }

    public void MagnetizeTo(Transform target, float pickupRadius)
    {
        if (collected) return;
        if (target == null) return;

        magnetTarget = target;
        collectDistance = Mathf.Max(0.05f, pickupRadius);
    }

    public int Collect()
    {
        if (collected) return 0;
        collected = true;

        int result = Mathf.Max(0, value);
        ReturnToPoolOrDestroy();

        return result;
    }

    private void ReturnToPoolOrDestroy()
    {
        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }
}
