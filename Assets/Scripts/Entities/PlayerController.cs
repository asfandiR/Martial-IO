using System;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Player movement and input handling.
// Responsibilities:
// - Read input
// - Move character
// - Relay actions to ability/weapon systems
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerPickupController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float acceleration = 24f;
    [SerializeField] private float deceleration = 32f;

    [SerializeField] private Joystick joystick;
    [SerializeField] private Animator animator;
    [SerializeField] private float startingMaxHp = 20f;
    [Header("Audio")]
    [SerializeField] private bool enableFootsteps = true;
    [SerializeField] private float footstepIntervalAtFullSpeed = 0.32f;
    [SerializeField] private float footstepIntervalAtLowSpeed = 0.5f;
    [SerializeField] private float minFootstepVelocity = 0.2f;

    public event Action<int> OnCollectXp;

    private float moveSpeedMultiplier = 1f;
    private HealthSystem health;
    private Rigidbody2D rb;
    private PlayerPickupController pickupController;
    private Vector2 cachedInput;
    private float footstepTimer;

    public float MoveSpeedMultiplier => moveSpeedMultiplier;
    public float CurrentMoveSpeed => moveSpeed * moveSpeedMultiplier;

    private static readonly int IsDeadHash = Animator.StringToHash("IsDead");
    private static readonly int RunHash = Animator.StringToHash("Run");

    private Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 input = Vector2.zero;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
            input += pad.leftStick.ReadValue();

        return input;
#else
        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
#endif
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

        pickupController = GetComponent<PlayerPickupController>();
        if (pickupController == null)
            pickupController = gameObject.AddComponent<PlayerPickupController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (joystick == null)
            Debug.LogWarning("Joystick reference is missing on PlayerController.");
    }

    private void OnEnable()
    {
        if (health != null)
            health.OnDeath += HandleDeath;
        if (health != null)
            health.OnDamage += HandleDamageTaken;

        if (pickupController != null)
            pickupController.OnCollectXp += HandleXpCollected;

        if (animator != null)
            animator.SetBool(IsDeadHash, false);
    }

    private void OnDisable()
    {
        if (health != null)
            health.OnDeath -= HandleDeath;
        if (health != null)
            health.OnDamage -= HandleDamageTaken;

        if (pickupController != null)
            pickupController.OnCollectXp -= HandleXpCollected;

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        footstepTimer = 0f;
    }

    private void Update()
    {
        cachedInput = ReadMoveInput();
        float run = Mathf.Clamp01(cachedInput.magnitude);
        if (animator != null)
            animator.SetFloat(RunHash, run);
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
        TickFootsteps();

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
        SoundManager.Instance?.PlaySfx(GameSfxId.PlayerDeath, ignoreInterval: true);

        if (animator != null)
            animator.SetBool(IsDeadHash, true);

        if (rb != null)
            rb.linearVelocity = Vector2.zero;

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }

    private void HandleXpCollected(int value)
    {
        if (value > 0)
            OnCollectXp?.Invoke(value);
    }

    private void HandleDamageTaken(float _)
    {
        SoundManager.Instance?.PlaySfx(GameSfxId.PlayerHit);
    }

    private void TickFootsteps()
    {
        if (!enableFootsteps || rb == null)
            return;

        if (health != null && health.IsDead)
        {
            footstepTimer = 0f;
            return;
        }

        float speed = rb.linearVelocity.magnitude;
        if (speed < minFootstepVelocity)
        {
            footstepTimer = 0f;
            return;
        }

        float maxSpeed = Mathf.Max(0.01f, CurrentMoveSpeed);
        float t = Mathf.Clamp01(speed / maxSpeed);
        float interval = Mathf.Lerp(footstepIntervalAtLowSpeed, footstepIntervalAtFullSpeed, t);

        footstepTimer -= Time.fixedDeltaTime;
        if (footstepTimer > 0f)
            return;

        SoundManager.Instance?.PlaySfx(GameSfxId.Footstep);
        footstepTimer = Mathf.Max(0.05f, interval);
    }
}
