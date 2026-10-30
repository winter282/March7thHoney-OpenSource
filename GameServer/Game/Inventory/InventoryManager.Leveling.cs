using System.Collections.Frozen;
using March7thHoney.Data;
using March7thHoney.Data.Custom;
using March7thHoney.Data.Excel;
using March7thHoney.Database;
using March7thHoney.Database.Avatar;
using March7thHoney.Database.Friend;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums.Item;
using March7thHoney.Enums.Mission;
using March7thHoney.GameServer.Game.Player;
using March7thHoney.GameServer.Game.Scene;
using March7thHoney.GameServer.Game.Sync;
using March7thHoney.GameServer.Server.Packet.Send.Avatar;
using March7thHoney.GameServer.Server.Packet.Send.Lineup;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.GameServer.Server.Packet.Send.Scene;
using March7thHoney.Proto;
using March7thHoney.Util;
using Google.Protobuf.Collections;

namespace March7thHoney.GameServer.Game.Inventory;

public partial class InventoryManager
{
    #region Equip

    public async ValueTask EquipAvatar(int avatarId, int equipmentUniqueId)
    {
        var itemData = Data.EquipmentItems.Find(x => x.UniqueId == equipmentUniqueId);
        var avatarData = Player.AvatarManager!.GetFormalAvatar(avatarId);
        if (itemData == null || avatarData == null) return;
        var oldItem = Data.EquipmentItems.Find(x => x.UniqueId == avatarData.PathInfos[avatarId].EquipId);
        if (itemData.EquipAvatar > 0) // already be dressed
        {
            var equipAvatarId = itemData.EquipAvatar;
            var equipAvatar = Player.AvatarManager.GetFormalAvatar(equipAvatarId);
            if (equipAvatar != null && oldItem != null)
            {
                // switch
                equipAvatar.PathInfos[equipAvatarId].EquipId = oldItem.UniqueId;
                oldItem.EquipAvatar = equipAvatar.AvatarId;
                await Player.SendPacket(new PacketPlayerSyncScNotify(new AvatarSyncData(equipAvatar), new ItemSyncData(oldItem)));
            }
            else if (equipAvatar != null && oldItem == null)
            {
                equipAvatar.PathInfos[equipAvatarId].EquipId = 0;
                await Player.SendPacket(new PacketPlayerSyncScNotify(equipAvatar));
            }
        }
        else
        {
            if (oldItem != null)
            {
                oldItem.EquipAvatar = 0;
                await Player.SendPacket(new PacketPlayerSyncScNotify(oldItem));
            }
        }

        itemData.EquipAvatar = avatarData.AvatarId;
        avatarData.PathInfos[avatarId].EquipId = itemData.UniqueId;
        await Player.SendPacket(new PacketPlayerSyncScNotify(new AvatarSyncData(avatarData), new ItemSyncData(itemData)));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 换装必须显式标脏
    }

    // LOCAL(fix-relic-operations): 优先精确 pathId；多命途角色按 MultiplePathAvatarConfig 映射，
    // 不做「唯一路径」猜测，避免把遗器写进请求之外的命途。
    private static PathInfo? ResolvePathInfo(FormalAvatarInfo avatar, int pathId)
    {
        if (avatar.PathInfos.TryGetValue(pathId, out var exact)) return exact;

        if (GameData.MultiplePathAvatarConfigData.TryGetValue(pathId, out var multiple))
        {
            if (avatar.PathInfos.TryGetValue(multiple.AvatarID, out var byPath)) return byPath;
            if (avatar.PathInfos.TryGetValue(multiple.BaseAvatarID, out var byBase)) return byBase;
        }

        return null;
    }

    public async ValueTask<Retcode> EquipRelic(int avatarId, int relicUniqueId, int slot)
    {
        if (!RelicRules.IsValidSlot(slot)) return Retcode.RetReqParaInvalid;
        // 客户端用 0 表示该槽位空/不处理，保持旧行为：静默成功。
        if (relicUniqueId == 0) return Retcode.RetSucc;

        var itemData = Data.RelicItems.Find(x => x.UniqueId == relicUniqueId);
        if (itemData == null) return Retcode.RetRelicNotExist;

        if (!GameData.RelicConfigData.TryGetValue(itemData.ItemId, out var relicConfig))
            return Retcode.RetItemConfigNotExist;
        if (!RelicRules.MatchesSlot((int)relicConfig.Type, slot))
            return Retcode.RetReqParaInvalid;

        var avatarData = Player.AvatarManager!.GetFormalAvatar(avatarId);
        if (avatarData == null) return Retcode.RetAvatarNotExist;

        var pathInfo = ResolvePathInfo(avatarData, avatarId);
        if (pathInfo == null) return Retcode.RetReqParaInvalid;

        pathInfo.Relic.TryGetValue(slot, out var oldUniqueId);
        var oldItem = Data.RelicItems.Find(x => x.UniqueId == oldUniqueId);

        // 先把「原穿戴者」与它的路径解析完，解析失败就不做任何修改，避免留下孤儿遗器。
        FormalAvatarInfo? owner = null;
        PathInfo? ownerPath = null;
        if (itemData.EquipAvatar > 0 && itemData.EquipAvatar != avatarData.AvatarId)
        {
            owner = Player.AvatarManager!.GetFormalAvatar(itemData.EquipAvatar);
            if (owner != null)
            {
                ownerPath = ResolvePathInfo(owner, itemData.EquipAvatar);
                if (ownerPath == null) return Retcode.RetReqParaInvalid;
            }
        }

        // 同一遗器不能同时占用同一角色的两个槽位。
        foreach (var key in pathInfo.Relic
                     .Where(kv => kv.Value == relicUniqueId && kv.Key != slot)
                     .Select(kv => kv.Key).ToList())
            pathInfo.Relic.Remove(key);

        if (owner != null)
        {
            foreach (var path in owner.PathInfos.Values)
                foreach (var key in path.Relic
                             .Where(kv => kv.Value == itemData.UniqueId)
                             .Select(kv => kv.Key).ToList())
                    path.Relic.Remove(key);

            if (oldItem != null)
            {
                ownerPath!.Relic[slot] = oldItem.UniqueId;
                oldItem.EquipAvatar = owner.AvatarId;
            }

            await Player.SendPacket(new PacketPlayerSyncScNotify(owner));
        }
        else if (oldItem != null)
        {
            oldItem.EquipAvatar = 0;
            await Player.SendPacket(new PacketPlayerSyncScNotify(oldItem));
        }

        itemData.EquipAvatar = avatarData.AvatarId;
        pathInfo.Relic[slot] = itemData.UniqueId;
        await Player.SendPacket(new PacketPlayerSyncScNotify(new AvatarSyncData(avatarData), new ItemSyncData(itemData)));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 换装必须显式标脏

        return Retcode.RetSucc;
    }

    public async ValueTask<Retcode> UnequipRelic(int avatarId, int slot)
    {
        if (!RelicRules.IsValidSlot(slot)) return Retcode.RetReqParaInvalid;

        var avatarData = Player.AvatarManager!.GetFormalAvatar(avatarId);
        if (avatarData == null) return Retcode.RetAvatarNotExist;

        var pathInfo = ResolvePathInfo(avatarData, avatarId);
        if (pathInfo == null) return Retcode.RetReqParaInvalid;

        if (!pathInfo.Relic.TryGetValue(slot, out var uniqueId)) return Retcode.RetSucc;

        pathInfo.Relic.Remove(slot);
        var itemData = Data.RelicItems.Find(x => x.UniqueId == uniqueId);
        if (itemData != null) itemData.EquipAvatar = 0;

        await Player.SendPacket(itemData != null
            ? new PacketPlayerSyncScNotify(new AvatarSyncData(avatarData), new ItemSyncData(itemData))
            : new PacketPlayerSyncScNotify(new AvatarSyncData(avatarData)));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 卸下必须显式标脏

        return Retcode.RetSucc;
    }

    public async ValueTask UnequipEquipment(int avatarId)
    {
        var avatarData = Player.AvatarManager!.GetFormalAvatar(avatarId);
        if (avatarData == null) return;
        var pathInfo = avatarData.PathInfos[avatarId];
        var itemData = Data.EquipmentItems.Find(x => x.UniqueId == pathInfo.EquipId);
        if (itemData == null) return;
        itemData.EquipAvatar = 0;
        pathInfo.EquipId = 0;
        await Player.SendPacket(new PacketPlayerSyncScNotify(new AvatarSyncData(avatarData), new ItemSyncData(itemData)));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 卸下必须显式标脏
    }

    public async ValueTask<List<ItemData>> LevelUpAvatar(int baseAvatarId, ItemCostData item)
    {
        var avatarData = Player.AvatarManager!.GetFormalAvatar(baseAvatarId);
        if (avatarData == null) return [];
        GameData.AvatarConfigData.TryGetValue(avatarData.AvatarId, out var avatarConfig);
        if (avatarConfig == null) return [];

        GameData.AvatarPromotionConfigData.TryGetValue(avatarData.AvatarId * 10 + avatarData.Promotion,
            out var promotionConfig);
        if (promotionConfig == null) return [];
        var exp = 0;

        foreach (var cost in item.ItemList)
        {
            GameData.ItemConfigData.TryGetValue((int)cost.PileItem.ItemId, out var itemConfig);
            if (itemConfig == null) continue;
            exp += itemConfig.Exp * (int)cost.PileItem.ItemNum;
        }

        // payment
        var costScoin = exp / 10;
        if (Player.Data.Scoin < costScoin) return [];
        foreach (var cost in item.ItemList) await RemoveItem((int)cost.PileItem.ItemId, (int)cost.PileItem.ItemNum);
        await RemoveItem(2, costScoin);

        var maxLevel = promotionConfig.MaxLevel;
        var curExp = avatarData.Exp;
        var curLevel = avatarData.Level;
        var nextLevelExp = GameData.GetAvatarExpRequired(avatarConfig.ExpGroup, avatarData.Level);
        do
        {
            int toGain;
            if (curExp + exp >= nextLevelExp)
                toGain = nextLevelExp - curExp;
            else
                toGain = exp;
            curExp += toGain;
            exp -= toGain;
            // level up
            if (curExp >= nextLevelExp)
            {
                curExp = 0;
                curLevel++;
                nextLevelExp = GameData.GetAvatarExpRequired(avatarConfig.ExpGroup, curLevel);
            }
        } while (exp > 0 && nextLevelExp > 0 && curLevel < maxLevel);

        avatarData.Level = curLevel;
        avatarData.Exp = curExp;
        // leftover
        Dictionary<int, ItemData> list = [];
        var leftover = exp;
        while (leftover > 0)
        {
            var gain = false;
            // LOCAL(fix-avatar-growth-bounds): 角色溢出经验必须用角色经验材料表返还，不能用光锥表
            foreach (var expItem in GameData.AvatarExpItemConfigData.Values.Reverse())
                if (leftover >= expItem.Exp)
                {
                    // add
                    await PutItem(expItem.ItemID, 1);
                    if (list.TryGetValue(expItem.ItemID, out var i))
                    {
                        i.Count++;
                    }
                    else
                    {
                        i = new ItemData
                        {
                            ItemId = expItem.ItemID,
                            Count = 1
                        };
                        list[expItem.ItemID] = i;
                    }

                    leftover -= expItem.Exp;
                    gain = true;
                    break;
                }

            if (!gain) break; // no more item
        }

        if (list.Count > 0) await Player.SendPacket(new PacketPlayerSyncScNotify(list.Values.ToList()));
        await Player.SendPacket(new PacketPlayerSyncScNotify(avatarData));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 角色升级必须显式标脏
        return [.. list.Values];
    }

    #endregion

    #region Levelup

    private async ValueTask SyncStaleEquipmentIds(IEnumerable<int> equipmentIds)
    {
        var ids = equipmentIds.Where(id => id > 0).Select(id => (uint)id).Distinct().ToList();
        if (ids.Count > 0) await Player.SendPacket(new PacketPlayerSyncScNotify(ids));
    }

    private async ValueTask SyncStaleEquipmentIds(IEnumerable<uint> equipmentIds)
    {
        var ids = equipmentIds.Where(id => id > 0).Distinct().ToList();
        if (ids.Count > 0) await Player.SendPacket(new PacketPlayerSyncScNotify(ids));
    }

    public async ValueTask<(Retcode retcode, List<ItemData> returnItems)> LevelUpEquipment(int equipmentUniqueId,
        ItemCostData item)
    {
        var itemData = Data.EquipmentItems.Find(x => x.UniqueId == equipmentUniqueId);
        if (itemData == null)
        {
            await SyncStaleEquipmentIds([equipmentUniqueId]);
            return (Retcode.RetEquipmentNotExist, []);
        }

        GameData.EquipmentPromotionConfigData.TryGetValue(itemData.ItemId * 10 + itemData.Promotion,
            out var equipmentPromotionConfig);
        GameData.EquipmentConfigData.TryGetValue(itemData.ItemId, out var equipmentConfig);
        if (equipmentConfig == null || equipmentPromotionConfig == null) return (Retcode.RetItemConfigNotExist, []);
        if (itemData.Level >= equipmentPromotionConfig.MaxLevel) return (Retcode.RetEquipmentLevelReachMax, []);

        var exp = 0;
        List<(int itemId, int count, int uniqueId)> costItems = [];
        HashSet<uint> costEquipmentIds = [];
        List<uint> staleEquipmentIds = [];

        foreach (var cost in item.ItemList)
            if (cost.EquipmentUniqueId != 0)
            {
                if (cost.EquipmentUniqueId == equipmentUniqueId)
                    return (Retcode.RetEquipmentConsumeSelf, []);
                if (!costEquipmentIds.Add(cost.EquipmentUniqueId)) continue;

                var costItem = Data.EquipmentItems.Find(x => x.UniqueId == cost.EquipmentUniqueId);
                if (costItem == null)
                {
                    staleEquipmentIds.Add(cost.EquipmentUniqueId);
                    continue;
                }

                if (costItem.Locked) return (Retcode.RetEquipmentLocked, []);

                exp += costItem.CalcTotalEquipmentExpGained();
                costItems.Add((costItem.ItemId, 1, (int)cost.EquipmentUniqueId));
            }
            else if (cost.PileItem != null)
            {
                GameData.EquipmentExpItemConfigData.TryGetValue((int)cost.PileItem.ItemId, out var itemConfig);
                if (itemConfig == null) continue;
                exp += itemConfig.ExpProvide * (int)cost.PileItem.ItemNum;
                costItems.Add(((int)cost.PileItem.ItemId, (int)cost.PileItem.ItemNum, 0));
            }

        // payment
        if (staleEquipmentIds.Count > 0)
        {
            await SyncStaleEquipmentIds(staleEquipmentIds);
            return (Retcode.RetEquipmentNotExist, []);
        }

        if (exp <= 0 || costItems.Count == 0) return (Retcode.RetItemNoCost, []);

        var costScoin = exp / 2;
        if (Player.Data.Scoin < costScoin) return (Retcode.RetScoinNotEnough, []);
        await RemoveItems(costItems);

        await RemoveItem(2, costScoin);

        var maxLevel = equipmentPromotionConfig.MaxLevel;
        var curExp = itemData.Exp;
        var curLevel = itemData.Level;
        var nextLevelExp = GameData.GetEquipmentExpRequired(equipmentConfig.ExpType, itemData.Level);
        do
        {
            int toGain;
            if (curExp + exp >= nextLevelExp)
                toGain = nextLevelExp - curExp;
            else
                toGain = exp;
            curExp += toGain;
            exp -= toGain;
            // level up
            if (curExp >= nextLevelExp)
            {
                curExp = 0;
                curLevel++;
                nextLevelExp = GameData.GetEquipmentExpRequired(equipmentConfig.ExpType, curLevel);
            }
        } while (exp > 0 && nextLevelExp > 0 && curLevel < maxLevel);

        itemData.Level = curLevel;
        itemData.Exp = curExp;
        MarkDirty();
        // leftover
        Dictionary<int, ItemData> list = [];
        var leftover = exp;
        while (leftover > 0)
        {
            var gain = false;
            foreach (var expItem in GameData.EquipmentExpItemConfigData.Values.Reverse())
                if (leftover >= expItem.ExpProvide)
                {
                    // add
                    await PutItem(expItem.ItemID, 1);
                    if (list.TryGetValue(expItem.ItemID, out var i))
                    {
                        i.Count++;
                    }
                    else
                    {
                        i = new ItemData
                        {
                            ItemId = expItem.ItemID,
                            Count = 1
                        };
                        list[expItem.ItemID] = i;
                    }

                    leftover -= expItem.ExpProvide;
                    gain = true;
                    break;
                }

            if (!gain) break; // no more item
        }

        if (list.Count > 0) await Player.SendPacket(new PacketPlayerSyncScNotify(list.Values.ToList()));
        await Player.SendPacket(new PacketPlayerSyncScNotify(itemData));
        return (Retcode.RetSucc, [.. list.Values]);
    }

    public async ValueTask<bool> PromoteAvatar(int avatarId)
    {
        // Get avatar
        var avatarData = Player.AvatarManager!.GetFormalAvatar(avatarId);
        if (avatarData == null) return false;

        GameData.AvatarConfigData.TryGetValue(avatarId, out var avatarConfig);
        if (avatarConfig == null ||
            avatarData.Promotion >= avatarConfig.MaxPromotion) return false;

        // Get promotion data
        var promotion =
            GameData.AvatarPromotionConfigData.Values.FirstOrDefault(x =>
                x.AvatarID == avatarId && x.Promotion == avatarData.Promotion)!;

        // Sanity check
        if (avatarData.Level < promotion.MaxLevel ||
            Player.Data.Level < promotion.PlayerLevelRequire ||
            Player.Data.WorldLevel < promotion.WorldLevelRequire) return false;

        // Pay items
        foreach (var cost in promotion.PromotionCostList)
            await Player.InventoryManager!.RemoveItem(cost.ItemID, cost.ItemNum);

        // Promote
        avatarData.Promotion += 1;

        // Send packets
        await Player.SendPacket(new PacketPlayerSyncScNotify(avatarData));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 角色晋阶必须显式标脏
        return true;
    }

    public async ValueTask<bool> PromoteEquipment(int equipmentUniqueId)
    {
        var equipmentData =
            Player.InventoryManager!.Data.EquipmentItems.FirstOrDefault(x => x.UniqueId == equipmentUniqueId);
        if (equipmentData == null ||
            equipmentData.Promotion >= GameData.EquipmentConfigData[equipmentData.ItemId].MaxPromotion) return false;

        var promotionConfig = GameData.EquipmentPromotionConfigData.Values
            .FirstOrDefault(x => x.EquipmentID == equipmentData.ItemId && x.Promotion == equipmentData.Promotion);

        if (promotionConfig == null || equipmentData.Level < promotionConfig.MaxLevel ||
            Player.Data.WorldLevel < promotionConfig.WorldLevelRequire) return false;

        foreach (var cost in promotionConfig.PromotionCostList)
            await Player.InventoryManager!.RemoveItem(cost.ItemID, cost.ItemNum);

        equipmentData.Promotion++;
        await Player.SendPacket(new PacketPlayerSyncScNotify(equipmentData));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 光锥晋阶必须显式标脏

        return true;
    }

    public async ValueTask<(Retcode, List<ItemData>)> LevelUpRelic(int uniqueId, ItemCostData costData)
    {
        var relicItem = Data.RelicItems.Find(x => x.UniqueId == uniqueId);
        if (relicItem == null) return (Retcode.RetRelicNotExist, []);

        // LOCAL(fix-material-validation): 先收集并校验，再扣减。避免锁定遗器被当材料烧掉，
        // 以及材料不足时出现「前面已扣、后面失败」的部分扣除。
        var exp = 0;
        var money = 0;
        List<(int itemId, int count, int uniqueId)> consume = [];
        Dictionary<int, int> requiredMaterials = [];

        foreach (var cost in costData.ItemList)
            if (cost.PileItem != null)
            {
                var itemId = (int)cost.PileItem.ItemId;
                var count = (int)cost.PileItem.ItemNum;
                // 空槽/零数量条目跳过，不否决整次强化（与 RemoveItem 的零扣除语义一致）。
                if (count <= 0) continue;

                GameData.RelicExpItemData.TryGetValue(itemId, out var excel);
                if (excel != null)
                {
                    exp += excel.ExpProvide * count;
                    money += excel.CoinCost * count;
                }

                consume.Add((itemId, count, 0));
                requiredMaterials[itemId] = requiredMaterials.GetValueOrDefault(itemId) + count;
            }
            else if (cost.RelicUniqueId != 0)
            {
                var costItem = Data.RelicItems.Find(x => x.UniqueId == cost.RelicUniqueId);
                if (costItem != null)
                {
                    if (costItem.Locked) return (Retcode.RetRelicLocked, []);

                    GameData.RelicConfigData.TryGetValue(costItem.ItemId, out var costExcel);
                    if (costExcel == null) continue;

                    if (costItem.Level > 0)
                        foreach (var level in Enumerable.Range(0, costItem.Level))
                        {
                            GameData.RelicExpTypeData.TryGetValue(costExcel.ExpType * 100 + level, out var typeExcel);
                            if (typeExcel != null)
                                exp += typeExcel.Exp;
                        }
                    else
                        exp += costExcel.ExpProvide;

                    exp += costItem.Exp;
                    money += costExcel.CoinCost;

                    consume.Add((costItem.ItemId, 1, (int)cost.RelicUniqueId));
                }
            }

        // 配置缺失必须在扣材料之前发现，避免白烧材料。
        GameData.RelicConfigData.TryGetValue(relicItem.ItemId, out var relicExcel);
        if (relicExcel == null) return (Retcode.RetItemConfigNotExist, []);

        // 按 itemId 聚合后校验，避免重复条目各自通过、合并后超扣。
        // 信用点并入需求表，保证「材料 + 信用点」是合并校验。
        if (money > 0) requiredMaterials[2] = requiredMaterials.GetValueOrDefault(2) + money;

        // 先确认每个 itemId 都能被 RemoveItem 正确处理，避免部分扣除。
        foreach (var itemId in requiredMaterials.Keys)
        {
            if (!GameData.ItemConfigData.TryGetValue(itemId, out var costConfig))
                return (Retcode.RetItemConfigNotExist, []);
            if (costConfig.ItemMainType == ItemMainTypeEnum.Virtual && itemId is not (1 or 2 or 3 or 32))
                return (Retcode.RetReqParaInvalid, []);
        }

        var heldMaterials = Data.MaterialItems
            .GroupBy(x => x.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Count));
        heldMaterials[1] = Player.Data.Hcoin;
        heldMaterials[2] = Player.Data.Scoin;
        heldMaterials[3] = Player.Data.Mcoin;
        heldMaterials[32] = Player.Data.TalentPoints;

        if (!RelicRules.CanAfford(requiredMaterials, heldMaterials, 0, 0))
            return (Retcode.RetItemNotEnough, []);

        // credit + materials are charged only after every check passed
        foreach (var cost in consume)
            await RemoveItem(cost.itemId, cost.count, cost.uniqueId);
        if (money > 0)
            await RemoveItem(2, money);

        // level up

        GameData.RelicExpTypeData.TryGetValue(relicExcel.ExpType * 100 + relicItem.Level, out var relicType);
        do
        {
            if (relicType == null) break;
            int toGain;
            if (relicItem.Exp + exp >= relicType.Exp)
                toGain = relicType.Exp - relicItem.Exp;
            else
                toGain = exp;
            relicItem.Exp += toGain;
            exp -= toGain;

            // level up
            if (relicItem.Exp >= relicType.Exp)
            {
                relicItem.Exp = 0;
                relicItem.Level++;
                GameData.RelicExpTypeData.TryGetValue(relicExcel.ExpType * 100 + relicItem.Level, out relicType);
                // relic attribute
                if (relicItem.Level % 3 == 0)
                {
                    if (relicItem.SubAffixes.Count >= 4)
                        relicItem.IncreaseRandomRelicSubAffix();
                    else
                        relicItem.AddRandomRelicSubAffix();
                }
            }
        } while (exp > 0 && relicType?.Exp > 0 && relicItem.Level < relicExcel.MaxLevel);

        // leftover
        Dictionary<int, ItemData> list = [];
        var leftover = exp;
        while (leftover > 0)
        {
            var gain = false;
            foreach (var expItem in GameData.RelicExpItemData.Values.Reverse())
                if (leftover >= expItem.ExpProvide)
                {
                    // add
                    await PutItem(expItem.ItemID, 1);
                    if (list.TryGetValue(expItem.ItemID, out var i))
                    {
                        i.Count++;
                    }
                    else
                    {
                        i = new ItemData
                        {
                            ItemId = expItem.ItemID,
                            Count = 1
                        };
                        list[expItem.ItemID] = i;
                    }

                    leftover -= expItem.ExpProvide;
                    gain = true;
                    break;
                }

            if (!gain) break; // no more item
        }

        if (list.Count > 0) await Player.SendPacket(new PacketPlayerSyncScNotify(list.Values.ToList()));

        // sync
        await Player.SendPacket(new PacketPlayerSyncScNotify(relicItem));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 遗器强化必须显式标脏

        return (Retcode.RetSucc, [.. list.Values]);
    }

    public async ValueTask RankUpAvatar(int avatarId, ItemCostData costData)
    {
        var baseAvatarId = avatarId;
        GameData.MultiplePathAvatarConfigData.TryGetValue(baseAvatarId, out var avatar);
        if (avatar != null) baseAvatarId = avatar.BaseAvatarID;
        var avatarData = Player.AvatarManager!.GetFormalAvatar(baseAvatarId);
        if (avatarData == null) return;
        var pathInfo = avatarData.GetCurPathInfo();
        // LOCAL(fix-avatar-growth-bounds): 星魂已满时不扣材料、不提升；上限与抽卡侧共用 GachaRewardRules.MaxAvatarRank
        if (!AvatarGrowthRules.CanRankUp(pathInfo.Rank)) return;
        foreach (var cost in costData.ItemList) await RemoveItem((int)cost.PileItem.ItemId, (int)cost.PileItem.ItemNum);
        pathInfo.Rank++;
        await Player.SendPacket(new PacketPlayerSyncScNotify(avatarData));
        MarkDirty(); // LOCAL(fix-inventory-persistence): 星魂升级必须显式标脏
    }

    public async ValueTask<Retcode> RankUpEquipment(int equipmentUniqueId, ItemCostData costData)
    {
        var itemData = Data.EquipmentItems.Find(x => x.UniqueId == equipmentUniqueId);
        if (itemData == null)
        {
            await SyncStaleEquipmentIds([equipmentUniqueId]);
            return Retcode.RetEquipmentNotExist;
        }

        GameData.EquipmentConfigData.TryGetValue(itemData.ItemId, out var equipmentConfig);
        if (equipmentConfig == null) return Retcode.RetItemConfigNotExist;

        var maxRank = Math.Max(1, equipmentConfig.MaxRank);
        if (itemData.Rank >= maxRank) return Retcode.RetEquipmentRankUpReachMax;

        var rank = 0;
        List<(int itemId, int count, int uniqueId)> costItems = [];
        HashSet<uint> costEquipmentIds = [];
        List<uint> staleEquipmentIds = [];
        foreach (var cost in costData.ItemList)
        {
            if (cost.EquipmentUniqueId == 0) continue;
            if (cost.EquipmentUniqueId == equipmentUniqueId) return Retcode.RetEquipmentConsumeSelf;
            if (!costEquipmentIds.Add(cost.EquipmentUniqueId)) continue;

            var costItem = Data.EquipmentItems.Find(x => x.UniqueId == cost.EquipmentUniqueId);
            if (costItem == null)
            {
                staleEquipmentIds.Add(cost.EquipmentUniqueId);
                continue;
            }

            if (costItem.Locked) return Retcode.RetEquipmentLocked;
            if (costItem.ItemId != itemData.ItemId) return Retcode.RetEquipmentRankUpMustConsumeSameTid;

            costItems.Add((costItem.ItemId, 1, (int)cost.EquipmentUniqueId));
            rank += Math.Max(1, costItem.Rank);

            if (itemData.Rank + rank >= maxRank) break;
        }

        if (staleEquipmentIds.Count > 0)
        {
            await SyncStaleEquipmentIds(staleEquipmentIds);
            return Retcode.RetEquipmentNotExist;
        }

        if (rank == 0 || costItems.Count == 0) return Retcode.RetItemNoCost;

        await RemoveItems(costItems, false);
        itemData.Rank = Math.Min(itemData.Rank + rank, maxRank);
        MarkDirty();
        await Player.SendPacket(new PacketPlayerSyncScNotify(new ItemSyncData(itemData),
            new DelEquipmentSyncData(costItems.Select(item => (uint)item.uniqueId))));
        return Retcode.RetSucc;
    }

    #endregion
}
