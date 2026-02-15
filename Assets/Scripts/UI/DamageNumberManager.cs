using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Floating damage numbers.
public class DamageNumberManager : MonoBehaviour
{
    public static DamageNumberManager Instance { get; private set; }

    [SerializeField] private RectTransform screenSpaceRoot;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private TMP_Text damageTextPrefab;
    [SerializeField] private float floatDistance = 40f;
    [SerializeField] private float lifetime = 0.7f;
    [SerializeField] private Color color = new Color(1f, 0.25f, 0.2f, 1f);
    [SerializeField] private int maxActive = 64;

    private readonly List<DamageNumber> active = new List<DamageNumber>(128);
    private readonly Stack<DamageNumber> pool = new Stack<DamageNumber>(128);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            var dn = active[i];
            dn.timer += dt;
            float t = Mathf.Clamp01(dn.timer / lifetime);
            float yOffset = floatDistance * t;

            if (dn.isScreenSpace)
            {
                dn.rect.anchoredPosition = dn.startScreenPos + new Vector2(0f, yOffset);
            }
            else
            {
                Vector3 worldPos = dn.worldPos + Vector3.up * yOffset * 0.01f;
                dn.rect.position = worldCamera.WorldToScreenPoint(worldPos);
            }

            Color c = dn.text.color;
            c.a = 1f - t;
            dn.text.color = c;

            if (dn.timer >= lifetime)
            {
                Despawn(i);
            }
        }
    }

    public void SpawnDamage(Vector3 worldPosition, float amount)
    {
        if (damageTextPrefab == null) return;
        if (active.Count >= maxActive) return;

        DamageNumber dn = pool.Count > 0 ? pool.Pop() : CreateInstance();
        dn.timer = 0f;
        dn.text.text = Mathf.CeilToInt(amount).ToString();
        dn.text.color = color;
        dn.worldPos = worldPosition;

        if (screenSpaceRoot != null)
        {
            dn.isScreenSpace = true;
            dn.rect.SetParent(screenSpaceRoot, false);
            dn.startScreenPos = WorldToScreen(worldPosition);
            dn.rect.anchoredPosition = dn.startScreenPos;
        }
        else
        {
            dn.isScreenSpace = false;
            dn.rect.SetParent(transform, false);
            dn.rect.position = WorldToScreen(worldPosition);
        }

        dn.text.gameObject.SetActive(true);
        active.Add(dn);
    }

    private Vector2 WorldToScreen(Vector3 worldPos)
    {
        if (worldCamera == null) worldCamera = Camera.main;
        return worldCamera != null ? (Vector2)worldCamera.WorldToScreenPoint(worldPos) : Vector2.zero;
    }

    private DamageNumber CreateInstance()
    {
        var text = Instantiate(damageTextPrefab, transform);
        text.gameObject.SetActive(false);
        var dn = new DamageNumber
        {
            text = text,
            rect = text.rectTransform
        };
        return dn;
    }

    private void Despawn(int index)
    {
        var dn = active[index];
        dn.text.gameObject.SetActive(false);
        active.RemoveAt(index);
        pool.Push(dn);
    }

    private class DamageNumber
    {
        public TMP_Text text;
        public RectTransform rect;
        public float timer;
        public bool isScreenSpace;
        public Vector2 startScreenPos;
        public Vector3 worldPos;
    }
}
