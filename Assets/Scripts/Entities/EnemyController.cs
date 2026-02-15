using System;
using System.Collections;
using UnityEngine;

// Enemy AI controller.
// Responsibilities:
// - Chase target
// - Handle enemy stats and behaviors
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("Physics")]
    [SerializeField] private bool enforceEnemyLayerSelfCollision = true;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float acceleration = 18f;
    [SerializeField] private float deceleration = 26f;
    [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.7f;

    [SerializeField, Range(0.1f, 1f)] private float earlyHpMultiplier = 0.45f;
    [SerializeField] private float fullHpDifficultyAt = 3f;
    [SerializeField] private GameObject xpGemPrefab;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string hitKeyword = "hit";
    [SerializeField] private string runWalkKeyword = "fly/walk/run";
    [SerializeField] private string deadKeyword = "dead";
    [SerializeField] private string idleKeyword = "idle";
    [SerializeField] private string attackKeyword = "attack";

    [Header("Spacing")]
    [SerializeField] private float desiredDistanceFromPlayer = 1.6f;
    [SerializeField] private float distanceTolerance = 0.2f;

    private Transform target;
    private HealthSystem health;
    private Rigidbody2D rb;
    private float contactDamage;
    [SerializeField] private float contactHitInterval = 0.7f;
    private float contactTimer;
    private bool configured;
    private bool isDying;
    private string lastPlayedClipName;
    private Vector2 desiredVelocity;

    private void Awake()
    {
        health = GetComponent<HealthSystem>();
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (enforceEnemyLayerSelfCollision)
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            if (enemyLayer >= 0)
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, false);
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void OnEnable()
    {
        isDying = false;
        lastPlayedClipName = null;
        desiredVelocity = Vector2.zero;

        if (health != null)
        {
            health.Revive(health.MaxHp);
            health.OnDeath += HandleDeath;
            health.OnDamage += HandleDamageTaken;
        }

        PlayByKeyword(idleKeyword, forceRestart: true);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnDeath -= HandleDeath;
            health.OnDamage -= HandleDamageTaken;
        }

        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    private void Start()
    {
        var playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            target = playerObj.transform;
    }

    private void Update()
    {
        if (isDying)
        {
            desiredVelocity = Vector2.zero;
            PlayByKeyword(idleKeyword);
            return;
        }

        if (contactTimer > 0f)
            contactTimer -= Time.deltaTime;

        if (target == null)
        {
            desiredVelocity = Vector2.zero;
            PlayByKeyword(idleKeyword);
            return;
        }

        Vector2 myPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector3 toTarget = target.position - (Vector3)myPos;
        toTarget.z = 0f;

        if (toTarget.sqrMagnitude <= 0.01f)
        {
            desiredVelocity = Vector2.zero;
            PlayByKeyword(idleKeyword);
            return;
        }

        Vector3 direction = GetDistanceControlDirection(toTarget);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            desiredVelocity = Vector2.zero;
            PlayByKeyword(idleKeyword);
            return;
        }

        direction.Normalize();

        desiredVelocity = (Vector2)direction * moveSpeed;

        if (Mathf.Abs(desiredVelocity.x) > 0.05f)
        {
            Vector3 scale = transform.localScale;
            scale.x = desiredVelocity.x > 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            transform.localScale = scale;
        }

        PlayByKeyword(runWalkKeyword);
    }

    private void FixedUpdate()
    {
        if (rb == null) return;
        if (isDying)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float rate = desiredVelocity.sqrMagnitude > 0.0001f ? acceleration : deceleration;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, rate * Time.fixedDeltaTime);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealContactDamage(other);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision == null) return;
        TryDealContactDamage(collision.collider);
    }

    private void TryDealContactDamage(Collider2D other)
    {
        if (other == null) return;
        if (contactDamage <= 0f) return;
        if (contactTimer > 0f) return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        var damageable = player.GetComponent<HealthSystem>();
        if (damageable == null || damageable.IsDead) return;

        PlayByKeyword(attackKeyword, forceRestart: true);
        damageable.TakeDamage(contactDamage);
        contactTimer = Mathf.Max(0.1f, contactHitInterval);
    }

    private void HandleDamageTaken(float _)
    {
        if (isDying) return;
        PlayByKeyword(hitKeyword, forceRestart: true);
    }

    private Vector3 GetDistanceControlDirection(Vector3 toTarget)
    {
        float distance = toTarget.magnitude;
        float minDistance = Mathf.Max(0.1f, desiredDistanceFromPlayer - distanceTolerance);
        float maxDistance = desiredDistanceFromPlayer + distanceTolerance;

        if (distance > maxDistance)
            return toTarget.normalized;
        if (distance < minDistance)
            return -toTarget.normalized;

        return Vector3.zero;
    }

    private bool PlayByKeyword(string keyword, bool forceRestart = false)
    {
        if (animator == null) return false;

        string clipName = FindClipNameByKeyword(keyword);
        if (string.IsNullOrEmpty(clipName)) return false;

        if (!forceRestart && string.Equals(lastPlayedClipName, clipName, StringComparison.Ordinal))
            return true;

        animator.Play(clipName, 0, 0f);
        lastPlayedClipName = clipName;
        return true;
    }

    private string FindClipNameByKeyword(string keyword)
    {
        if (animator == null || string.IsNullOrWhiteSpace(keyword)) return null;

        var controller = animator.runtimeAnimatorController;
        if (controller == null) return null;

        string[] tokens = keyword.Split('/');
        var clips = controller.animationClips;

        for (int t = 0; t < tokens.Length; t++)
        {
            string token = tokens[t].Trim();
            if (string.IsNullOrWhiteSpace(token)) continue;

            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || string.IsNullOrWhiteSpace(clip.name)) continue;

                if (clip.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return clip.name;
            }
        }

        return null;
    }

    private void HandleDeath()
    {
        if (isDying) return;
        isDying = true;
        desiredVelocity = Vector2.zero;
        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        SpawnXpGem();
        PlayByKeyword(deadKeyword, forceRestart: true);
        StartCoroutine(DespawnAfterDelay(0.15f));
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        float timer = Mathf.Max(0f, delay);
        while (timer > 0f)
        {
            timer -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (ObjectPooler.Instance != null)
            ObjectPooler.Instance.ReturnToPool(gameObject);
        else
            Destroy(gameObject);
    }

    private void SpawnXpGem()
    {
        if (xpGemPrefab == null) return;
        if (ObjectPooler.Instance == null) return;

        ObjectPooler.Instance.Get(
            xpGemPrefab,
            transform.position,
            Quaternion.identity
        );
    }

    public void Configure(EnemyData data, float difficultyMultiplier)
    {
        if (data == null) return;
        if (configured) return;
        configured = true;

        moveSpeed = Mathf.Max(0.1f, data.baseSpeed * difficultyMultiplier * speedMultiplier);
        contactDamage = Mathf.Max(0f, data.baseDamage * difficultyMultiplier);

        if (health != null)
        {
            float t = Mathf.InverseLerp(1f, Mathf.Max(1f, fullHpDifficultyAt), difficultyMultiplier);
            float hpScale = Mathf.Lerp(earlyHpMultiplier, 1f, t);
            float hp = Mathf.Max(1f, data.baseHp * difficultyMultiplier * hpScale);
            health.SetMaxHp(hp, true);
        }
    }
}
