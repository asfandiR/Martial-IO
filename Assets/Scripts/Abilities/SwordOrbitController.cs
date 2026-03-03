using System;
using System.Collections.Generic;
using UnityEngine;

// Sword orbit logic: spin, touch damage, and sword progression.
public class SwordOrbitController : MonoBehaviour
{
    [Serializable]
    private class SwordOrbiter
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Collider2D collider;
        public Vector3 baseLocalScale;
    }
private string anim="Change";
    [Header("Sword")]
    [SerializeField] private bool swordEnabled = true;
    [SerializeField] private float swordDamage = 2f;
    [SerializeField] private Transform swordOrbitRoot;
    [SerializeField] private Transform[] swordTransforms = new Transform[0];
    [SerializeField] private float baseOrbitRadius = 2.5f; // Базовый радиус орбиты
    [SerializeField] private SwordDateBase swordDateBase;
    [SerializeField] private float swordOrbitSpeed = 180f;
    [SerializeField] private float swordTouchDamageInterval = 0.2f;
    [SerializeField] private LayerMask enemyMask;

    [Header("Sword Ability Tokens")]
    [SerializeField] private AbilityTag[] swordAbilityTags =
    {
        AbilityTag.Swordsman,
        AbilityTag.Berserker,
        AbilityTag.Paladin,
        AbilityTag.Blacksmith,
        AbilityTag.Spearman
    };
     private Animator[] Animators = new Animator[0];

    private readonly Collider2D[] swordTouchHits = new Collider2D[32];
    private readonly Dictionary<int, float> swordHitTimers = new Dictionary<int, float>(128);
    private readonly List<SwordOrbiter> swordOrbiters = new List<SwordOrbiter>(8);
    private ContactFilter2D hitFilter;

    private float swordOrbitAngle;
    private int learnedAbilityCount;
    private int unlockedSwordCount;
    private float swordOrbitSpeedMultiplier = 1f;
    private float swordDamageMultiplier = 1f;
    private float additionalOrbitRadius = 0f;
    private float swordScaleYMultiplier = 1f;

    private void Awake()
    {
        if (swordOrbitRoot == null)
        {
            for (int i = 0; i < swordTransforms.Length; i++)
            {
                if (swordTransforms[i] == null) continue;
                swordOrbitRoot = swordTransforms[i].parent;
                break;
            }
        }
Animators = new Animator[swordTransforms.Length];
        for (int i = 0; i < swordTransforms.Length; i++)
        {
            Transform tr = swordTransforms[i];
            if (tr == null) continue;
            Animators[i] = tr.GetComponent<Animator>();
            SpriteRenderer sr = tr.GetComponentInChildren<SpriteRenderer>();
            Collider2D col = tr.GetComponentInChildren<Collider2D>();
            AddSwordOrbiter(tr, sr, col);
        }

        // Start with one unlocked sword.
        unlockedSwordCount = swordOrbiters.Count > 0 ? 1 : 0;
        for (int i = 0; i < swordOrbiters.Count; i++)
        {
            var orbiter = swordOrbiters[i];
            if (orbiter == null || orbiter.transform == null) continue;
            orbiter.transform.gameObject.SetActive(i < unlockedSwordCount);
        }

        hitFilter = new ContactFilter2D();
        hitFilter.useLayerMask = true;
        hitFilter.layerMask = enemyMask;
        hitFilter.useTriggers = true;
        
        UpdateSwordPositions(); // Первичное распределение
    }

    private void Update()
    {
        TickSwords();
    }

    // Kept for compatibility with existing AbilityManager calls.
    public void AddSwordSectorAngle(float _)
    {
    }

    public void AddSwordRadius(float radiusDelta)
    {
        additionalOrbitRadius += radiusDelta;
        UpdateSwordPositions();
    }

    private void UpdateSwordPositions()
    {
        int activeCount = unlockedSwordCount;
        if (activeCount <= 0) return;

        float currentRadius = baseOrbitRadius + additionalOrbitRadius;
        float angleStep = 360f / activeCount;

        // Распределяем только активные мечи
        for (int i = 0; i < activeCount; i++)
        {
            if (i >= swordOrbiters.Count) break;

            var orbiter = swordOrbiters[i];
            if (orbiter != null && orbiter.transform != null)
            {
                float angleRad = (i * angleStep) * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * currentRadius;
                orbiter.transform.localPosition = pos;
                
                // Поворачиваем меч лезвием наружу (опционально)
                float angleDeg = i * angleStep;
                orbiter.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);
            }
        }
    }

    public void HandleAbilityLearned(AbilityData ability)
    {
        ApplyNextSwordSkin();

        if (IsSwordAbility(ability))
            AddExtraSword();
    }

    private void ApplyNextSwordSkin()
    {
        if (swordDateBase == null || swordOrbiters.Count == 0) return;

        int count = swordDateBase.SwordCount;
        if (count <= 0) return;

        int index = learnedAbilityCount % count;
        int cycle = learnedAbilityCount / count;

        Sprite sprite = swordDateBase.GetSwordSprite(index);
        Color color;
        if (!swordDateBase.TryGetCycleColor(cycle, out color))
            color = cycle <= 0 ? Color.white : Color.HSVToRGB((cycle * 0.19f) % 1f, 0.55f, 1f);

        for (int i = 0; i < swordOrbiters.Count; i++)
        {
            var orbiter = swordOrbiters[i];
            if (orbiter == null || orbiter.renderer == null) continue;
            if (sprite != null)
                orbiter.renderer.sprite = sprite;
            orbiter.renderer.color = color;
            if (Animators != null && i < Animators.Length && Animators[i] != null)
            Animators[i].SetTrigger(anim);
        }

        learnedAbilityCount++;
    }

    private void AddExtraSword()
    {
        if (unlockedSwordCount >= swordOrbiters.Count) return;
        unlockedSwordCount++;
        UpdateSwordPositions(); // Пересчитываем симметрию при добавлении нового меча
    }

    private void AddSwordOrbiter(Transform tr, SpriteRenderer sr, Collider2D col)
    {
        if (tr == null) return;
        if (swordOrbitRoot != null && tr.parent != swordOrbitRoot)
            tr.SetParent(swordOrbitRoot, true);

        var orbiter = new SwordOrbiter
        {
            transform = tr,
            renderer = sr,
            collider = col,
            baseLocalScale = tr.localScale,
        };

        swordOrbiters.Add(orbiter);
        ApplySwordScaleYToOrbiter(orbiter);
    }

    private bool IsSwordAbility(AbilityData ability)
    {
        if (ability == null) return false;
        if (ability.tags == null) return false;

        for (int i = 0; i < swordAbilityTags.Length; i++)
        {
            if (ability.tags.Contains(swordAbilityTags[i]))
                return true;
        }

        return false;
    }

    private void TickSwords()
    {
        if (swordOrbiters.Count == 0) return;

        TickSwordHitTimers();

        bool active = swordEnabled;
        for (int i = 0; i < swordOrbiters.Count; i++)
        {
            var orbiter = swordOrbiters[i];
            if (orbiter == null || orbiter.transform == null) continue;

            bool shouldBeActive = active && i < unlockedSwordCount;
            if (orbiter.transform.gameObject.activeSelf != shouldBeActive)
                orbiter.transform.gameObject.SetActive(shouldBeActive);
        }

        if (!active) return;

        swordOrbitAngle += (swordOrbitSpeed * swordOrbitSpeedMultiplier) * Time.deltaTime;
        if (swordOrbitAngle >= 360f)
            swordOrbitAngle -= 360f;

        if (swordOrbitRoot != null)
        {
            swordOrbitRoot.position = transform.position;
            swordOrbitRoot.rotation = Quaternion.Euler(0f, 0f, swordOrbitAngle);
        }

        DealSwordColliderContactDamage();
    }

    public void MultiplySwordOrbitSpeed(float multiplier)
    {
        float clamped = Mathf.Clamp(multiplier, 0.8f, 1.25f);
        swordOrbitSpeedMultiplier *= clamped;
    }

    public void MultiplySwordDamage(float multiplier)
    {
        swordDamageMultiplier *= Mathf.Max(0.1f, multiplier);
    }

    public void AddSwordScaleY(float scaleDelta, float maxMultiplier = 5f)
    {
        float clampedMax = Mathf.Max(1f, maxMultiplier);
        swordScaleYMultiplier = Mathf.Clamp(swordScaleYMultiplier + scaleDelta, 1f, clampedMax);
        ApplySwordScaleY();
    }

    private void ApplySwordScaleY()
    {
        for (int i = 0; i < swordOrbiters.Count; i++)
            ApplySwordScaleYToOrbiter(swordOrbiters[i]);
    }

    private void ApplySwordScaleYToOrbiter(SwordOrbiter orbiter)
    {
        if (orbiter == null || orbiter.transform == null)
            return;

        Vector3 baseScale = orbiter.baseLocalScale;
        orbiter.transform.localScale = new Vector3(
            baseScale.x,
            baseScale.y * swordScaleYMultiplier,
            baseScale.z
        );
    }

   /* private void OnValidate()
    {
        if (swordTransforms == null) return;
        if (swordTransforms.Length == 8) return;
        Array.Resize(ref swordTransforms, 8);
    }*/

    private void DealSwordColliderContactDamage()
    {
        for (int s = 0; s < swordOrbiters.Count; s++)
        {
            var orbiter = swordOrbiters[s];
            if (orbiter == null || orbiter.collider == null) continue;
            if (!orbiter.transform.gameObject.activeInHierarchy) continue;

            int count = orbiter.collider.Overlap(hitFilter, swordTouchHits);
            if (count <= 0) continue;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = swordTouchHits[i];
                if (col == null) continue;
                if (col.transform.root == transform.root) continue;

                int key = col.GetInstanceID() ^ (s * 73856093);
                if (swordHitTimers.TryGetValue(key, out float timer) && timer > 0f)
                    continue;

                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                damageable.TakeDamage(swordDamage * swordDamageMultiplier);
                SoundManager.Instance?.PlaySfx(GameSfxId.SwordHit);
                swordHitTimers[key] = Mathf.Max(0.05f, swordTouchDamageInterval);
            }
        }
    }

    private void TickSwordHitTimers()
    {
        if (swordHitTimers.Count == 0) return;

        var keys = ListPoolKeys.Get();
        keys.AddRange(swordHitTimers.Keys);

        float dt = Time.deltaTime;
        for (int i = 0; i < keys.Count; i++)
        {
            int key = keys[i];
            if (!swordHitTimers.TryGetValue(key, out float timer))
                continue;

            timer -= dt;
            if (timer <= 0f)
                swordHitTimers.Remove(key);
            else
                swordHitTimers[key] = timer;
        }

        ListPoolKeys.Release(keys);
    }

    private static class ListPoolKeys
    {
        private static readonly Stack<List<int>> Pool = new Stack<List<int>>(4);

        public static List<int> Get()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<int>(64);
        }

        public static void Release(List<int> list)
        {
            list.Clear();
            Pool.Push(list);
        }
    }
}
