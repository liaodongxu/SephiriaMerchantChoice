# 商人自选商品 · Sephiria Merchant Choice

少刷几次商店，多花时间冒险。

将《Sephiria》的随机补货改为可搜索、可筛选的商品目录，直接挑选符合条件的神器和石板。

**[下载 v1.0.0（BepInEx 6）](downloads/SephiriaMerchantChoice-1.0.0-BepInEx6.zip?raw=true)** · [English](README.en.md)

## 功能

- 按神器、石板或偏好筛选，支持名称搜索和分页。
- 目录内显示偏好爱心，悬停商品名称或图标查看介绍。
- 购买按钮与介绍区域分离，避免介绍遮挡点击。
- 使用游戏原生购买流程，保留谈判折扣、交易券和背包检查。
- 面板限制在屏幕左侧约 3/7 范围内，复用游戏字体、图标和边框。

## 安装

适用环境：**Windows x64、Sephiria 1.0.30、BepInEx 6 Unity Mono**。开发使用 BepInEx 6.0.0-be.697；不支持 BepInEx 5 或 IL2CPP 版本，其他游戏版本未验证。

1. 先安装适配的 [BepInEx 6](https://github.com/BepInEx/BepInEx) 环境并退出游戏。
2. 下载上方 ZIP，将包内的 `BepInEx` 文件夹合并到 `Sephiria.exe` 所在目录。
3. 确认文件位于 `BepInEx/plugins/SephiriaMerchantChoice.dll`，启动游戏。
4. 在已具备补货功能的商人处点击原版“刷新”按钮，即可打开目录。

本包只包含这个 mod，不附带游戏文件或 BepInEx 环境。不需要其他自制插件。

**从旧合并版升级：**先将 `SephiriaRunQoL.dll` 移到 `BepInEx/plugins` 之外；只改文件名或放进 plugins 子目录不能禁用它。新旧版不能同时加载。

## 保留的规则

- 每位商人最多补购 **2 件神器、1 件石板**，关闭再打开目录不会重置名额。
- 商人尚未尝试过补货时，首次打开目录消耗 **2 颗蓝宝石**；之后打开目录不重复扣费。商品仍需支付金币或使用有效交易券。
- 只列出已经解锁且满足当前补货条件的商品，并非游戏全物品生成器。
- 双属性羁绊神器需要相关两种连击都激活。
- 主背包已持有的神器、商人当前正在出售的同款神器不会重复列出；还会检查互斥、武器适配等条件。
- 副背包不在原版 `HasItem` 检查范围内；移入副背包可能绕过主背包重复过滤，但仍需满足其他条件。

这会改变原版随机选货体验，**不是纯界面美化，也不声称完全保持原版平衡**。

## 配置与卸载

首次启动生成 `BepInEx/config/com.codex.sephiria.merchantchoice.cfg`。

```ini
[Shop]
EnableChoiceCatalog = true
```

设为 `false` 后重启，恢复原版刷新入口。卸载时退出游戏，移走插件 DLL 即可。

## 兼容性与已知限制

- 早期合并版的单机选购流程已获使用者实际反馈可用；独立版通过编译、引用检查和拆分前后逻辑对照，但尚未完成独立版全面实机回归。
- **加入别人房间时的购买同步未验证，不保证联机可用。** 联机使用前请和队友沟通并备份存档。
- 当前界面文案为简体中文，商品名称沿用游戏语言。
- 目录唤出的介绍不拦截鼠标，因此不能点击介绍内部的关键词；原版背包介绍不受这项处理影响。
- 本插件不包含快速重开、局内偏好显示修复或原版商店列表爱心功能。

遇到问题请在 Issues 提供游戏版本、BepInEx 版本、单机/房主/客户端身份、复现步骤、截图和相关日志片段。发送日志前请删去账号、路径等个人信息。

## 从源码构建

需要 Windows .NET Framework 4 C# 编译器（脚本从 Windows 目录查找）、已安装游戏以及 BepInEx 6。

```powershell
./Build.ps1 -GameRoot 'D:\Games\Sephiria'
```

输出：`build/SephiriaMerchantChoice.dll`。构建只读取本机依赖，不下载、不打包游戏程序集，也不自动安装插件。

`src/FavoriteSupport.cs` 是本地独立插件系列共用的偏好辅助源码；此项目以 `MERCHANT_CHOICE` 编译，仅注册自选目录的补丁，不依赖或启用原版商店爱心插件。

## 许可

本仓库的插件源码以 [MIT License](LICENSE) 发布。游戏、游戏内美术及外部运行库不属于本仓库许可范围，见 [第三方说明](THIRD_PARTY_NOTICES.md)。
这是非官方玩家 mod，与游戏开发者不存在隶属或背书关系。
