using MemoryPack;
using Newtonsoft.Json;

namespace March7thHoney.Data.Custom;

/// <summary>
///     Config/Custom/MissionExclude.json —— 客户端任务表里不存在的 subMission id 黑名单。
///     Resource.bin 的生成源混入了 MissionBeta 数据，正式版客户端收到这些 id 的
///     SceneMissionInfo 会在 EnterMap 里抛 NRE，整条地图加载中断（ISSUE-022）。
/// </summary>
[MemoryPackable]
public partial class MissionExcludeConfig
{
    [JsonProperty("subMissionIds")] public List<int> SubMissionIds { get; set; } = [];

    // 兼容下划线写法，和 Config/Custom 里其它文件保持一致
    [JsonProperty("sub_mission_ids")]
    private List<int> SubMissionIdsSnake
    {
        set => SubMissionIds = value ?? [];
    }
}
