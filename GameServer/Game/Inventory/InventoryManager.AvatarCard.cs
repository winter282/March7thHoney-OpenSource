using March7thHoney.Data.Custom;
using March7thHoney.Database.Inventory;

namespace March7thHoney.GameServer.Game.Inventory;

public partial class InventoryManager
{
    private async ValueTask<ItemData?> AddAvatarCardItem(int itemId, bool sync, bool notify)
    {
        // add avatar
        var avatar = Player.AvatarManager?.GetFormalAvatar(itemId);
        if (avatar != null)
        {
            var rankUpItem = Player.InventoryManager!.GetItem(itemId + 10000);
            // LOCAL(fix-avatar-growth-bounds): 与抽卡响应侧共用同一满星判定，避免裸索引 KeyNotFound 与双阈值分叉
            if (!GachaRewardRules.IsAvatarAtMaxRank(avatar, itemId, rankUpItem?.Count ?? 0))
                return await PutItem(itemId + 10000, 1);
            return null;
        }

        await Player.AddAvatar(itemId, sync, notify);
        await AddItem(itemId + 200000, 1, false);
        return null;
    }
}
