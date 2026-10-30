using March7thHoney.Data.Custom;
using March7thHoney.Database.Avatar;

// Standalone regression runner for the duplicate-avatar max-rank rule.
// Deliberately dependency-free (no test framework): run with
//   dotnet run --project D:\hsr-srv\m7h\tests\GachaRewardRules

var failures = 0;
var checks = 0;

void Check(string name, bool actual, bool expected)
{
    checks++;
    if (actual == expected) return;
    failures++;
    Console.WriteLine($"FAIL  {name}: expected {expected}, got {actual}");
}

static FormalAvatarInfo AvatarWithRank(int avatarId, int rank)
{
    var avatar = new FormalAvatarInfo(avatarId, avatarId, addSkills: false);
    avatar.PathInfos[avatarId] = new PathInfo(avatarId) { Rank = rank };
    return avatar;
}

// 1. 满星且背包无星魂道具：修复前 int + null -> null，比较为 false（bug）
Check("Rank=6, extra=0 -> max", GachaRewardRules.IsAvatarAtMaxRank(AvatarWithRank(1003, 6), 1003, 0), true);

// 2. 5 星 + 1 个星魂道具 = 6，满星
Check("Rank=5, extra=1 -> max", GachaRewardRules.IsAvatarAtMaxRank(AvatarWithRank(1003, 5), 1003, 1), true);

// 3. 5 星、无道具，未满星
Check("Rank=5, extra=0 -> not max", GachaRewardRules.IsAvatarAtMaxRank(AvatarWithRank(1003, 5), 1003, 0), false);

// 4. 已满星且还有多余道具，仍是满星
Check("Rank=6, extra=3 -> max", GachaRewardRules.IsAvatarAtMaxRank(AvatarWithRank(1003, 6), 1003, 3), true);

// 5. 路径数据缺失：不得抛异常，按 0 计
var missingPath = new FormalAvatarInfo(1003, 1003, addSkills: false);
Check("missing PathInfo -> not max", GachaRewardRules.IsAvatarAtMaxRank(missingPath, 1003, 0), false);

// 6. avatar 为 null：不得抛异常，按 0 计
Check("null avatar -> not max", GachaRewardRules.IsAvatarAtMaxRank(null, 1003, 0), false);

// 7. 负数道具数量（防御性）不应把结果抬高
Check("Rank=6, extra=-5 -> max", GachaRewardRules.IsAvatarAtMaxRank(AvatarWithRank(1003, 6), 1003, -5), true);
Check("Rank=0, extra=-5 -> not max", GachaRewardRules.IsAvatarAtMaxRank(AvatarWithRank(1003, 0), 1003, -5), false);

Console.WriteLine($"{(failures == 0 ? "PASS" : "FAILED")}: {checks - failures}/{checks} checks");
return failures == 0 ? 0 : 1;