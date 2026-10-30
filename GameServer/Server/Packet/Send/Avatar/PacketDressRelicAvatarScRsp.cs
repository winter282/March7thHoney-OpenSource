using March7thHoney.Kcp;
using March7thHoney.Proto;

namespace March7thHoney.GameServer.Server.Packet.Send.Avatar;

public class PacketDressRelicAvatarScRsp : BasePacket
{
    public PacketDressRelicAvatarScRsp(uint avatarId, Retcode retcode = Retcode.RetSucc)
        : base(CmdIds.DressRelicAvatarScRsp)
    {
        SetData(new DressRelicAvatarScRsp
        {
            AvatarId = avatarId,
            Retcode = (uint)retcode
        });
    }
}