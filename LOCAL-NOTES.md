# 本地改造说明

> 上游：`Mar7thLover/March7thHoney-OpenSource` 分支 `4.6`，基线 commit `fe00bdb`。
> 总控入口：`D:\桌面\pc私服\README.md`。

## 仓库策略

| 分支 | 用途 | 规则 |
|---|---|---|
| `4.6` | 上游镜像 | 只 pull，不直接改 |
| `local/4.6` | 本地开发 | 所有本地改动提交在这里 |

远程（2026-10-30 建立异地备份）：

| remote | 地址 | 用途 |
|---|---|---|
| `origin` | `Mar7thLover/March7thHoney-OpenSource` | 上游，只 fetch；push 实测 403 |
| `mine` | `winter282/March7thHoney-OpenSource` | 本地分支备份，推送 `local/4.6` |

```bash
git checkout local/4.6
git fetch origin
git merge origin/4.6
git push mine local/4.6      # 每次本地提交后同步异地备份
```

上游事实（2026-10-29 核实）：`4.6` 历史仅 8 个 commit，最后推送 2026-09-08；
`main` 是旧线，**不要合并**；44 stars，1 个 open issue，3 个未合 PR，0 discussions。

## 本地改动（2026-10-30）

| 文件 | 改动 | 原因 |
|---|---|---|
| `Config.json` | `GameServer.GameServerId = prod_gf_cn`、`GameServerName = 星穹列车`、`EnvType = 0` | 客户端按 region 判定国服；非此值会走 `hkrpg_global` / HoYoPass，登录到不了本地 |
| `WebServer/Controllers/DispatchRoutes.cs` | `GetExperimentList` 返回空列表 `data:[]` | 关闭 Porte，让客户端回退到本地 combosdk 登录 |
| `WebServer/WebProgram.cs` | 增加 `HttpTrace` 请求日志（方法/路径/状态码） | 登录排障；稳定后可移除 |
| `Config.json` | `AutoCreateUser = true`、`EnableGamePacketLog = true`、`SavePersonalDebugFile = true` | 自动建号与排障 |
| `Config/Custom/Banners.json` | 补齐 5003/5004/6003/6004，联动池共 8 个 | 联动 merge group 31 固定需要 8 个池；缺 4 个导致 `avatar pool count [0]` 断言与黑屏（上游 issue #4 / PR #5） |
| `Common/Data/Custom/BannersConfig.cs` | `item_detail_list` 改为 UP 物品 ID 列表；非 1001 池补 `GachaCeiling` | 字段语义对齐官方包（`reliquary-archiver` / `HyacineDH-Core` 证据） |
| `Common/Data/Custom/GachaRewardRules.cs` | **新增**：重复角色满星判定纯函数 | 原 `rank + rankUpItem?.Count >= 6` 在道具缺失时得 `null` 被误判未满星；`PathInfos[item]` 可能 KeyNotFound |
| `GameServer/Game/Gacha/GachaManager.cs` | 5 星/4 星重复角色分支改为调用 `GachaRewardRules.IsAvatarAtMaxRank` | 同上；仅改判定，不改概率/保底/存档 |
| `tests/GachaRewardRules/` | **新增**：独立 assert 回归项目（8 项），不入 `.sln` | 让该规则可测试；不影响 `build.ps1` |
| `Common/Util/GameConstants.cs` | `GAME_VERSION 4.5.5 → 4.6.0`、`GameVersionInt 4550 → 4600` | 与客户端 4.6.0 对齐；hotfix PROD 键变为 `4.6.0`。副作用：旧存档首次登录清空 ServerPrefsDict（上游既定逻辑，改前已备份 DB） |
| `Common/Data/Custom/BannersConfig.cs` | `ToInfo()` 新增 `eventPreview`：活动池 `PrizeItemList` 填四星去重、`ONIHOMCELJK` 填五星在前 + 四星；`ItemDetailList` 不变 | 4.6 客户端用 collection 10 (`prize_item_list`) 渲染四星立绘、collection 5 (`ONIHOMCELJK`) 取 featured 名称/属性/星级；旧投影导致四星显示 `TEST[NO.999]`、UP 显示成第一个四星。依据上游 PR #5 `GachaProtocol.BuildInfo`（PR #5-b） |
| `Common/Configuration/ConfigContainer.cs` | `ServerOption` 新增 `SuppressStory`（默认 true） | 剧情抑制总开关；见 `openspec/changes/archive/2026-10-30-suppress-story/` |
| `GameServer/Game/Task/LevelTask.cs` | `PlayMessage`、`TriggerPerformance` 入口拦截 | 抑制剧情短信与过场演出；机关类动作（ChangePropState 等）保留 |
| `GameServer/Game/Task/PerformanceTrigger.cs` | `TriggerPerformanceE/D` 兜底拦截 | 防止其他调用方绕过 LevelTask 触发演出 |
| `GameServer/Game/Mission/StoryLineManager.cs` | `EnterStoryLine` 拦截非 0 剧情线 | 防止切换 lineup / 加载剧情场景；0 放行 |
| `Common/Data/Custom/AvatarGrowthRules.cs` | **新增**：`CanRankUp(int)`，上限复用 `GachaRewardRules.MaxAvatarRank` | 星魂升级上限单一来源，可独立测试 |
| `GameServer/Game/Inventory/InventoryManager.Leveling.cs` | `RankUpAvatar` 改为「解析→判上限→扣→升」；溢出返还改用 `AvatarExpItemConfigData` | 星魂无上限；角色溢出经验误退光锥材料（ISSUE-008/010） |
| `GameServer/Game/Inventory/InventoryManager.AvatarCard.cs` | 满星判定改用 `GachaRewardRules.IsAvatarAtMaxRank` | 写包侧满星仍发星魂 + 裸索引 KeyNotFound（ISSUE-009） |
| `tests/GachaRewardRules/Program.cs` | 新增 4 条 `CanRankUp` 用例（0/5/6/7） | 覆盖星魂上限 |
| `Config.json` | `ServerOption.SuppressStory = true` | 运行时开关，改后重启生效 |
| `GameServer/Game/Inventory/InventoryManager.Leveling.cs` | 换装/卸下/升级/晋阶/光锥晋阶/遗器强化/星魂共 9 处显式 `MarkDirty()` | 原落盘依赖 `RemoveItem` 副作用；见 `fix-inventory-persistence` |
| `GameServer/Game/Inventory/InventoryManager.Mark.cs` | 锁定/丢弃后显式 `MarkDirty()` | 同上 |
| `GameServer/Game/Avatar/AvatarManager.cs` | 遗器重掷/确认/推荐方案增删改 5 处显式 `MarkDirty()` | 同上 |
| `GameServer/Server/Packet/Recv/Avatar/HandlerUnlockSkilltreeCsReq.cs` | 行迹写入后 `DatabaseHelper.MarkDirty(player.Uid)` | 同上 |
| `WebServer/Controllers/AdminRoutes.cs` | 新增 `POST /admin/api/save`（AdminKey 会话鉴权，保存失败返回非 0） | 供 `stop.ps1` 在强杀前刷盘；ISSUE-015 |
| `Common/Database/DatabaseHelper.cs` | `SaveDatabase()` 返回 `bool`，并用 `SaveLock` 串行化定时刷盘与保存端点 | 让保存端点能报告失败，避免假 PASS；ISSUE-015 |
| `GameServer/Game/Inventory/OfferingManager.cs` | 供奉提交后显式 `MarkDirty()` | 零扣除时不再依赖 `RemoveItem` 副作用；ISSUE-012 |
| `Common/Data/Custom/InventoryRules.cs` | **新增**：`HasEnough(held, required)` 纯函数 | 材料/货币扣除的可单测规则；`fix-material-validation` |
| `GameServer/Game/Inventory/InventoryManager.cs` | `RemoveItem` 在 `count<=0`、材料不足、余额不足时返回 `null` 且不改数据 | 原可扣成负数；ISSUE-011 |
| `GameServer/Game/Inventory/InventoryManager.Leveling.cs` | `LevelUpRelic` 改为先收集校验（含 `Locked`）再扣减 | 锁定遗器可被烧、部分扣除；ISSUE-011 |
| `tests/InventoryRules/` | **新增**：独立回归项目（14 项） | 覆盖 `HasEnough`/`HasEnoughAll` 边界；不入 `.sln` |

`Config.json` 由程序生成、不入 git，改后需重启服务端。

## 登录链路（2026-10-30 实测通过）

```
query_dispatch (region=prod_gf_cn, env=0)
  → query_gateway
  → POST /account/ma-cn-passport/app/loginByPassword   200
  → POST /hkrpg_cn/combo/granter/login/v2/login        200
  → KCP: PlayerGetToken → PlayerLogin → SyncClientResVersion → PlayerLoginFinish
  → uid 10001, token_type=combo
```

客户端侧条件：

- `config.ini`：`channel=1 / sub_channel=1 / cps=gw_PC`
- 客户端补丁：`umpdc.dll` + `Echium.json`（sdk_url=`http://127.0.0.1:21000`）
- 防火墙规则 `Block StarRail IPv6`（Outbound，`2000::/3`）阻断 IPv6 直连官方

Fiddler 曾在排障中使用，**当前未运行**。系统代理由 Clash 开启（`127.0.0.1:7897`），
但 `ProxyOverride` 含 `127.*` 且无 TUN 网卡，回环流量直连；代理不是登录必需项。

## 已知问题（未解决）

无。移动包已于 2026-10-30 实测验证（见 ISSUE-005）。

## 已解决（2026-10-30）

- 星魂无 6 级上限、写包侧满星仍发星魂、角色溢出经验误退光锥材料：
  `fix-avatar-growth-bounds`，单测 12/12，实机验证 V1/V2 通过。
  详见 `docs/08-known-issues.md` ISSUE-008/009/010。

- 抽卡界面退出后黑屏：联动 merge group 需要 8 个池，补齐 5003/5004/6003/6004 后恢复。
  详见 `D:\桌面\pc私服\docs\08-known-issues.md` ISSUE-001 与
  `openspec/changes/archive/2026-10-30-fix-gacha-collaboration-black-screen/RESULT.md`。
- 四星立绘 `TEST[NO.999]` + UP 名称/属性/星级取错：`BannersConfig.ToInfo()` 的字段投影
  按上游 PR #5 修正（见上表）。实机验证四星立绘、抽卡、退出均正常。
  详见 `D:\桌面\pc私服\openspec\changes\archive\2026-10-30-fix-gacha-four-star-preview/RESULT.md`。
- 移动包未验证：临时移出 `IgnoreLog` 实测，按 W 后移动包 94→182，客户端确实发送。
  探针已还原。详见 `docs/08-known-issues.md` ISSUE-005。
- 剧情演出抑制：`SuppressStory=true`，拦 TriggerPerformance / PlayMessage / 非 0 剧情线，
  保留场景机关。多张地图（含列车）实机传送与行走正常。
  详见 `D:\桌面\pc私服\openspec\changes\archive\2026-10-30-suppress-story\RESULT.md`。

## 待改（未执行）

无。版本常量已于 2026-10-30 对齐 `4.6.0` / `4600`（见上表）。

## 改动标记约定

- 改上游文件时在改动处加 `// LOCAL(<change-id>): 原因`。
- 能通过配置、扩展点、partial class 实现的，不改上游文件。
- 禁止 `dotnet format` 全仓。
- 一个子系统一次提交，message 用 `feat(scope): ...` / `fix(scope): ...`。

## 构建与运行

```powershell
powershell -ExecutionPolicy Bypass -File D:\桌面\pc私服\scripts\build.ps1
powershell -ExecutionPolicy Bypass -File D:\桌面\pc私服\scripts\start.ps1
```

必须把仓库根作为工作目录。端口：HTTP 21000、KCP 23301。
控制台日志写到 `pc私服/logs`，**不要写进 `Config/Logs`**（启动时会打包导致崩溃）。