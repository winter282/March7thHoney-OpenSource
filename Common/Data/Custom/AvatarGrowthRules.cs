namespace March7thHoney.Data.Custom;

/// <summary>
///     角色成长相关的纯规则判定。放在 Common 是为了能被独立回归测试直接引用，
///     不依赖 PlayerInstance、数据库或网络。
/// </summary>
public static class AvatarGrowthRules
{
    /// <summary>
    ///     当前星魂等级是否还能继续提升。
    ///     上限与抽卡奖励判定共用 <see cref="GachaRewardRules.MaxAvatarRank" />，避免两处常量分叉。
    /// </summary>
    public static bool CanRankUp(int currentRank)
    {
        return currentRank < GachaRewardRules.MaxAvatarRank;
    }
}