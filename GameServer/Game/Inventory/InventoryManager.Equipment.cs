using March7thHoney.Data;
using March7thHoney.Data.Excel;
using March7thHoney.Database.Friend;
using March7thHoney.Database.Inventory;
using March7thHoney.Enums.Item;
using March7thHoney.GameServer.Server.Packet.Send.Player;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Game.Inventory;

public partial class InventoryManager
{
    private async ValueTask<ItemData?> AddEquipmentItem(int itemId, int rank, int level, int promotion,
        ItemConfigExcel itemConfig)
    {
        if (Data.EquipmentItems.Count + 1 > GameConstants.INVENTORY_MAX_EQUIPMENT) // get the max equipment
        {
            await Player.SendPacket(new PacketRetcodeNotify(Retcode.RetEquipmentExceedLimit));
            return null;
        }

        // LOCAL(add-default-max-level): 新获得的光锥默认满级/满晋阶
        GameData.EquipmentConfigData.TryGetValue(itemId, out var equipmentExcel);
        var itemData = await PutItem(itemId, 1, rank,
            equipmentExcel?.MaxPromotion ?? promotion, 80, uniqueId: ++Data.NextUniqueId);

        if (itemConfig.Rarity == ItemRarityEnum.SuperRare)
            // add development
            Player.FriendRecordData!.AddAndRemoveOld(new FriendDevelopmentInfoPb
            {
                DevelopmentType = (DevelopmentType)11,
                Params = { { "EquipmentTid", (uint)itemConfig.ID } }
            });
        return itemData;
    }
}
