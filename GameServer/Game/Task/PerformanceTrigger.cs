using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.Task;

public class PerformanceTrigger(PlayerInstance player)
{
    public PlayerInstance Player { get; } = player;

    public void TriggerPerformanceE(int performanceEId, SubMissionData subMission)
    {
        // LOCAL(suppress-story): 兜底拦截，避免其他调用方绕过 LevelTask 直接触发演出。
        if (ConfigManager.Config.ServerOption.SuppressStory) return;

        GameData.PerformanceEData.TryGetValue(performanceEId, out var excel);
        if (excel != null) TriggerPerformanceE(excel, subMission);
    }

    public void TriggerPerformanceE(PerformanceEExcel excel, SubMissionData subMission)
    {
        if (excel.ActInfo == null) return;
        foreach (var act in excel.ActInfo.OnInitSequece) Player.TaskManager?.LevelTask.TriggerInitAct(act, subMission);

        foreach (var act in excel.ActInfo.OnStartSequece)
            Player.TaskManager?.LevelTask.TriggerStartAct(act, subMission);
    }

    public void TriggerPerformanceD(int performanceDId, SubMissionData subMission)
    {
        // LOCAL(suppress-story): 兜底拦截，避免其他调用方绕过 LevelTask 直接触发演出。
        if (ConfigManager.Config.ServerOption.SuppressStory) return;

        GameData.PerformanceDData.TryGetValue(performanceDId, out var excel);
        if (excel != null) TriggerPerformanceD(excel, subMission);
    }

    public void TriggerPerformanceD(PerformanceDExcel excel, SubMissionData subMission)
    {
        if (excel.ActInfo == null) return;
        foreach (var act in excel.ActInfo.OnInitSequece) Player.TaskManager?.LevelTask.TriggerInitAct(act, subMission);

        foreach (var act in excel.ActInfo.OnStartSequece)
            Player.TaskManager?.LevelTask.TriggerStartAct(act, subMission);
    }
}
