using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.Avatar;

public class PacketTakeOffRelicScRsp : BasePacket
{
    public PacketTakeOffRelicScRsp(Retcode retcode = Retcode.RetSucc) : base(CmdIds.TakeOffRelicScRsp)
    {
        SetData(new TakeOffRelicScRsp
        {
            PLDKENJLKME = (uint)retcode
        });
    }
}