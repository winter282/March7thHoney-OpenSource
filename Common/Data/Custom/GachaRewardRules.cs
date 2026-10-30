using March7thHoney.Database.Avatar;

namespace March7thHoney.Data.Custom;

/// <summary>
///     抽卡奖励的纯规则判定。放在 Common 是为了能被独立回归测试直接引用，
///     不依赖 PlayerInstance、数据库或网络。
/// </summary>
public static class GachaRewardRules
{
    /// <summary>角色星魂满级。</summary>
    public const int MaxAvatarRank = 6;

    /// <summary>
    ///     判断抽到的已拥有角色是否已达满星（星魂 <see cref="MaxAvatarRank" /> 级）。
    ///     路径数据或星魂道具缺失时按 0 计：既避免 <c>int + null</c> 得到 null 后被误判为未满星，
    ///     也避免 <c>PathInfos[avatarId]</c> 缺键时抛 <see cref="KeyNotFoundException" />。
    /// </summary>
    public static bool IsAvatarAtMaxRank(BaseAvatarInfo? avatar, int avatarId, int extraRankCount)
    {
        var currentRank = avatar?.GetPathInfo(avatarId)?.Rank ?? 0;
        return currentRank + Math.Max(0, extraRankCount) >= MaxAvatarRank;
    }

    /// <summary>
    ///     三星光锥是否跳过入包。抽卡结果仍展示该光锥，但不写入背包、也不发 251 补偿。
    ///     <paramref name="type" /> 使用 <c>GetType</c> 的语义：1=角色，2=光锥。
    /// </summary>
    public static bool IsSkippedThreeStarLightCone(int rarity, int type)
    {
        return rarity == 3 && type == 2;
    }
}