using UnityEngine;

// Lightweight runtime blood stain decal (sprite on ground) that fades out over time.
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class TemporaryBloodStain : MonoBehaviour
{
    private static Sprite sharedSprite;

    private SpriteRenderer spriteRenderer;
    private float lifeTimer;
    private float lifeDuration = 8f;
    private float fadeStartNormalized = 0.65f;
    private Color baseColor = new Color(0.45f, 0.05f, 0.05f, 0.55f);
    private float initialAlpha = 0.55f;

    public static TemporaryBloodStain Spawn(
        Vector3 position,
        float scale,
        float lifetime,
        int sortingOrder,
        string sortingLayerName = null)
    {
        GameObject go = new GameObject("BloodStain");
        go.transform.position = position;

        var stain = go.AddComponent<TemporaryBloodStain>();
        stain.Configure(scale, lifetime, sortingOrder, sortingLayerName);
        return stain;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = GetOrCreateSharedSprite();
        spriteRenderer.color = baseColor;

        if (transform.localScale == Vector3.zero)
            transform.localScale = Vector3.one;
    }

    public void Configure(float scale, float lifetime, int sortingOrder, string sortingLayerName)
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        float uniform = Mathf.Max(0.05f, scale);
        float stretchX = Random.Range(0.9f, 1.5f);
        float stretchY = Random.Range(0.75f, 1.2f);
        transform.localScale = new Vector3(uniform * stretchX, uniform * stretchY, 1f);
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        lifeDuration = Mathf.Max(0.5f, lifetime);
        lifeTimer = 0f;
        fadeStartNormalized = Random.Range(0.45f, 0.75f);

        Color tint = baseColor;
        tint.r = Mathf.Clamp01(baseColor.r + Random.Range(-0.03f, 0.03f));
        tint.g = Mathf.Clamp01(baseColor.g + Random.Range(-0.02f, 0.02f));
        tint.b = Mathf.Clamp01(baseColor.b + Random.Range(-0.02f, 0.02f));
        tint.a = Mathf.Clamp(baseColor.a + Random.Range(-0.08f, 0.08f), 0.18f, 0.8f);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = tint;
            initialAlpha = tint.a;
            spriteRenderer.sortingOrder = sortingOrder;

            if (!string.IsNullOrWhiteSpace(sortingLayerName))
                spriteRenderer.sortingLayerName = sortingLayerName;
        }
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        float t = lifeDuration > 0.01f ? Mathf.Clamp01(lifeTimer / lifeDuration) : 1f;

        if (spriteRenderer != null)
        {
            float fadeT = Mathf.InverseLerp(fadeStartNormalized, 1f, t);
            Color c = spriteRenderer.color;
            c.a = initialAlpha * (1f - fadeT);
            spriteRenderer.color = c;
        }

        if (t >= 1f)
            Destroy(gameObject);
    }

    private static Sprite GetOrCreateSharedSprite()
    {
        if (sharedSprite != null)
            return sharedSprite;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.ARGB32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "Runtime_BloodStain_Texture"
        };

        Color32[] pixels = new Color32[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = x + (y * size);
                float dx = (x - center.x) / radius;
                float dy = (y - center.y) / radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = 1f - Mathf.Clamp01(dist);
                alpha = alpha * alpha * (3f - 2f * alpha); // smoothstep-ish soft edge
                alpha *= Random.Range(0.86f, 1f);

                byte a = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[i] = new Color32(255, 255, 255, a);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        texture.hideFlags = HideFlags.DontSave;

        sharedSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            64f
        );
        sharedSprite.name = "Runtime_BloodStain_Sprite";
        return sharedSprite;
    }
}
