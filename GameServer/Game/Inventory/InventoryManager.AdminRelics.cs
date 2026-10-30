using March7thHoney.Data;
using March7thHoney.Data.Custom;
using March7thHoney.Database;
using March7thHoney.Database.Avatar;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums.Avatar;
using March7thHoney.Enums.Item;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.Inventory;

/// <summary>
///     LOCAL(add-relic-admin-api): 供本地 C# GUI 在线写入遗器/清空背包使用。
///     所有写操作都走当前 PlayerInstance，保证客户端能收到 PlayerSyncScNotify，且 MarkDirty 落盘。
/// </summary>
public partial class InventoryManager
{
    public sealed record RelicAdminAffix(int Id, int Count, int Step);

    public sealed record RelicAdminView(
        int UniqueId,
        int RelicId,
        int Level,
        int MainAffixId,
        int EquipAvatar,
        bool Locked,
        List<RelicAdminAffix> SubAffixes);

    public List<RelicAdminView> GetRelicViews()
    {
        return Data.RelicItems
            .OrderBy(x => x.UniqueId)
            .Select(ToView)
            .ToList();
    }

    public static List<RelicAdminView> GetRelicViewsOffline(int uid)
    {
        var inv = DatabaseHelper.Instance?.GetInstance<InventoryData>(uid);
        return inv == null
            ? []
            : inv.RelicItems.OrderBy(x => x.UniqueId).Select(ToView).ToList();
    }

    private static RelicAdminView ToView(ItemData item)
    {
        return new RelicAdminView(
            item.UniqueId,
            item.ItemId,
            item.Level,
            item.MainAffix,
            item.EquipAvatar,
            item.Locked,
            item.SubAffixes.Select(a => new RelicAdminAffix(a.Id, a.Count, a.Step)).ToList());
    }

    public async ValueTask<(Retcode, ItemData?)> CreateRelicFromAdmin(int relicId, int level, int mainAffixId,
        IReadOnlyList<RelicAdminAffix> subAffixes)
    {
        if (!RelicRules.CanCompose(Data.RelicItems.Count, 1, GameConstants.INVENTORY_MAX_RELIC))
            return (Retcode.RetRelicExceedLimit, null);

        var retcode = TryBuildRelicAffixes(relicId, level, mainAffixId, subAffixes, out var affixes);
        if (retcode != Retcode.RetSucc) return (retcode, null);

        var item = new ItemData
        {
            ItemId = relicId,
            Level = level,
            UniqueId = ++Data.NextUniqueId,
            MainAffix = mainAffixId,
            SubAffixes = affixes,
            Count = 1
        };

        await AddItem(item, false);
        return (Retcode.RetSucc, item);
    }

    public async ValueTask<Retcode> UpdateRelicFromAdmin(int uniqueId, int level, int mainAffixId,
        IReadOnlyList<RelicAdminAffix> subAffixes)
    {
        var relic = Data.RelicItems.FirstOrDefault(x => x.UniqueId == uniqueId);
        if (relic == null) return Retcode.RetRelicNotExist;

        var retcode = TryBuildRelicAffixes(relic.ItemId, level, mainAffixId, subAffixes, out var affixes);
        if (retcode != Retcode.RetSucc) return retcode;

        relic.Level = level;
        relic.MainAffix = mainAffixId;
        relic.SubAffixes = affixes;

        await Player.SendPacket(new PacketPlayerSyncScNotify(relic));
        MarkDirty();
        return Retcode.RetSucc;
    }

    public async ValueTask<int> ClearAllRelicsForAdmin()
    {
        var changed = new Dictionary<int, FormalAvatarInfo>();
        foreach (var avatar in Player.AvatarManager?.Data.FormalAvatars ?? [])
        {
            var avatarChanged = false;
            foreach (var path in avatar.PathInfos.Values)
            {
                if (path.Relic.Count == 0) continue;
                path.Relic.Clear();
                avatarChanged = true;
            }

            if (avatarChanged) changed[avatar.AvatarId] = avatar;
        }

        foreach (var relic in Data.RelicItems) relic.EquipAvatar = 0;

        var removed = Data.RelicItems.Select(relic =>
        {
            var clone = relic.Clone();
            clone.Count = 0;
            return clone;
        }).ToList();
        var count = Data.RelicItems.Count;
        Data.RelicItems.Clear();

        if (changed.Count > 0)
            await Player.SendPacket(new PacketPlayerSyncScNotify(changed.Values.ToList()));
        if (removed.Count > 0)
            await Player.SendPacket(new PacketPlayerSyncScNotify(removed));

        MarkDirty();
        return count;
    }

    public async ValueTask<int> ClearUnequippedThreeStarLightConesForAdmin()
    {
        var equipped = new HashSet<int>();
        foreach (var avatar in Player.AvatarManager?.Data.FormalAvatars ?? [])
            foreach (var path in avatar.PathInfos.Values)
            {
                if (path.EquipId != 0) equipped.Add(path.EquipId);
                if (path.EquipData is { UniqueId: > 0 }) equipped.Add(path.EquipData.UniqueId);
            }

        var targets = Data.EquipmentItems.Where(item =>
            item.EquipAvatar == 0
            && !equipped.Contains(item.UniqueId)
            && GameData.EquipmentConfigData.TryGetValue(item.ItemId, out var config)
            && config.Rarity == RarityEnum.CombatPowerLightconeRarity3).ToList();

        if (targets.Count == 0) return 0;

        var removed = targets.Select(item =>
        {
            var clone = item.Clone();
            clone.Count = 0;
            return clone;
        }).ToList();

        foreach (var item in targets) Data.EquipmentItems.Remove(item);

        await Player.SendPacket(new PacketPlayerSyncScNotify(removed));
        MarkDirty();
        return targets.Count;
    }

    public static (Retcode, ItemData?) CreateRelicOffline(int uid, int relicId, int level, int mainAffixId,
        IReadOnlyList<RelicAdminAffix> subAffixes)
    {
        var inv = DatabaseHelper.Instance?.GetInstance<InventoryData>(uid);
        if (inv == null) return (Retcode.RetItemNotExist, null);
        if (!RelicRules.CanCompose(inv.RelicItems.Count, 1, GameConstants.INVENTORY_MAX_RELIC))
            return (Retcode.RetRelicExceedLimit, null);

        var retcode = TryBuildRelicAffixes(relicId, level, mainAffixId, subAffixes, out var affixes);
        if (retcode != Retcode.RetSucc) return (retcode, null);

        var item = new ItemData
        {
            ItemId = relicId,
            Level = level,
            UniqueId = ++inv.NextUniqueId,
            MainAffix = mainAffixId,
            SubAffixes = affixes,
            Count = 1
        };
        inv.RelicItems.Add(item);
        DatabaseHelper.MarkDirty(uid);
        DatabaseHelper.Instance!.SaveDatabase();
        return (Retcode.RetSucc, item);
    }

    public static Retcode UpdateRelicOffline(int uid, int uniqueId, int level, int mainAffixId,
        IReadOnlyList<RelicAdminAffix> subAffixes)
    {
        var inv = DatabaseHelper.Instance?.GetInstance<InventoryData>(uid);
        var relic = inv?.RelicItems.FirstOrDefault(x => x.UniqueId == uniqueId);
        if (relic == null) return Retcode.RetRelicNotExist;

        var retcode = TryBuildRelicAffixes(relic.ItemId, level, mainAffixId, subAffixes, out var affixes);
        if (retcode != Retcode.RetSucc) return retcode;

        relic.Level = level;
        relic.MainAffix = mainAffixId;
        relic.SubAffixes = affixes;
        DatabaseHelper.MarkDirty(uid);
        DatabaseHelper.Instance!.SaveDatabase();
        return Retcode.RetSucc;
    }

    public static int ClearAllRelicsOffline(int uid)
    {
        var inv = DatabaseHelper.Instance?.GetInstance<InventoryData>(uid);
        if (inv == null) return 0;

        var avatars = DatabaseHelper.Instance?.GetInstance<AvatarData>(uid);
        if (avatars != null)
            foreach (var avatar in avatars.FormalAvatars)
                foreach (var path in avatar.PathInfos.Values)
                    path.Relic.Clear();

        foreach (var relic in inv.RelicItems) relic.EquipAvatar = 0;
        var count = inv.RelicItems.Count;
        inv.RelicItems.Clear();
        DatabaseHelper.MarkDirty(uid);
        DatabaseHelper.Instance!.SaveDatabase();
        return count;
    }

    public static int ClearUnequippedThreeStarLightConesOffline(int uid)
    {
        var inv = DatabaseHelper.Instance?.GetInstance<InventoryData>(uid);
        if (inv == null) return 0;

        var equipped = new HashSet<int>();
        var avatars = DatabaseHelper.Instance?.GetInstance<AvatarData>(uid);
        if (avatars != null)
            foreach (var avatar in avatars.FormalAvatars)
                foreach (var path in avatar.PathInfos.Values)
                {
                    if (path.EquipId != 0) equipped.Add(path.EquipId);
                    if (path.EquipData is { UniqueId: > 0 }) equipped.Add(path.EquipData.UniqueId);
                }

        var targets = inv.EquipmentItems.Where(item =>
            item.EquipAvatar == 0
            && !equipped.Contains(item.UniqueId)
            && GameData.EquipmentConfigData.TryGetValue(item.ItemId, out var config)
            && config.Rarity == RarityEnum.CombatPowerLightconeRarity3).ToList();

        foreach (var item in targets) inv.EquipmentItems.Remove(item);
        if (targets.Count == 0) return 0;

        DatabaseHelper.MarkDirty(uid);
        DatabaseHelper.Instance!.SaveDatabase();
        return targets.Count;
    }

    public static Retcode TryBuildRelicAffixes(int relicId, int level, int mainAffixId,
        IReadOnlyList<RelicAdminAffix> subAffixes, out List<ItemSubAffix> result)
    {
        result = [];

        if (!GameData.RelicConfigData.TryGetValue(relicId, out var relicConfig))
            return Retcode.RetItemConfigNotExist;
        if (level < 0 || level > relicConfig.MaxLevel)
            return Retcode.RetReqParaInvalid;
        if (!GameData.RelicMainAffixData.TryGetValue(relicConfig.MainAffixGroup, out var mainGroup) ||
            !mainGroup.TryGetValue(mainAffixId, out var mainAffix))
            return Retcode.RetReqParaInvalid;
        if (!GameData.RelicSubAffixData.TryGetValue(relicConfig.SubAffixGroup, out var subGroup))
            return Retcode.RetItemConfigNotExist;

        if (subAffixes.Count is < 1 or > 4) return Retcode.RetReqParaInvalid;

        var seenIds = new HashSet<int>();
        var seenProps = new HashSet<AvatarPropertyTypeEnum>();
        foreach (var affix in subAffixes)
        {
            if (!subGroup.TryGetValue(affix.Id, out var config)) return Retcode.RetReqParaInvalid;
            if (!seenIds.Add(affix.Id)) return Retcode.RetReqParaInvalid;
            if (config.Property == mainAffix.Property) return Retcode.RetReqParaInvalid;
            if (!seenProps.Add(config.Property)) return Retcode.RetReqParaInvalid;

            if (affix.Count is < 1 or > 6) return Retcode.RetReqParaInvalid;
            if (affix.Step < 0 || affix.Step > config.StepNum * affix.Count)
                return Retcode.RetReqParaInvalid;

            result.Add(new ItemSubAffix
            {
                Id = affix.Id,
                Count = affix.Count,
                Step = affix.Step
            });
        }

        return Retcode.RetSucc;
    }
}