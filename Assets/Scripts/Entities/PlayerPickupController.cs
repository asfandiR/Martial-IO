using System;
using UnityEngine;

// Handles player pickup/magnet logic for XP gems, gold and effectors.
public class PlayerPickupController : MonoBehaviour
{
    [Header("XP Pickup")]
    [SerializeField] private float xpPickupRadius = 1.5f;
    [SerializeField] private float xpMagnetRadius = 5.5f;
    [SerializeField] private LayerMask xpGemMask;
    [SerializeField] private int maxXpGemChecks = 64;
    [SerializeField] private ParticleSystem xpPickupEffect;

    [Header("Gold Pickup")]
    [SerializeField] private float goldPickupRadius = 1.5f;
    [SerializeField] private float goldMagnetRadius = 5.5f;
    [SerializeField] private LayerMask goldMask;
    [SerializeField] private int maxGoldChecks = 64;
    [SerializeField] private bool useXpPickupEffectForGold = true;

    [Header("Effector Pickup")]
    [SerializeField] private float effectorPickupRadius = 1.5f;
    [SerializeField] private float effectorMagnetRadius = 5.5f;
    [SerializeField] private LayerMask effectorMask;
    [SerializeField] private int maxEffectorChecks = 64;
    [SerializeField] private bool useXpPickupEffectForEffectors = true;

    public event Action<int> OnCollectXp;

    private Collider2D[] xpGemHits;
    private Collider2D[] goldHits;
    private Collider2D[] effectorHits;

    private void Awake()
    {
        if (xpPickupEffect == null)
            Debug.LogWarning("XP Pickup Effect reference is missing on PlayerPickupController.");

        xpGemHits = new Collider2D[Mathf.Max(8, maxXpGemChecks)];
        goldHits = new Collider2D[Mathf.Max(8, maxGoldChecks)];
        effectorHits = new Collider2D[Mathf.Max(8, maxEffectorChecks)];
    }

    private void Update()
    {
        HandleXpGemsInRange();
        HandleGoldInRange();
        HandleEffectorsInRange();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollectXp(other);
        TryCollectGold(other);
        TryCollectEffector(other);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollectXp(other);
        TryCollectGold(other);
        TryCollectEffector(other);
    }

    private void TryCollectXp(Component source)
    {
        if (source == null) return;
        XPGem gem = source.GetComponentInParent<XPGem>();
        if (gem == null) return;

        if (xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectGem(gem);
    }

    private void HandleXpGemsInRange()
    {
        float magnetRadius = Mathf.Max(xpPickupRadius, xpMagnetRadius);
        int count = OverlapCircle((Vector2)transform.position, magnetRadius, xpGemHits, GetXpGemMask());
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

    private void TryCollectGold(Component source)
    {
        if (source == null) return;
        GoldPickup gold = source.GetComponentInParent<GoldPickup>();
        if (gold == null) return;

        if (useXpPickupEffectForGold && xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectGold(gold);
    }

    private void HandleGoldInRange()
    {
        float magnetRadius = Mathf.Max(goldPickupRadius, goldMagnetRadius);
        int count = OverlapCircle((Vector2)transform.position, magnetRadius, goldHits, GetGoldMask());
        if (count <= 0) return;

        float pickupSqr = goldPickupRadius * goldPickupRadius;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = goldHits[i];
            if (col == null) continue;

            GoldPickup gold = col.GetComponentInParent<GoldPickup>();
            if (gold == null) continue;

            Vector3 delta = gold.transform.position - transform.position;
            delta.z = 0f;

            if (delta.sqrMagnitude <= pickupSqr)
                CollectGold(gold);
            else
                gold.MagnetizeTo(transform, goldPickupRadius);
        }
    }

    private void TryCollectEffector(Component source)
    {
        if (source == null) return;
        EffectorPickup effector = source.GetComponentInParent<EffectorPickup>();
        if (effector == null) return;

        if (useXpPickupEffectForEffectors && xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectEffector(effector);
    }

    private void HandleEffectorsInRange()
    {
        float magnetRadius = Mathf.Max(effectorPickupRadius, effectorMagnetRadius);
        int count = OverlapCircle((Vector2)transform.position, magnetRadius, effectorHits, GetEffectorMask());
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

    private void CollectGem(XPGem gem)
    {
        if (gem == null) return;

        int value = gem.Collect();
        if (value > 0)
        {
            SoundManager.Instance?.PlaySfx(GameSfxId.PickupXp);
            OnCollectXp?.Invoke(value);
        }
    }

    private void CollectGold(GoldPickup gold)
    {
        if (gold == null) return;
        int collectedAmount = gold.Collect();
        if (collectedAmount > 0)
            SoundManager.Instance?.PlaySfx(GameSfxId.PickupXp);
    }

    private void CollectEffector(EffectorPickup effector)
    {
        if (effector == null) return;
        if (effector.Collect(gameObject))
            SoundManager.Instance?.PlaySfx(GameSfxId.PickupEffector);
    }

    private int GetXpGemMask()
    {
        return xpGemMask.value == 0 ? ~0 : xpGemMask.value;
    }

    private int GetGoldMask()
    {
        return goldMask.value == 0 ? ~0 : goldMask.value;
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
