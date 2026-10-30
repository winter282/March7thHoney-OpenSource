using March7thHoney.GameServer.Server.Packet.Send.Avatar;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Avatar;

[Opcode(CmdIds.TakeOffRelicCsReq)]
public class HandlerTakeOffRelicCsReq : Handler<TakeOffRelicCsReq>
{
    protected override async Task OnHandle(Connection connection, PlayerInstance player, TakeOffRelicCsReq req)
    {
        var retcode = Retcode.RetSucc;
        foreach (var param in req.RelicTypeList)
        {
            var result = await player.InventoryManager!.UnequipRelic((int)req.AvatarId, (int)param);
            if (retcode == Retcode.RetSucc && result != Retcode.RetSucc) retcode = result;
        }

        await connection.SendPacket(new PacketTakeOffRelicScRsp(retcode));
    }
}
