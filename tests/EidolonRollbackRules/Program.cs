using March7thHoney.Data.Custom;

// Standalone regression runner for eidolon rollback rules.
// Deliberately dependency-free (no test framework): run with
//   dotnet run --project D:\hsr-srv\m7h\tests\EidolonRollbackRules

var failures = 0;
var checks = 0;

void Check(string name, bool actual, bool expected)
{
    checks++;
    if (actual == expected) return;
    failures++;
    Console.WriteLine($"FAIL  {name}: expected {expected}, got {actual}");
}

void CheckInt(string name, int actual, int expected)
{
    checks++;
    if (actual == expected) return;
    failures++;
    Console.WriteLine($"FAIL  {name}: expected {expected}, got {actual}");
}

// ---------- 目标等级 ----------
Check("target 0 valid", EidolonRollbackRules.IsValidTarget(0), true);
Check("target 6 valid", EidolonRollbackRules.IsValidTarget(6), true);
Check("target -1 invalid", EidolonRollbackRules.IsValidTarget(-1), false);
Check("target 7 invalid", EidolonRollbackRules.IsValidTarget(7), false);

// ---------- 退款数量 ----------
CheckInt("6 -> 3 refunds 3", EidolonRollbackRules.CalculateRefund(6, 3), 3);
CheckInt("3 -> 3 refunds 0", EidolonRollbackRules.CalculateRefund(3, 3), 0);
CheckInt("3 -> 6 refunds 0", EidolonRollbackRules.CalculateRefund(3, 6), 0);
CheckInt("0 -> 0 refunds 0", EidolonRollbackRules.CalculateRefund(0, 0), 0);
CheckInt("6 -> 0 refunds 6", EidolonRollbackRules.CalculateRefund(6, 0), 6);
CheckInt("negative current refunds 0", EidolonRollbackRules.CalculateRefund(-1, 0), 0);
CheckInt("legacy rank 8 clamps to 6", EidolonRollbackRules.CalculateRefund(8, 0), 6);
CheckInt("target above max clamps", EidolonRollbackRules.CalculateRefund(6, 9), 0);

// ---------- 星魂道具 ID ----------
CheckInt("1105 -> 11105", EidolonRollbackRules.ResolveRankItemId(1105), 11105);
CheckInt("1001 -> 11001", EidolonRollbackRules.ResolveRankItemId(1001), 11001);
CheckInt("1224 -> 11224", EidolonRollbackRules.ResolveRankItemId(1224), 11224);

Console.WriteLine($"{(failures == 0 ? "PASS" : "FAILED")}: {checks - failures}/{checks} checks");
return failures == 0 ? 0 : 1;