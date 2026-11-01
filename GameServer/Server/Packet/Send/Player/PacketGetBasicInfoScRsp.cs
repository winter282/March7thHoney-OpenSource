using March7thHoney.GameServer.Game.Player;
using March7thHoney.Kcp;
using March7thHoney.Proto;
using March7thHoney.Util;

namespace March7thHoney.GameServer.Server.Packet.Send.Player;

public class PacketGetBasicInfoScRsp : BasePacket
{
    public PacketGetBasicInfoScRsp(PlayerInstance player) : base(CmdIds.GetBasicInfoScRsp)
    {
        var proto = new GetBasicInfoScRsp
        {
            CurDay = 1,
            // LOCAL(fix-stamina-recover-time): NextStaminaRecover 本身就是 Unix 秒
            // （PlayerInstance.cs 用 GetUnixSec() 赋值）。原先多余的 /1000 让这里下发成
            // 1793446 这样的 6 位数，客户端按秒解析成 1970 年，_CmdGetBasicInfoScRsp 每次
            // 登录抛 NullReferenceException。StaminaInfoScNotify 同一字段是原样发的，口径一致。
            NextRecoverTime = player.Data.NextStaminaRecover,
            GameplayBirthday = (uint)player.Data.Birthday,
            PlayerSettingInfo = player.Data.PrivacySettings.ToSettingProto(),
            Gender = (uint)player.Data.CurrentGender
        };

        if (player.MissionEnabled)
        {
            if (player.AvatarManager!.GetHero()!.PathInfos.Count > 0) player.Data.IsGenderSet = true;
            proto.Gender = (uint)player.Data.CurrentGender;
            proto.IsGenderSet = player.Data.IsGenderSet;
        }
        else
        {
            proto.Gender = (uint)player.Data.CurrentGender;
            proto.IsGenderSet = true;
        }

        SetData(proto);
    }
}
