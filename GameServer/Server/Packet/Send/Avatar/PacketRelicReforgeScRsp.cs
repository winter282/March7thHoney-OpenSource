using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.Avatar;

public class PacketRelicReforgeScRsp : BasePacket
{
    public PacketRelicReforgeScRsp(Retcode retcode = Retcode.RetSucc) : base(CmdIds.RelicReforgeScRsp)
    {
        SetData(new RelicReforgeScRsp
        {
            PLDKENJLKME = (uint)retcode
        });
    }
}