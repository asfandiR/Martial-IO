using UnityEngine;

// World gold pickup with XP-like magnet movement and optional timed despawn.
public class GoldPickup : MonoBehaviour
{
    private const int GoldPerPickup = 1;

    [Header("Magnet")]
    [SerializeField] private float magnetStartSpeed = 2.5f;
    [SerializeField] private float magnetAcceleration = 20f;
    [SerializeField] private float maxMagnetSpeed = 12f;

    [Header("Lifetime")]
    [SerializeField] private float defaultDespawnAfterSeconds = 25f;

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
        SetDespawnTimer(defaultDespawnAfterSeconds);
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

        SaveSystem saveSystem = SaveSystem.Instance;
        if (saveSystem == null)
            return 0;

        saveSystem.AddGold(GoldPerPickup);
        collected = true;
        ReturnToPoolOrDestroy();
        return GoldPerPickup;
    }

    private void ReturnToPoolOrDestroy()
    {
        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }
}
