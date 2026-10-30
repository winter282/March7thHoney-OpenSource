using March7thHoney.Data;
using March7thHoney.Data.Custom;
using March7thHoney.Database;
using March7thHoney.Enums.Avatar;
using March7thHoney.Enums.Item;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Internationalization;
using March7thHoney.Proto;

namespace March7thHoney.Command.Command.Cmd;

[CommandInfo("avatar", "Game.Command.Avatar.Desc", "Game.Command.Avatar.Usage", ["av", "ava"], permission: CommandPermissions.Avatar)]
public class CommandAvatar : ICommand
{
    [CommandMethod("talent")]
    public async ValueTask SetTalent(CommandArg arg)
    {
        if (arg.Target == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }

        if (arg.BasicArgs.Count < 2)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        // change basic type
        var avatarId = arg.GetInt(0);
        var level = arg.GetInt(1);
        if (level is < 0 or > 10)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.InvalidLevel",
                I18NManager.Translate("Word.Talent")));
            return;
        }

        var player = arg.Target.Player!;
        if (avatarId == -1)
        {
            foreach (var path in player.AvatarManager!.Data.FormalAvatars.SelectMany(avatarInfo =>
                         avatarInfo.PathInfos))
            {
                if (!GameData.AvatarConfigData.TryGetValue(path.Key, out var pathExcel)) continue;
                foreach (var talent in pathExcel.SkillTree.GetValueOrDefault(path.Value.EnhanceId, []))
                    path.Value.GetSkillTree()[talent.PointID] = Math.Min(level, talent.MaxLevel);
            }

            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AllAvatarsLevelSet",
                I18NManager.Translate("Word.Talent"), level.ToString()));

            // sync
            await player.SendPacket(new PacketPlayerSyncScNotify(player.AvatarManager.Data.FormalAvatars));

            return;
        }

        if (GameData.MultiplePathAvatarConfigData.TryGetValue(avatarId, out var multiple))
            avatarId = multiple.BaseAvatarID;

        var avatar = player.AvatarManager!.GetFormalAvatar(avatarId);
        if (avatar == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
            return;
        }

        var avatarPathInfo = avatar.PathInfos.FirstOrDefault(x => x.Key == (multiple?.AvatarID ?? avatarId));
        if (avatarPathInfo.Value == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
            return;
        }

        GameData.AvatarConfigData.TryGetValue(avatarPathInfo.Key, out var excel);
        if (excel == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
            return;
        }

        foreach (var talent in excel.SkillTree.GetValueOrDefault(avatarPathInfo.Value.EnhanceId, []))
            avatarPathInfo.Value.GetSkillTree()[talent.PointID] = Math.Min(level, talent.MaxLevel);

        // sync
        await player.SendPacket(new PacketPlayerSyncScNotify(avatar));

        await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarLevelSet",
            excel.Name?.Replace("{NICKNAME}", player.Data.Name) ?? avatarId.ToString(),
            I18NManager.Translate("Word.Talent"), level.ToString()));
    }

    [CommandMethod("get")]
    public async ValueTask GetAvatar(CommandArg arg)
    {
        if (arg.Target == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }

        if (arg.BasicArgs.Count < 1)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var id = arg.GetInt(0);
        var excel = await arg.Target.Player!.AvatarManager!.AddAvatar(id);

        if (excel == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarFailedGet", id.ToString()));
            return;
        }

        await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarGet", excel.Name ?? id.ToString()));
    }

    [CommandMethod("rank")]
    public async ValueTask SetRank(CommandArg arg)
    {
        if (arg.Target == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }

        if (arg.BasicArgs.Count < 2)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var id = arg.GetInt(0);
        var rank = arg.GetInt(1);
        if (!EidolonRollbackRules.IsValidTarget(rank))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.InvalidLevel",
                I18NManager.Translate("Word.Rank")));
            return;
        }

        if (id == -1)
        {
            arg.Target.Player!.AvatarManager!.Data.FormalAvatars.ForEach(avatar =>
            {
                foreach (var path in avatar.PathInfos.Values) path.Rank = Math.Min(rank, 6);
            });
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AllAvatarsLevelSet",
                I18NManager.Translate("Word.Rank"), rank.ToString()));

            // sync
            await arg.Target.SendPacket(
                new PacketPlayerSyncScNotify(arg.Target.Player!.AvatarManager.Data.FormalAvatars));
            DatabaseHelper.MarkDirty(arg.Target.Player!.Uid);
        }
        else
        {
            var player = arg.Target.Player!;
            if (GameData.MultiplePathAvatarConfigData.TryGetValue(id, out var multiple)) id = multiple.BaseAvatarID;

            var avatar = player.AvatarManager!.GetFormalAvatar(id);
            if (avatar == null)
            {
                await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
                return;
            }

            var avatarPathInfo = avatar.PathInfos.FirstOrDefault(x => x.Key == (multiple?.AvatarID ?? id));
            if (avatarPathInfo.Value == null)
            {
                await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
                return;
            }

            // LOCAL(add-eidolon-rollback): 降低星魂时按差值退还星魂道具；提升仍为 GM 调试语义。
            var oldRank = avatarPathInfo.Value.Rank;
            var refund = EidolonRollbackRules.CalculateRefund(oldRank, rank);
            if (refund > 0)
            {
                var rankItemId = EidolonRollbackRules.ResolveRankItemId(avatarPathInfo.Key);
                if (!GameData.ItemConfigData.TryGetValue(rankItemId, out var rankItemConfig) ||
                    (rankItemConfig.ItemSubType != ItemSubTypeEnum.Eidolon &&
                     rankItemConfig.ItemSubType != ItemSubTypeEnum.AvatarRank))
                {
                    await arg.SendMsg($"星魂回退失败：找不到道具 {rankItemId} 的星魂配置");
                    return;
                }

                if (await player.InventoryManager!.AddItem(rankItemId, refund) == null)
                {
                    await arg.SendMsg("星魂回退失败：星魂道具返还写入失败");
                    return;
                }
            }

            avatarPathInfo.Value.Rank = rank;

            // sync
            await arg.Target.SendPacket(new PacketPlayerSyncScNotify(avatar));
            DatabaseHelper.MarkDirty(player.Uid);

            GameData.AvatarConfigData.TryGetValue(id, out var avatarExcel);
            var avatarName = avatarExcel?.Name?.Replace("{NICKNAME}", player.Data.Name) ?? id.ToString();
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarLevelSet",
                avatarName, I18NManager.Translate("Word.Rank"), rank.ToString()));

            if (refund > 0)
                await arg.SendMsg($"星魂回退：{avatarName} {oldRank} -> {rank}，退还 {refund} 个 {avatarPathInfo.Key + 10000}");
        }
    }

    [CommandMethod("level")]
    public async ValueTask SetLevel(CommandArg arg)
    {
        if (arg.Target == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }

        if (arg.BasicArgs.Count < 2)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var id = arg.GetInt(0);
        var level = arg.GetInt(1);
        if (level is < 1 or > 80)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.InvalidLevel",
                I18NManager.Translate("Word.Avatar")));
            return;
        }

        if (id == -1)
        {
            arg.Target.Player!.AvatarManager!.Data.FormalAvatars.ForEach(avatar =>
            {
                avatar.Level = Math.Min(level, 80);
                avatar.Promotion = GameData.GetMinPromotionForLevel(avatar.Level);
            });
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AllAvatarsLevelSet",
                I18NManager.Translate("Word.Avatar"), level.ToString()));

            // sync
            await arg.Target.SendPacket(
                new PacketPlayerSyncScNotify(arg.Target.Player!.AvatarManager.Data.FormalAvatars));
        }
        else
        {
            var avatar = arg.Target.Player!.AvatarManager!.GetFormalAvatar(id);
            if (avatar == null)
            {
                await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
                return;
            }

            avatar.Level = Math.Min(level, 80);
            avatar.Promotion = GameData.GetMinPromotionForLevel(avatar.Level);

            // sync
            await arg.Target.SendPacket(new PacketPlayerSyncScNotify(avatar));

            GameData.AvatarConfigData.TryGetValue(id, out var avatarExcel);
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarLevelSet",
                avatarExcel?.Name?.Replace("{NICKNAME}", arg.Target.Player!.Data.Name) ?? id.ToString(),
                I18NManager.Translate("Word.Avatar"), level.ToString()));
        }
    }

    [CommandMethod("path")]
    public async ValueTask SetPath(CommandArg arg)
    {
        if (arg.Target == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.PlayerNotFound"));
            return;
        }

        if (arg.BasicArgs.Count < 2)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Notice.InvalidArguments"));
            return;
        }

        var avatarId = arg.GetInt(0);
        var pathId = arg.GetInt(1);

        var avatar = arg.Target.Player!.AvatarManager!.GetFormalAvatar(avatarId);
        if (avatar == null)
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
            return;
        }

        if (!GameData.MultiplePathAvatarConfigData.ContainsKey(pathId))
        {
            await arg.SendMsg(I18NManager.Translate("Game.Command.Avatar.AvatarNotFound"));
            return;
        }

        await arg.Target.Player.ChangeAvatarPathType(avatarId, (MultiPathAvatarTypeEnum)pathId);
        await arg.Target.SendPacket(new PacketAvatarPathChangedNotify((uint)avatarId, (MultiPathAvatarType)pathId));
        await arg.Target.SendPacket(new PacketPlayerSyncScNotify(avatar));
    }
}
