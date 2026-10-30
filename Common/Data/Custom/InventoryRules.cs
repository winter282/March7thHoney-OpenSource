namespace March7thHoney.Data.Custom;

/// <summary>
///     背包扣除的纯规则判定。放在 Common 是为了能被独立回归测试直接引用，
///     不依赖 PlayerInstance、数据库或网络。
/// </summary>
public static class InventoryRules
{
    /// <summary>
    ///     判断持有量是否足够扣除。<c>required &lt; 0</c> 视为非法输入，
    ///     <c>required == 0</c> 视为无需消耗；其余按 <paramref name="held" /> 判断。
    /// </summary>
    public static bool HasEnough(int held, int required)
    {
        return required >= 0 && held >= required;
    }

    /// <summary>
    ///     批量校验：<paramref name="required" /> 应按 itemId 聚合后再传入，
    ///     避免同一物品多个条目各自通过、合并后超扣。
    /// </summary>
    public static bool HasEnoughAll(IReadOnlyDictionary<int, int> required,
        IReadOnlyDictionary<int, int> held)
    {
        foreach (var (itemId, need) in required)
            if (!HasEnough(held.GetValueOrDefault(itemId), need))
                return false;

        return true;
    }
}