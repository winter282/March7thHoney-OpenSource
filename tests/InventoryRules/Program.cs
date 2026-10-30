using March7thHoney.Data.Custom;

// Standalone regression runner for inventory material validation.
// Deliberately dependency-free (no test framework): run with
//   dotnet run --project D:\hsr-srv\m7h\tests\InventoryRules

var failures = 0;
var checks = 0;

void Check(string name, bool actual, bool expected)
{
    checks++;
    if (actual == expected) return;
    failures++;
    Console.WriteLine($"FAIL  {name}: expected {expected}, got {actual}");
}

// 正常持有量
Check("held=5, need=3", InventoryRules.HasEnough(5, 3), true);
Check("held=3, need=3", InventoryRules.HasEnough(3, 3), true);
Check("held=2, need=3", InventoryRules.HasEnough(2, 3), false);
Check("held=0, need=1", InventoryRules.HasEnough(0, 1), false);

// 零消耗视为无需扣除
Check("held=10, need=0", InventoryRules.HasEnough(10, 0), true);

// 非法输入：负需求、负持有
Check("held=10, need=-1", InventoryRules.HasEnough(10, -1), false);
Check("held=-1, need=0", InventoryRules.HasEnough(-1, 0), false);

// 聚合校验：同一 itemId 的重复条目必须先合并，否则会各自通过、合并后超扣
var need10 = new Dictionary<int, int> { [1] = 10 };
Check("agg: need 10, held 5", InventoryRules.HasEnoughAll(need10, new Dictionary<int, int> { [1] = 5 }), false);
Check("agg: need 10, held 10", InventoryRules.HasEnoughAll(need10, new Dictionary<int, int> { [1] = 10 }), true);
Check("agg: missing item", InventoryRules.HasEnoughAll(need10, new Dictionary<int, int>()), false);
Check("agg: empty need", InventoryRules.HasEnoughAll(new Dictionary<int, int>(), new Dictionary<int, int>()), true);

// 极值/溢出边界
Check("max held, max need", InventoryRules.HasEnough(int.MaxValue, int.MaxValue), true);
Check("max held, need 1", InventoryRules.HasEnough(int.MaxValue, 1), true);
Check("held 0, need max", InventoryRules.HasEnough(0, int.MaxValue), false);

Console.WriteLine($"{(failures == 0 ? "PASS" : "FAILED")}: {checks - failures}/{checks} checks");
return failures == 0 ? 0 : 1;