namespace March7thHoney.Data.Custom;

/// <summary>
///     星魂回退的纯规则判定。放在 Common 是为了能被独立回归测试直接引用，
///     不依赖 PlayerInstance、数据库或网络。
/// </summary>
public static class EidolonRollbackRules
{
    /// <summary>
    ///     目标星魂等级是否合法（0..6）。
    /// </summary>
    public static bool IsValidTarget(int targetRank)
    {
        return targetRank is >= 0 and <= GachaRewardRules.MaxAvatarRank;
    }

    /// <summary>
    ///     回退时应退还的星魂道具数量。只对降低等级生效；提升或持平返回 0。
    /// </summary>
    public static int CalculateRefund(int currentRank, int targetRank)
    {
        // 旧存档可能残留超过 6 的等级；退款数量上限按可支付的 6 级计算。
        var current = Math.Clamp(currentRank, 0, GachaRewardRules.MaxAvatarRank);
        var target = Math.Clamp(targetRank, 0, GachaRewardRules.MaxAvatarRank);
        if (target >= current) return 0;
        return current - target;
    }

    /// <summary>
    ///     星魂道具 ID。与发卡侧 <c>itemId + 10000</c> 的规则保持一致。
    /// </summary>
    public static int ResolveRankItemId(int pathId)
    {
        return pathId + 10000;
    }
}