using System;
using UnityEngine;

// Handles player pickup/magnet logic for XP gems, relics and effectors.
public class PlayerPickupController : MonoBehaviour
{
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

    private Collider2D[] xpGemHits;
    private Collider2D[] relicHits;
    private Collider2D[] effectorHits;

    private void Awake()
    {
        if (xpPickupEffect == null)
            Debug.LogWarning("XP Pickup Effect reference is missing on PlayerPickupController.");

        xpGemHits = new Collider2D[Mathf.Max(8, maxXpGemChecks)];
        relicHits = new Collider2D[Mathf.Max(8, maxRelicChecks)];
        effectorHits = new Collider2D[Mathf.Max(8, maxEffectorChecks)];
    }

    private void Update()
    {
        HandleXpGemsInRange();
        HandleRelicsInRange();
        HandleEffectorsInRange();
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

    private void TryCollectRelic(Component source)
    {
        if (source == null) return;
        RelicPickup relic = source.GetComponentInParent<RelicPickup>();
        if (relic == null) return;

        if (useXpPickupEffectForRelics && xpPickupEffect != null)
            xpPickupEffect.Play();

        CollectRelic(relic);
    }

    private void HandleRelicsInRange()
    {
        float magnetRadius = Mathf.Max(relicPickupRadius, relicMagnetRadius);
        int count = OverlapCircle((Vector2)transform.position, magnetRadius, relicHits, GetRelicMask());
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

    private void CollectRelic(RelicPickup relic)
    {
        if (relic == null) return;
        if (relic.Collect())
            SoundManager.Instance?.PlaySfx(GameSfxId.PickupRelic);
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
