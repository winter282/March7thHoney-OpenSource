namespace March7thHoney.Data.Custom;

/// <summary>
///     遗器合成、强化、重掷、装备的纯规则判定。放在 Common 是为了能被独立回归测试直接引用，
///     不依赖 PlayerInstance、数据库或网络。
/// </summary>
public static class RelicRules
{
    /// <summary>
    ///     按 itemId 聚合成本。非正 itemId 或非正数量视为无效条目并跳过。
    /// </summary>
    public static Dictionary<int, int> AggregateCosts(IEnumerable<(int ItemId, int Count)> costs)
    {
        var result = new Dictionary<int, int>();
        foreach (var (itemId, count) in costs)
        {
            if (itemId <= 0 || count <= 0) continue;
            result[itemId] = result.GetValueOrDefault(itemId) + count;
        }

        return result;
    }

    /// <summary>
    ///     材料与信用点是否都足够。材料按 itemId 聚合后传入。
    /// </summary>
    public static bool CanAfford(IReadOnlyDictionary<int, int> requiredMaterials,
        IReadOnlyDictionary<int, int> heldMaterials, int requiredCoin, int heldCoin)
    {
        return InventoryRules.HasEnoughAll(requiredMaterials, heldMaterials)
               && InventoryRules.HasEnough(heldCoin, requiredCoin);
    }

    /// <summary>
    ///     遗器槽位是否合法。1..6 对应 HEAD/HAND/BODY/FOOT/NECK/OBJECT。
    /// </summary>
    public static bool IsValidSlot(int slot)
    {
        return slot is >= 1 and <= 6;
    }

    /// <summary>
    ///     遗器部位与请求槽位是否一致。
    /// </summary>
    public static bool MatchesSlot(int relicType, int slot)
    {
        return relicType == slot;
    }

    /// <summary>
    ///     背包剩余空间能否容纳本次合成数量。
    /// </summary>
    public static bool CanCompose(int currentRelicCount, int addCount, int maxRelicCount)
    {
        // 减法写法避免 current + add 溢出绕过上限。
        return addCount > 0
               && currentRelicCount >= 0
               && addCount <= maxRelicCount
               && currentRelicCount <= maxRelicCount - addCount;
    }

    /// <summary>
    ///     为一次重掷挑选新的副词条 id：排除被屏蔽的词条，去重，数量尽量等于 <paramref name="count" />。
    ///     候选池不足时返回能选到的全部，不补重复项。
    /// </summary>
    public static IReadOnlyList<int> BuildReforgeAffixIds(IReadOnlyList<int> candidateAffixIds,
        int count, int blockedAffixId, Random random)
    {
        if (count <= 0) return [];

        var pool = candidateAffixIds
            .Where(id => id != blockedAffixId)
            .Distinct()
            .ToList();

        var picked = new List<int>(Math.Min(count, pool.Count));
        while (picked.Count < count && pool.Count > 0)
        {
            var index = random.Next(pool.Count);
            picked.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return picked;
    }

    /// <summary>
    ///     把 <paramref name="totalUpgrades" /> 次副词条升级随机分配到 <paramref name="affixCount" /> 条词条上。
    ///     返回值只包含额外升级次数，0 表示该词条保持 1 层。
    /// </summary>
    public static int[] DistributeUpgrades(int totalUpgrades, int affixCount, Random random)
    {
        if (affixCount <= 0) return [];

        var result = new int[affixCount];
        if (totalUpgrades <= 0) return result;

        for (var i = 0; i < totalUpgrades; i++)
            result[random.Next(affixCount)]++;

        return result;
    }
}