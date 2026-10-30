using March7thHoney.GameServer.Server.Packet.Send.Avatar;
using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Recv.Avatar;

[Opcode(CmdIds.RelicReforgeCsReq)]
public class HandlerRelicReforgeCsReq : Handler<RelicReforgeCsReq>
{
    protected override async Task OnHandle(Connection connection, PlayerInstance player, RelicReforgeCsReq req)
    {
        var retcode = await player.AvatarManager!.ReforgeRelic((int)req.RelicUniqueId,
            (int)req.ReforgeBlockSubAffixId);
        await connection.SendPacket(new PacketRelicReforgeScRsp(retcode));
    }
}
