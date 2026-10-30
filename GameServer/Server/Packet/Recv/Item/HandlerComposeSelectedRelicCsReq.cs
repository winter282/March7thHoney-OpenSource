using March7thHoney.GameServer.Server.Packet.Send.Item;
using March7thHoney.Kcp;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Server.Packet.Recv.Item;

[Opcode(CmdIds.ComposeSelectedRelicCsReq)]
public class HandlerComposeSelectedRelicCsReq : Handler<ComposeSelectedRelicCsReq>
{
    protected override async Task OnHandle(Connection connection, PlayerInstance player, ComposeSelectedRelicCsReq req)
    {
        var (retcode, items) = await player.InventoryManager!.ComposeRelic(req);
        if (retcode != Retcode.RetSucc || items.Count == 0)
        {
            await connection.SendPacket(new PacketComposeSelectedRelicScRsp(req.ComposeId, retcode));
            return;
        }

        await connection.SendPacket(new PacketComposeSelectedRelicScRsp(req.ComposeId, items));
    }
}
