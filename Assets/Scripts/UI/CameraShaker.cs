using UnityEngine;
using System.Collections;

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
    private Coroutine shakeRoutine;

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

    private void OnDisable()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        transform.localPosition = originalLocalPos;
        shakeTimer = 0f;
    }

    public void Shake(float strength, float duration)
    {
        shakeStrength = Mathf.Max(0f, strength);
        shakeDuration = Mathf.Max(0.01f, duration);
        shakeTimer = shakeDuration;

        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    public void ShakeDefault()
    {
        Shake(defaultStrength, defaultDuration);
    }

    private IEnumerator ShakeRoutine()
    {
        while (shakeTimer > 0f)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(shakeTimer / shakeDuration);
            float strength = shakeStrength * t;
            Vector3 offset = Random.insideUnitSphere * strength;
            offset.z = 0f;
            transform.localPosition = originalLocalPos + offset;
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        shakeRoutine = null;
    }
}
