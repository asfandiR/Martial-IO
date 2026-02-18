using UnityEngine;

// World effector pickup with magnet movement and timed stat buff apply.
public class EffectorPickup : MonoBehaviour
{
    [SerializeField] private EffectorSO effectorData;
    [SerializeField] private SpriteRenderer iconRenderer;

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

    public EffectorSO Data => effectorData;

    private void OnEnable()
    {
        collected = false;
        magnetTarget = null;
        magnetSpeed = magnetStartSpeed;
        SetDespawnTimer(defaultDespawnAfterSeconds);
        RefreshVisual();
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

    public bool Collect(GameObject collector)
    {
        if (collected) return false;
        collected = true;

        bool applied = false;
        if (effectorData != null && collector != null)
        {
            var runtime = collector.GetComponent<EffectorRuntimeController>();
            if (runtime == null)
                runtime = collector.AddComponent<EffectorRuntimeController>();

            runtime.ApplyEffector(effectorData);
            applied = true;
        }

        ReturnToPoolOrDestroy();
        return applied;
    }

    public void SetEffectorData(EffectorSO data)
    {
        effectorData = data;
        RefreshVisual();
    }

    private void ReturnToPoolOrDestroy()
    {
        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }

    private void RefreshVisual()
    {
        if (iconRenderer == null)
            iconRenderer = GetComponentInChildren<SpriteRenderer>();

        if (iconRenderer == null)
            return;

        iconRenderer.sprite = effectorData != null ? effectorData.icon : null;
    }
}
