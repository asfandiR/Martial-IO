using System.Collections.Generic;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Renders active player effectors into a fixed amount of UI slots.
public class EffectorStatusUI : MonoBehaviour
{
    [System.Serializable]
    private sealed class SlotView
    {
        public GameObject root;
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text durationText;
    }

    private sealed class AggregatedStat
    {
        public EffectorSO.BoostedStat stat;
        public EffectorSO source;
        public float maxRemaining;
    }

    [SerializeField] private EffectorRuntimeController runtimeController;
    [SerializeField] private List<SlotView> slots = new List<SlotView>(5);

    private readonly List<EffectorRuntimeController.ActiveEffectorSnapshot> snapshots =
        new List<EffectorRuntimeController.ActiveEffectorSnapshot>(16);
    private readonly List<AggregatedStat> aggregated = new List<AggregatedStat>(5);
    private Coroutine refreshRoutine;

    private void Awake()
    {
        if (runtimeController == null)
            runtimeController = Object.FindFirstObjectByType<EffectorRuntimeController>();
    }

    private void OnEnable()
    {
        Refresh();
        if (refreshRoutine != null)
            StopCoroutine(refreshRoutine);
        refreshRoutine = StartCoroutine(RefreshLoop());
    }

    private void OnDisable()
    {
        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
    }

    private IEnumerator RefreshLoop()
    {
        var wait = new WaitForSeconds(0.2f);
        while (enabled && gameObject.activeInHierarchy)
        {
            Refresh();
            yield return wait;
        }

        refreshRoutine = null;
    }

    private void Refresh()
    {
        if (runtimeController == null)
            runtimeController = Object.FindFirstObjectByType<EffectorRuntimeController>();

        if (runtimeController == null)
        {
            HideAllSlots();
            return;
        }

        runtimeController.GetActiveEffectors(snapshots);
        BuildAggregatedList();
        Draw();
    }

    private void BuildAggregatedList()
    {
        aggregated.Clear();

        for (int i = 0; i < snapshots.Count; i++)
        {
            var current = snapshots[i];
            int existingIndex = IndexOfStat(current.stat);
            if (existingIndex < 0)
            {
                aggregated.Add(new AggregatedStat
                {
                    stat = current.stat,
                    source = current.source,
                    maxRemaining = current.remainingSeconds
                });
                continue;
            }

            AggregatedStat existing = aggregated[existingIndex];
            if (current.remainingSeconds > existing.maxRemaining)
            {
                existing.maxRemaining = current.remainingSeconds;
                if (current.source != null)
                    existing.source = current.source;
            }
        }

        aggregated.Sort((a, b) => a.stat.CompareTo(b.stat));
    }

    private int IndexOfStat(EffectorSO.BoostedStat stat)
    {
        for (int i = 0; i < aggregated.Count; i++)
        {
            if (aggregated[i].stat == stat)
                return i;
        }

        return -1;
    }

    private void Draw()
    {
        int shown = Mathf.Min(aggregated.Count, slots.Count);
        for (int i = 0; i < shown; i++)
        {
            SlotView slot = slots[i];
            AggregatedStat entry = aggregated[i];
            if (slot == null)
                continue;

            if (slot.root != null && !slot.root.activeSelf)
                slot.root.SetActive(true);

            if (slot.icon != null)
                slot.icon.sprite = entry.source != null ? entry.source.icon : null;

            if (slot.nameText != null)
                slot.nameText.text = GetStatLabel(entry.stat);

            if (slot.durationText != null)
                slot.durationText.text = FormatTime(entry.maxRemaining);
        }

        for (int i = shown; i < slots.Count; i++)
        {
            SlotView slot = slots[i];
            if (slot == null)
                continue;

            if (slot.root != null && slot.root.activeSelf)
                slot.root.SetActive(false);
        }
    }

    private void HideAllSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            SlotView slot = slots[i];
            if (slot == null || slot.root == null)
                continue;

            slot.root.SetActive(false);
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = total / 60;
        int secs = total % 60;
        return $"{minutes:00}:{secs:00}";
    }

    private static string GetStatLabel(EffectorSO.BoostedStat stat)
    {
        switch (stat)
        {
            case EffectorSO.BoostedStat.X2Damage:
                return "Double Damage";
            case EffectorSO.BoostedStat.HalfCooldown:
                return "Half Cooldown";
            case EffectorSO.BoostedStat.X2CritChance:
                return "Double Crit Chance";
            case EffectorSO.BoostedStat.Healing:
                return "Healing";
            case EffectorSO.BoostedStat.Shielding:
                return "Shielding";
            default:
                return stat.ToString();
        }
    }
}
