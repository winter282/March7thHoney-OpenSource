using March7thHoney.GameServer.Server.Packet.Send.Avatar;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Avatar;

[Opcode(CmdIds.DressRelicAvatarCsReq)]
public class HandlerDressRelicAvatarCsReq : Handler<DressRelicAvatarCsReq>
{
    protected override async Task OnHandle(Connection connection, PlayerInstance player, DressRelicAvatarCsReq req)
    {
        var retcode = Retcode.RetSucc;
        foreach (var param in req.SwitchList)
        {
            var result = await player.InventoryManager!.EquipRelic((int)req.AvatarId, (int)param.RelicUniqueId,
                (int)param.RelicType);
            if (retcode == Retcode.RetSucc && result != Retcode.RetSucc) retcode = result;
        }

        await connection.SendPacket(new PacketDressRelicAvatarScRsp(req.AvatarId, retcode));
    }
}
