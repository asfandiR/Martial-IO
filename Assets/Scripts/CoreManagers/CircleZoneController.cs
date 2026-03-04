using UnityEngine;

public class CircleZoneController : MonoBehaviour
{
    private enum ZoneMovementPattern
    {
        None,
        Circle,
        Spiral,
        Square,
        Triangle,
        Chaotic
    }

    [Header("Target")]
    [SerializeField] private Transform playerTransform;

    [Header("Zone Settings")]
    [SerializeField] private Vector2 mapCenter = Vector2.zero;
    [SerializeField] private Vector2 centerPosition = Vector2.zero;
    [SerializeField] private float maxRadius = 50f;
    [SerializeField] private float minRadius = 10f;
    [SerializeField] private float shrinkDuration = 300f;
    [SerializeField] private float damagePerSecondOutside = 5f;

    [Header("Center Movement")]
    [SerializeField] private ZoneMovementPattern movementPattern = ZoneMovementPattern.Circle;
    [SerializeField] private float centerMoveSpeed = 0.4f;
    [SerializeField] private float maxCenterOffset = 10f;
    [SerializeField] private float spiralTurns = 2f;
    [SerializeField] private float chaoticNoiseScale = 1.2f;

    [Header("Visuals (Optional)")]
    [SerializeField] private LineRenderer zoneVisuals;
    [SerializeField] private int circleSegments = 64;
    [SerializeField] private float ZOffset = -1f;

    private float timeElapsed;
    private float currentRadius;
    private HealthSystem playerHealth;
    private float chaoticSeedX;
    private float chaoticSeedY;

    private void Start()
    {
        currentRadius = maxRadius;
        centerPosition = mapCenter;

        chaoticSeedX = Random.Range(-1000f, 1000f);
        chaoticSeedY = Random.Range(-1000f, 1000f);

        if (playerTransform == null)
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                playerTransform = player.transform;
                playerHealth = player.GetComponent<HealthSystem>();
            }
        }
        else
        {
            playerHealth = playerTransform.GetComponent<HealthSystem>();
        }
    }

    private void Update()
    {
        timeElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(timeElapsed / Mathf.Max(0.01f, shrinkDuration));
        currentRadius = Mathf.Lerp(maxRadius, minRadius, t);

        UpdateCenterPosition(t);
        DrawZone();

        if (playerTransform == null) return;

        float distance = Vector2.Distance(playerTransform.position, centerPosition);
        if (distance > currentRadius && playerHealth != null)
        {
            playerHealth.TakeDamage(damagePerSecondOutside * Time.deltaTime);
        }
    }

    private void UpdateCenterPosition(float shrinkProgress)
    {
        if (movementPattern == ZoneMovementPattern.None || maxCenterOffset <= 0f)
        {
            centerPosition = mapCenter;
            return;
        }

        float timeFactor = timeElapsed * Mathf.Max(0.01f, centerMoveSpeed);
        float angle = timeFactor * Mathf.PI * 2f;
        Vector2 localOffset = Vector2.zero;

        switch (movementPattern)
        {
            case ZoneMovementPattern.Circle:
                localOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * maxCenterOffset;
                break;

            case ZoneMovementPattern.Spiral:
                float spiralRadius = Mathf.Lerp(0f, maxCenterOffset, Mathf.PingPong(shrinkProgress * spiralTurns, 1f));
                localOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spiralRadius;
                break;

            case ZoneMovementPattern.Square:
                localOffset = EvaluatePolygonPath(4, timeFactor) * maxCenterOffset;
                break;

            case ZoneMovementPattern.Triangle:
                localOffset = EvaluatePolygonPath(3, timeFactor) * maxCenterOffset;
                break;

            case ZoneMovementPattern.Chaotic:
                localOffset = EvaluateChaoticPath(timeFactor) * maxCenterOffset;
                break;
        }

        centerPosition = mapCenter + Vector2.ClampMagnitude(localOffset, maxCenterOffset);
    }

    private Vector2 EvaluatePolygonPath(int sides, float pathProgress)
    {
        if (sides < 3) return Vector2.zero;

        float wrapped = Mathf.Repeat(pathProgress, 1f);
        float scaled = wrapped * sides;
        int edgeIndex = Mathf.FloorToInt(scaled);
        float edgeT = scaled - edgeIndex;

        float startAngle = (edgeIndex / (float)sides) * Mathf.PI * 2f;
        float endAngle = ((edgeIndex + 1) / (float)sides) * Mathf.PI * 2f;

        Vector2 start = new Vector2(Mathf.Cos(startAngle), Mathf.Sin(startAngle));
        Vector2 end = new Vector2(Mathf.Cos(endAngle), Mathf.Sin(endAngle));
        return Vector2.Lerp(start, end, edgeT);
    }

    private Vector2 EvaluateChaoticPath(float timeFactor)
    {
        float nx = Mathf.PerlinNoise(chaoticSeedX + timeFactor * chaoticNoiseScale, chaoticSeedY) * 2f - 1f;
        float ny = Mathf.PerlinNoise(chaoticSeedX, chaoticSeedY + timeFactor * chaoticNoiseScale) * 2f - 1f;
        return new Vector2(nx, ny);
    }

    private void DrawZone()
    {
        if (zoneVisuals == null) return;

        zoneVisuals.positionCount = circleSegments + 1;
        zoneVisuals.useWorldSpace = true;
        zoneVisuals.loop = true;

        float deltaTheta = (2f * Mathf.PI) / circleSegments;
        float theta = 0f;

        for (int i = 0; i < circleSegments + 1; i++)
        {
            float x = currentRadius * Mathf.Cos(theta) + centerPosition.x;
            float y = currentRadius * Mathf.Sin(theta) + centerPosition.y;
            zoneVisuals.SetPosition(i, new Vector3(x, y, ZOffset));
            theta += deltaTheta;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        float r = Application.isPlaying ? currentRadius : maxRadius;
        Gizmos.DrawWireSphere((Vector3)centerPosition, r);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere((Vector3)mapCenter, maxCenterOffset);
    }
}
