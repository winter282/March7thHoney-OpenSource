using March7thHoney.Data.Custom;

// Standalone regression runner for relic operation rules.
// Deliberately dependency-free (no test framework): run with
//   dotnet run --project D:\hsr-srv\m7h\tests\RelicRules

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

// ---------- 成本聚合 ----------
var empty = RelicRules.AggregateCosts([]);
CheckInt("empty cost count", empty.Count, 0);

var dup = RelicRules.AggregateCosts([(101, 2), (101, 3), (102, 1)]);
CheckInt("duplicate item summed", dup[101], 5);
CheckInt("separate item kept", dup[102], 1);

var invalid = RelicRules.AggregateCosts([(0, 5), (103, 0), (104, -2), (105, 1)]);
CheckInt("invalid ids/counts ignored", invalid.Count, 1);
CheckInt("valid item kept", invalid[105], 1);

// ---------- 余额校验 ----------
var need = new Dictionary<int, int> { [101] = 5, [102] = 1 };
var heldEnough = new Dictionary<int, int> { [101] = 5, [102] = 1 };
var heldShort = new Dictionary<int, int> { [101] = 4, [102] = 1 };
Check("afford exact", RelicRules.CanAfford(need, heldEnough, 100, 100), true);
Check("afford short material", RelicRules.CanAfford(need, heldShort, 100, 100), false);
Check("afford short coin", RelicRules.CanAfford(need, heldEnough, 101, 100), false);
Check("afford empty", RelicRules.CanAfford(new Dictionary<int, int>(), new Dictionary<int, int>(), 0, 0), true);

// ---------- 槽位 ----------
Check("slot 1 valid", RelicRules.IsValidSlot(1), true);
Check("slot 6 valid", RelicRules.IsValidSlot(6), true);
Check("slot 0 invalid", RelicRules.IsValidSlot(0), false);
Check("slot 7 invalid", RelicRules.IsValidSlot(7), false);
Check("type matches slot", RelicRules.MatchesSlot(3, 3), true);
Check("type mismatch slot", RelicRules.MatchesSlot(3, 4), false);

// ---------- 背包上限 ----------
Check("compose fits", RelicRules.CanCompose(10, 2, 12), true);
Check("compose exceeds", RelicRules.CanCompose(11, 2, 12), false);
Check("compose zero count", RelicRules.CanCompose(0, 0, 12), false);
Check("compose negative count", RelicRules.CanCompose(0, -1, 12), false);
Check("compose overflow count", RelicRules.CanCompose(100, int.MaxValue, 3000), false);
Check("compose negative current", RelicRules.CanCompose(-1, 1, 12), false);

// ---------- 重掷词条选择 ----------
var candidates = new List<int> { 1, 2, 3, 4, 5 };
var picked = RelicRules.BuildReforgeAffixIds(candidates, 4, 3, new Random(12345));
CheckInt("reforge picks requested count", picked.Count, 4);
Check("reforge excludes blocked id", picked.Contains(3), false);
CheckInt("reforge no duplicates", picked.Distinct().Count(), picked.Count);

var allBlocked = RelicRules.BuildReforgeAffixIds([3], 2, 3, new Random(1));
CheckInt("reforge no candidates after block", allBlocked.Count, 0);

var insufficient = RelicRules.BuildReforgeAffixIds([1, 2], 4, 0, new Random(1));
CheckInt("reforge returns available when pool short", insufficient.Count, 2);

// ---------- 重掷升级次数分配 ----------
var dist = RelicRules.DistributeUpgrades(7, 4, new Random(99));
CheckInt("upgrade distribution length", dist.Length, 4);
CheckInt("upgrade distribution sum", dist.Sum(), 7);
Check("upgrade distribution non-negative", dist.All(x => x >= 0), true);

var zeroDist = RelicRules.DistributeUpgrades(0, 3, new Random(1));
CheckInt("zero upgrade sum", zeroDist.Sum(), 0);
CheckInt("zero upgrade with no affixes", RelicRules.DistributeUpgrades(5, 0, new Random(1)).Length, 0);

Console.WriteLine($"{(failures == 0 ? "PASS" : "FAILED")}: {checks - failures}/{checks} checks");
return failures == 0 ? 0 : 1;