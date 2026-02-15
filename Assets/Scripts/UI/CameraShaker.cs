using UnityEngine;

// Camera shake feedback.
public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance { get; private set; }

    [SerializeField] private float defaultDuration = 0.15f;
    [SerializeField] private float defaultStrength = 0.2f;

    private Vector3 originalLocalPos;
    private float shakeTimer;
    private float shakeDuration;
    private float shakeStrength;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        originalLocalPos = transform.localPosition;
    }

    private void Update()
    {
        if (shakeTimer <= 0f) return;

        shakeTimer -= Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(shakeTimer / shakeDuration);
        float strength = shakeStrength * t;
        Vector3 offset = Random.insideUnitSphere * strength;
        offset.z = 0f;
        transform.localPosition = originalLocalPos + offset;

        if (shakeTimer <= 0f)
            transform.localPosition = originalLocalPos;
    }

    public void Shake(float strength, float duration)
    {
        shakeStrength = Mathf.Max(0f, strength);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = shakeDuration;
    }

    public void ShakeDefault()
    {
        Shake(defaultStrength, defaultDuration);
    }
}
