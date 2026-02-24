using UnityEngine;

public class CircleZoneController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform playerTransform;

    [Header("Zone Settings")]
    [SerializeField] private Vector2 centerPosition = Vector2.zero;
    [SerializeField] private float maxRadius = 50f;
    [SerializeField] private float minRadius = 10f;
    [SerializeField] private float shrinkDuration = 300f; // Время в секундах до минимального радиуса
    [SerializeField] private float damagePerSecondOutside = 5f;

    [Header("Visuals (Optional)")]
    [SerializeField] private LineRenderer zoneVisuals;
    [SerializeField] private int circleSegments = 64;

    private float timeElapsed;
    private float currentRadius;
    private HealthSystem playerHealth;

    private void Start()
    {
        currentRadius = maxRadius;
        
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
        // 1. Расчет текущего радиуса
        timeElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(timeElapsed / shrinkDuration);
        currentRadius = Mathf.Lerp(maxRadius, minRadius, t);

        // 2. Отрисовка (если есть LineRenderer)
        DrawZone();

        if (playerTransform == null) return;

        // 3. Проверка выхода за границы
        float distance = Vector2.Distance(playerTransform.position, centerPosition);
        
        // Вариант А: Жесткая стена (игрок не может выйти)
        if (distance > currentRadius)
        {
            Vector2 fromCenter = (Vector2)playerTransform.position - centerPosition;
            fromCenter = fromCenter.normalized * currentRadius;
            playerTransform.position = centerPosition + fromCenter;
        }

        // Вариант Б: Урон за зоной (раскомментируйте, если нужно вместо стены)
        /*
        if (distance > currentRadius && playerHealth != null)
        {
            playerHealth.TakeDamage(damagePerSecondOutside * Time.deltaTime);
        }
        */
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
            zoneVisuals.SetPosition(i, new Vector3(x, y, 0f));
            theta += deltaTheta;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        // Рисуем текущий радиус (в редакторе будет видно макс радиус, если не запущен play mode)
        float r = Application.isPlaying ? currentRadius : maxRadius;
        Gizmos.DrawWireSphere((Vector3)centerPosition, r);
    }
}