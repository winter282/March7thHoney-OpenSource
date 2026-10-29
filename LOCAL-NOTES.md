# 本地改造说明

> 上游：`Mar7thLover/March7thHoney-OpenSource` 分支 `4.6`，基线 commit `fe00bdb`（2026-09-08 快照）。
> 本文件记录本地改动，不改上游 README。项目总控入口在 `D:\桌面\pc私服\README.md`。

## 仓库策略

| 分支 | 用途 | 规则 |
|---|---|---|
| `4.6` | 上游镜像 | 只 `git pull`，不直接改 |
| `local/4.6` | 本地开发 | 所有本地改动提交在这里 |

同步上游：

```bash
git checkout local/4.6
git fetch origin
git merge origin/4.6
```

上游事实（2026-10-29 核实）：

- `4.6` 分支真实历史只有 8 个 commit（2026-05-22 起），最后一次推送 2026-09-08。
- `main` 分支是旧的另一条线，与 `4.6` 差异约 11472 个文件，**不要合并 main**。
- 上游 44 stars、1 个 open issue、3 个未合 PR、0 discussions，活跃度低。

## 本地改动（截至 2026-10-29）

| 文件 | 改动 | 原因 |
|---|---|---|
| `Config.json` | `ServerOption.AutoCreateUser = true` | 首次登录自动建号 |
| `Config.json` | `LogOption.EnableGamePacketLog = true`、`SavePersonalDebugFile = true` | 排查登录 -3208，记录客户端实际请求 |

`Config.json` 由程序首次启动生成，不入 git。

## 待改（未执行）

`Common/Util/GameConstants.cs`：

```csharp
GAME_VERSION = "4.5.5"   -> "4.6.0"
GameVersionInt = 4550    -> 4600
```

影响：`ConfigManager` 的 hotfix 版本 key、`PlayerInstance` 存档版本校验、连接版本标识。
4.6 分支快照未同步该常量，登录链路打通后再改，一次只改这一处。
变更提案：`D:\桌面\pc私服\openspec\changes\fix-login-4600\`。

## 改动标记约定

- 改上游文件时，在改动处加 `// LOCAL(变更id): 原因` 注释。
- 能通过配置、扩展点、partial class 实现的，不改上游文件。
- 提交粒度：一个子系统一次提交，message 用 `feat(scope): ...` / `fix(scope): ...`。

## 构建与运行

统一使用总控仓脚本（不要直接手敲 dotnet）：

```powershell
powershell -ExecutionPolicy Bypass -File D:\桌面\pc私服\scripts\build.ps1
powershell -ExecutionPolicy Bypass -File D:\桌面\pc私服\scripts\start.ps1
```

- 必须把仓库根作为工作目录（读取 `Config.json` / `Config/` / `Config/Resource.bin`）
- 端口：HTTP 21000、KCP 23301（`ConfigContainer.cs` 默认值）

## 关键文件

| 关注点 | 文件 |
|---|---|
| 启动流程 | `Program/Program/EntryPoint.cs` |
| 配置默认值 | `Common/Configuration/ConfigContainer.cs` |
| 版本常量 | `Common/Util/GameConstants.cs` |
| 登录 | `WebServer/Handler/LoginSessionFactory.cs`、`ComboTokenGranterHandler.cs` |
| 路由 | `WebServer/Controllers/DispatchRoutes.cs` |
| 深渊 | `GameServer/Game/Challenge/`（Memory/Story/Boss/Peak/Tierce 五套实例） |
| 战斗 | `GameServer/Game/Battle/`（`SceneSkillManager.cs`、`BattleManager.cs`） |
| 场景实体 | `GameServer/Game/Scene/SceneEntityLoader.cs` |