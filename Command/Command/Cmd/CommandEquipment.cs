using March7thHoney.Data;
using March7thHoney.Database;
using March7thHoney.GameServer.Server.Packet.Send.PlayerSync;
using March7thHoney.Internationalization;

namespace March7thHoney.Command.Command.Cmd;

// LOCAL(add-default-max-level): 批量把已有光锥设为满级/满晋阶。
[CommandInfo("equipment", "Game.Command.Give.Desc", "Game.Command.Give.Usage", ["equip"],
    permission: CommandPermissions.Give)]
public class CommandEquipment : ICommand
{
    [CommandMethod("level")]
    public async ValueTask SetLevel(CommandArg arg)
    {
        var player = arg.Target?.Player;
        if (player == null)
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
        var level = Math.Clamp(arg.GetInt(1), 1, 80);
        var inventory = player.InventoryManager!;

        if (id == -1)
        {
            foreach (var equip in inventory.Data.EquipmentItems)
            {
                GameData.EquipmentConfigData.TryGetValue(equip.ItemId, out var excel);
                equip.Level = level;
                equip.Promotion = excel?.MaxPromotion ?? equip.Promotion;
                equip.Exp = 0;
            }

            DatabaseHelper.MarkDirty(player.Uid);
            await player.SendPacket(new PacketPlayerSyncScNotify(inventory.Data.EquipmentItems));
            await arg.SendMsg($"All lightcones set to level {level}.");
            return;
        }

        var item = inventory.Data.EquipmentItems.Find(x => x.UniqueId == id);
        if (item == null)
        {
            await arg.SendMsg("Lightcone not found.");
            return;
        }

        GameData.EquipmentConfigData.TryGetValue(item.ItemId, out var config);
        item.Level = level;
        item.Promotion = config?.MaxPromotion ?? item.Promotion;
        item.Exp = 0;

        DatabaseHelper.MarkDirty(player.Uid);
        await player.SendPacket(new PacketPlayerSyncScNotify(item));
        await arg.SendMsg($"Lightcone {id} set to level {level}.");
    }
}