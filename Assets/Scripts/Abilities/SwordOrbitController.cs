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
    }

    [Header("Sword")]
    [SerializeField] private bool swordEnabled = true;
    [SerializeField] private float swordDamage = 2f;
    [SerializeField] private Transform swordOrbitRoot;
    [SerializeField] private Transform[] swordTransforms = new Transform[8];
    [SerializeField] private SwordDateBase swordDateBase;
    [SerializeField] private float swordOrbitSpeed = 180f;
    [SerializeField] private float swordTouchDamageInterval = 0.2f;
    [SerializeField] private LayerMask enemyMask;

    [Header("Sword Ability Tokens")]
    [SerializeField] private string[] swordAbilityTokens =
    {
        "Swordsman",
        "Berserker",
        "Paladin"
    };

    private readonly Collider2D[] swordTouchHits = new Collider2D[32];
    private readonly Dictionary<int, float> swordHitTimers = new Dictionary<int, float>(128);
    private readonly List<SwordOrbiter> swordOrbiters = new List<SwordOrbiter>(8);

    private float swordOrbitAngle;
    private int learnedAbilityCount;
    private int unlockedSwordCount;

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

        for (int i = 0; i < swordTransforms.Length; i++)
        {
            Transform tr = swordTransforms[i];
            if (tr == null) continue;

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
        // Swords are manually positioned in the scene.
    }

    public void HandleAbilityLearned(AbilityData ability)
    {
        ApplyNextSwordSkin();

        if (IsLegendarySwordAbility(ability))
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
        }

        learnedAbilityCount++;
    }

    private void AddExtraSword()
    {
        if (unlockedSwordCount >= swordOrbiters.Count) return;
        unlockedSwordCount++;
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
            collider = col
        };

        swordOrbiters.Add(orbiter);
    }

    private bool IsLegendarySwordAbility(AbilityData ability)
    {
        if (ability == null) return false;
        if (ability.rarity != AbilityData.AbilityRarity.Legendary) return false;
        if (string.IsNullOrWhiteSpace(ability.abilityName)) return false;

        for (int i = 0; i < swordAbilityTokens.Length; i++)
        {
            string token = swordAbilityTokens[i];
            if (string.IsNullOrWhiteSpace(token)) continue;

            if (ability.abilityName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
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

        swordOrbitAngle += swordOrbitSpeed * Time.deltaTime;
        if (swordOrbitAngle >= 360f)
            swordOrbitAngle -= 360f;

        if (swordOrbitRoot != null)
        {
            swordOrbitRoot.position = transform.position;
            swordOrbitRoot.rotation = Quaternion.Euler(0f, 0f, swordOrbitAngle);
        }

        DealSwordColliderContactDamage();
    }

    private void OnValidate()
    {
        if (swordTransforms == null) return;
        if (swordTransforms.Length == 8) return;
        Array.Resize(ref swordTransforms, 8);
    }

    private void DealSwordColliderContactDamage()
    {
        for (int s = 0; s < swordOrbiters.Count; s++)
        {
            var orbiter = swordOrbiters[s];
            if (orbiter == null || orbiter.collider == null) continue;
            if (!orbiter.transform.gameObject.activeInHierarchy) continue;

            ContactFilter2D filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = enemyMask.value == 0 ? ~0 : enemyMask.value,
                useTriggers = true
            };

            int count = orbiter.collider.Overlap(filter, swordTouchHits);
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

                damageable.TakeDamage(swordDamage);
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
