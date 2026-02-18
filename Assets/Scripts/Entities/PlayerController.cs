using System;
using UnityEngine;

// Player movement and input handling.
// Responsibilities:
// - Read input
// - Move character
// - Relay actions to ability/weapon systems
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float deceleration = 32f;

    [SerializeField] private Joystick joystick;
    [SerializeField] private Animator animator;
    [SerializeField] private float startingMaxHp = 20f;

    [Header("XP Pickup")]
    [SerializeField] private float xpPickupRadius = 1.5f;
    [SerializeField] private float xpMagnetRadius = 5.5f;
    [SerializeField] private LayerMask xpGemMask;
    [SerializeField] private int maxXpGemChecks = 64;
    [SerializeField] private ParticleSystem xpPickupEffect;

    [Header("Relic Pickup")]
    [SerializeField] private float relicPickupRadius = 1.5f;
    [SerializeField] private float relicMagnetRadius = 5.5f;
    [SerializeField] private LayerMask relicMask;
    [SerializeField] private int maxRelicChecks = 64;
    [SerializeField] private bool useXpPickupEffectForRelics = true;

    [Header("Effector Pickup")]
    [SerializeField] private float effectorPickupRadius = 1.5f;
    [SerializeField] private float effectorMagnetRadius = 5.5f;
    [SerializeField] private LayerMask effectorMask;
    [SerializeField] private int maxEffectorChecks = 64;
    [SerializeField] private bool useXpPickupEffectForEffectors = true;

    public event Action<int> OnCollectXp;

    private float moveSpeedMultiplier = 1f;
    private HealthSystem health;
    private Rigidbody2D rb;
    private Collider2D[] xpGemHits;
    private Collider2D[] relicHits;
    private Collider2D[] effectorHits;
    private Vector2 cachedInput;

    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float CurrentMoveSpeed => moveSpeed * moveSpeedMultiplier;

    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private Vector2 ReadMoveInput()
    {
        if (joystick != null)
            return new Vector2(joystick.Horizontal, joystick.Vertical);

        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    private void Awake()
    {
        health = GetComponent<HealthSystem>();
        if (health == null)
            health = gameObject.AddComponent<HealthSystem>();
        health.SetMaxHp(startingMaxHp, true);

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (joystick == null)
            Debug.LogWarning("Joystick reference is missing on PlayerController.");

        if (xpPickupEffect == null)
            Debug.LogWarning("XP Pickup Effect reference is missing on PlayerController.");

        xpGemHits = new Collider2D[Mathf.Max(8, maxXpGemChecks)];
        relicHits = new Collider2D[Mathf.Max(8, maxRelicChecks)];
        effectorHits = new Collider2D[Mathf.Max(8, maxEffectorChecks)];
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;

        if (animator != null)
            animator.SetBool(IsDeadHash, false);
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void Update()
    {
        cachedInput = ReadMoveInput();
        float run = Mathf.Clamp01(cachedInput.magnitude);
        if (animator != null)
            animator.SetFloat(RunHash, run);

        HandleXpGemsInRange();
        HandleRelicsInRange();
        HandleEffectorsInRange();
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        if (health != null && health.IsDead)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 input = cachedInput;
        if (input.sqrMagnitude > 1f)
            input.Normalize();

        Vector2 desiredVelocity = input * (moveSpeed * moveSpeedMultiplier);
        float changeRate = desiredVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, changeRate * Time.fixedDeltaTime);

        if (Mathf.Abs(rb.linearVelocity.x) > 0.05f)
        {
            Vector3 scale = transform.localScale;
            scale.x = rb.linearVelocity.x > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }
    }

    public void MultiplyMoveSpeed(float multiplier)
    {
        moveSpeedMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    private void HandleDeath()
    {
        if (animator != null)
            animator.SetBool(IsDeadHash, true);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollectXp(other);
        TryCollectRelic(other);
        TryCollectEffector(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollectXp(other);
        TryCollectRelic(other);
        TryCollectEffector(other);
    }

    private void TryCollectXp(Component source)
    {
        if (source == null) return;
        var gem = source.GetComponentInParent<XPGem>();
        if (gem == null) return;

        if (xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectGem(gem);
    }

    private void HandleXpGemsInRange()
    {
        float magnetRadius = Mathf.Max(xpPickupRadius, xpMagnetRadius);
        int count = OverlapCircle(
            transform.position,
            magnetRadius,
            xpGemHits,
            GetXpGemMask()
        );

        if (count <= 0) return;

        float pickupSqr = xpPickupRadius * xpPickupRadius;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = xpGemHits[i];
            if (col == null) continue;

            XPGem gem = col.GetComponentInParent<XPGem>();
            if (gem == null) continue;

            Vector3 delta = gem.transform.position - transform.position;
            delta.z = 0f;

            if (delta.sqrMagnitude <= pickupSqr)
                CollectGem(gem);
            else
                gem.MagnetizeTo(transform, xpPickupRadius);
        }
    }

    private void TryCollectRelic(Component source)
    {
        if (source == null) return;
        var relic = source.GetComponentInParent<RelicPickup>();
        if (relic == null) return;

        if (useXpPickupEffectForRelics && xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectRelic(relic);
    }

    private void HandleRelicsInRange()
    {
        float magnetRadius = Mathf.Max(relicPickupRadius, relicMagnetRadius);
        int count = OverlapCircle(
            transform.position,
            magnetRadius,
            relicHits,
            GetRelicMask()
        );

        if (count <= 0) return;

        float pickupSqr = relicPickupRadius * relicPickupRadius;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = relicHits[i];
            if (col == null) continue;

            RelicPickup relic = col.GetComponentInParent<RelicPickup>();
            if (relic == null) continue;

            Vector3 delta = relic.transform.position - transform.position;
            delta.z = 0f;

            if (delta.sqrMagnitude <= pickupSqr)
                CollectRelic(relic);
            else
                relic.MagnetizeTo(transform, relicPickupRadius);
        }
    }

    private void CollectGem(XPGem gem)
    {
        if (gem == null) return;

        int value = gem.Collect();
        if (value > 0)
            OnCollectXp?.Invoke(value);
    }

    private void CollectRelic(RelicPickup relic)
    {
        if (relic == null) return;
        relic.Collect();
    }

    private void TryCollectEffector(Component source)
    {
        if (source == null) return;
        var effector = source.GetComponentInParent<EffectorPickup>();
        if (effector == null) return;

        if (useXpPickupEffectForEffectors && xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectEffector(effector);
    }

    private void HandleEffectorsInRange()
    {
        float magnetRadius = Mathf.Max(effectorPickupRadius, effectorMagnetRadius);
        int count = OverlapCircle(
            transform.position,
            magnetRadius,
            effectorHits,
            GetEffectorMask()
        );

        if (count <= 0) return;

        float pickupSqr = effectorPickupRadius * effectorPickupRadius;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = effectorHits[i];
            if (col == null) continue;

            EffectorPickup effector = col.GetComponentInParent<EffectorPickup>();
            if (effector == null) continue;

            Vector3 delta = effector.transform.position - transform.position;
            delta.z = 0f;

            if (delta.sqrMagnitude <= pickupSqr)
                CollectEffector(effector);
            else
                effector.MagnetizeTo(transform, effectorPickupRadius);
        }
    }

    private void CollectEffector(EffectorPickup effector)
    {
        if (effector == null) return;
        effector.Collect(gameObject);
    }

    private int GetXpGemMask()
    {
        return xpGemMask.value == 0 ? ~0 : xpGemMask.value;
    }

    private int GetRelicMask()
    {
        return relicMask.value == 0 ? ~0 : relicMask.value;
    }

    private int GetEffectorMask()
    {
        return effectorMask.value == 0 ? ~0 : effectorMask.value;
    }

    private static int OverlapCircle(Vector2 center, float radius, Collider2D[] buffer, int layerMask)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = layerMask;
        filter.useTriggers = true;
        return Physics2D.OverlapCircle(center, radius, filter, buffer);
    }
}

