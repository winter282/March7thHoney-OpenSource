using March7thHoney.Data;
using March7thHoney.Data.Custom;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums.Item;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.Inventory;

public partial class InventoryManager
{
    private async ValueTask<ItemData?> AddRelicItem(int itemId, int level, bool sync)
    {
        //I dont think one player can have more than max count of relic until i see a player get 50000 relic and the client crashed :(
        if (Data.RelicItems.Count + 1 > GameConstants.INVENTORY_MAX_RELIC) // get the max relic
        {
            await Player.SendPacket(new PacketRetcodeNotify(Retcode.RetRelicExceedLimit));
            return null;
        }

        var (_, itemData) = await HandleRelic(itemId, ++Data.NextUniqueId, level, sync: sync);
        return itemData;
    }

    public async ValueTask<(int, ItemData?)> HandleRelic(
        int relicId, int uniqueId, int level, int mainAffixId = 0, List<(int, int)>? subAffixes = null,
        bool sync = true)
    {
        // Excel
        GameData.RelicConfigData.TryGetValue(relicId, out var itemConfig);
        GameData.ItemConfigData.TryGetValue(relicId, out var itemConfigExcel);
        if (itemConfig == null || itemConfigExcel == null)
            return (1, null);

        GameData.RelicSubAffixData.TryGetValue(itemConfig.SubAffixGroup, out var subAffixConfig);
        GameData.RelicMainAffixData.TryGetValue(itemConfig.MainAffixGroup, out var mainAffixConfig);
        if (subAffixConfig == null || mainAffixConfig == null)
            return (1, null);

        var relic = new ItemData
        {
            ItemId = relicId,
            Level = Math.Max(Math.Min(level, 9999), 0),
            UniqueId = uniqueId,
            Count = 1
        };

        // MainAffixId
        if (mainAffixId == 0 || !mainAffixConfig.TryGetValue(mainAffixId, out _))
            relic.AddRandomRelicMainAffix();
        else
            relic.MainAffix = mainAffixId;

        // SubAffixes
        subAffixes ??= [];
        if (subAffixes.Count > 4) return (3, null);
        relic.AddRelicSubAffix(subAffixes); // Add from input

        var initSubCnt = new Random().Next(3, 5);
        relic.AddRandomRelicSubAffix(initSubCnt - subAffixes.Count);
        if (initSubCnt == 3 && level / 3 > 0) relic.AddRandomRelicSubAffix(); // Random add init subAffixes

        var remainUpCnt = level / 3 - (4 - initSubCnt) - subAffixes.Sum(x => x.Item2);
        relic.IncreaseRandomRelicSubAffix(remainUpCnt); // Level up

        if (sync)
        {
            await Player.InventoryManager!.AddItem(relic, false);
        }
        else
        {
            await PutItem(relic.ItemId, relic.Count, relic.Rank, relic.Promotion, relic.Level,
                relic.Exp, relic.TotalExp, relic.MainAffix, relic.SubAffixes,
                relic.ReforgeSubAffixes, relic.UniqueId);
        }
        return (0, relic);
    }

    public async ValueTask<(Retcode, List<ItemData>)> ComposeRelic(ComposeSelectedRelicCsReq req)
    {
        var count = (int)req.Count;
        if (count <= 0) count = 1;
        // LOCAL(fix-relic-operations): 上限按遗器背包容量硬截断，防止超大 count 溢出或长循环。
        if (count > GameConstants.INVENTORY_MAX_RELIC)
            return (Retcode.RetReqParaInvalid, []);

        // LOCAL(fix-relic-operations): 背包空间、配方与子表必须在扣费前全部校验。
        if (!RelicRules.CanCompose(Data.RelicItems.Count, count, GameConstants.INVENTORY_MAX_RELIC))
            return (Retcode.RetRelicExceedLimit, []);

        if (!GameData.ItemComposeConfigData.TryGetValue((int)req.ComposeId, out var composeConfig))
            return (Retcode.RetItemConfigNotExist, []);

        var relicId = (int)req.ComposeRelicId;
        // LOCAL(fix-relic-operations): composeConfig.ItemID 是配方物品 ID（如 9004 → 81044），
        // 不是产出的遗器 ID，不能拿它和 composeRelicId 比较。
        if (!GameData.RelicConfigData.TryGetValue(relicId, out var itemConfig))
            return (Retcode.RetItemConfigNotExist, []);

        if (!GameData.RelicSubAffixData.TryGetValue(itemConfig.SubAffixGroup, out var subAffixConfig))
            return (Retcode.RetItemConfigNotExist, []);

        var mainAffix = (int)req.MainAffixId;
        if (mainAffix != 0)
        {
            if (!GameData.RelicMainAffixData.TryGetValue(itemConfig.MainAffixGroup, out var mainAffixConfig) ||
                !mainAffixConfig.ContainsKey(mainAffix))
                return (Retcode.RetReqParaInvalid, []);
        }

        var subAffixIds = req.SubAffixIdList.Select(id => (int)id).ToList();
        if (subAffixIds.Any(id => !subAffixConfig.ContainsKey(id)))
            return (Retcode.RetReqParaInvalid, []);

        // LOCAL(fix-relic-operations): 按主人决定，请求列表与配方表都计入成本；
        // 先聚合、校验，通过后再一次性扣除，避免中途失败造成部分扣除。
        var costs = new List<(int ItemId, int Count)>();
        if (req.ComposeItemList != null)
            foreach (var cost in req.ComposeItemList.ItemList)
                if (cost.PileItem != null)
                    costs.Add(((int)cost.PileItem.ItemId, (int)cost.PileItem.ItemNum));

        if (req.WrItemList != null)
            foreach (var cost in req.WrItemList.ItemList)
                if (cost.PileItem != null)
                    costs.Add(((int)cost.PileItem.ItemId, (int)cost.PileItem.ItemNum));

        foreach (var cost in composeConfig.MaterialCost)
            costs.Add((cost.ItemID, cost.ItemNum * count));

        var required = RelicRules.AggregateCosts(costs);
        var coin = composeConfig.CoinCost * count;
        // 信用点并入同一张需求表，保证「材料 + 信用点」是合并校验而不是各查各的。
        if (coin > 0) required[2] = required.GetValueOrDefault(2) + coin;

        // 先确认每个 itemId 都能被 RemoveItem 正确处理，避免部分扣除。
        foreach (var itemId in required.Keys)
        {
            if (!GameData.ItemConfigData.TryGetValue(itemId, out var costConfig))
                return (Retcode.RetItemConfigNotExist, []);
            if (costConfig.ItemMainType == ItemMainTypeEnum.Virtual && itemId is not (1 or 2 or 3 or 32))
                return (Retcode.RetReqParaInvalid, []);
        }

        var held = Data.MaterialItems
            .GroupBy(x => x.ItemId)
            .ToDictionary(group => group.Key, group => group.Sum(x => x.Count));
        // 请求列表可能包含虚拟货币；把它们并入持有量，避免被当成材料缺失。
        held[1] = Player.Data.Hcoin;
        held[2] = Player.Data.Scoin;
        held[3] = Player.Data.Mcoin;
        held[32] = Player.Data.TalentPoints;

        if (!RelicRules.CanAfford(required, held, 0, 0))
            return (Retcode.RetItemNotEnough, []);

        foreach (var (itemId, amount) in required)
            if (await RemoveItem(itemId, amount) == null)
                return (Retcode.RetItemNotEnough, []);

        List<ItemData> items = [];
        for (var i = 0; i < count; i++)
        {
            var itemData = new ItemData
            {
                ItemId = relicId,
                Level = 0,
                UniqueId = ++Data.NextUniqueId,
                MainAffix = mainAffix,
                SubAffixes = subAffixIds.Select(subId => new ItemSubAffix(subAffixConfig[subId], 1)).ToList(),
                Count = 1
            };
            if (mainAffix == 0) itemData.AddRandomRelicMainAffix();
            itemData.AddRandomRelicSubAffix(3 - itemData.SubAffixes.Count + itemData.LuckyRelicSubAffixCount());
            await AddItem(itemData, false);
            items.Add(itemData);
        }

        return (Retcode.RetSucc, items);
    }
}
