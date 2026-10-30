# 本地改造说明

> 上游：`Mar7thLover/March7thHoney-OpenSource` 分支 `4.6`，基线 commit `fe00bdb`。
> 总控入口：`D:\桌面\pc私服\README.md`。

## 仓库策略

| 分支 | 用途 | 规则 |
|---|---|---|
| `4.6` | 上游镜像 | 只 pull，不直接改 |
| `local/4.6` | 本地开发 | 所有本地改动提交在这里 |

```bash
git checkout local/4.6
git fetch origin
git merge origin/4.6
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

Fiddler 曾在排障中使用，**当前未运行、系统代理已清空，登录仍成立**，不是必需项。

## 已知问题（未解决）

1. 进入世界后**键盘失灵**（HUD 有按键提示，无响应）。疑似窗口焦点问题，未确认。
2. 进入抽卡界面再退出后**场景黑屏**。与上游 issue #4 邻近但现象不同，未定位。

## 待改（未执行）

`Common/Util/GameConstants.cs`：`GAME_VERSION = "4.5.5"` / `GameVersionInt = 4550`
应改为 `4.6.0` / `4600`。当前登录可用，但版本标识仍不一致。

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