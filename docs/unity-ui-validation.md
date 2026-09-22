# 第九阶段第一批：统一视觉、登录页与主页

日期：2026-09-22。本批只实施已确认的第 1–3 项，保留现有好友、消息、动态操作及手填聊天 ID 功能，不提前实施下一批交互调整。

## 结果

- 新增 SocialTheme，统一深色背景、中文字体、按钮颜色及悬停/按下/禁用状态、输入框和间距。
- 新增 LoginSceneView，运行时调整现有 LoginScene 控件布局与中文提示。保留输入框、按钮引用、密码掩码、登录调用和 /api/auth/me 的 JWT 身份核对。
- 新增独立 HomeView，显示当前用户的昵称、用户名和用户 ID；提供“进入好友”“查看消息”“浏览动态”三个入口。
- ChatView 仅把原导航回调提取为 OpenPage，供主页复用，原手填 ID、历史和发送逻辑保留。
- 登录成功、退出和账号切换仍由原认证组件及 SocialAppController 管理；无新网络实现。

LoginScene.unity 本身未改写；进入 Play 或运行构建后应用新样式。Editor 非运行状态仍可能显示旧场景布局，这是运行时展示层的实现方式，不需要手工挂接新脚本。

## 修改前备份

备份目录：[stage9-ui-20260922-162946](../client/Build/Backups/stage9-ui-20260922-162946)。

备份包括修改前的界面脚本、测试、客户端说明和项目状态，以及原 LoginScene 和场景 meta；额外在修改测试启动脚本前备份了该脚本。manifest.json 保存相对路径和 SHA256。protected-files.json 记录后端、数据库、认证业务和网络文件原始哈希，用于交付检查。备份是本机忽略产物，不提交 Git。

## 修改文件列表

| 类型 | 文件 | 用途 |
| --- | --- | --- |
| 新增 | [SocialTheme.cs](../client/SocialSystem.Unity/Assets/Scripts/UI/SocialTheme.cs) | 统一字体、色彩和控件状态 |
| 修改 | [SocialUi.cs](../client/SocialSystem.Unity/Assets/Scripts/UI/SocialUi.cs) | 共享组件使用主题与统一间距 |
| 新增 | [LoginSceneView.cs](../client/SocialSystem.Unity/Assets/Scripts/Auth/LoginSceneView.cs) | 登录控件展示、布局和中文文案 |
| 修改 | [LoginSceneController.cs](../client/SocialSystem.Unity/Assets/Scripts/Auth/LoginSceneController.cs) | 调用展示层、中文状态提示；保留认证流程 |
| 新增 | [HomeView.cs](../client/SocialSystem.Unity/Assets/Scripts/Main/HomeView.cs) | 用户信息和三个模块入口 |
| 修改 | [SocialAppController.cs](../client/SocialSystem.Unity/Assets/Scripts/Main/SocialAppController.cs) | 接入 HomeView、统一背景及退出清理 |
| 修改 | [ChatView.cs](../client/SocialSystem.Unity/Assets/Scripts/Messages/ChatView.cs) | 提取可复用的消息页入口，不改发送或历史逻辑 |
| 修改 | [ClientSocialSmokeTest.cs](../client/SocialSystem.Unity/Assets/Tests/ClientSocialSmokeTest.cs) | 增加中文登录、主页字段与账号切换、三个入口检查 |
| 新增 | [ClientUiCapture.cs](../client/SocialSystem.Unity/Assets/Tests/ClientUiCapture.cs) | 仅 Editor 编译的实际 UGUI 渲染截图工具 |
| 修改 | [Test-Connection.ps1](../client/scripts/Test-Connection.ps1) | 新增可选 -CaptureUi；默认无图形测试方式保留 |
| 修改 | [client/README.md](../client/README.md)、[PROJECT_STATUS.md](PROJECT_STATUS.md) | 更新当前进度、运行和验证说明 |
| 新增 | 本文件 | 修改清单、备份位置及验收记录 |

四个新增 C# 文件同时包含 Unity 生成的 .meta。后端接口、数据库 SQL、ApiClient、ApiResponse、AuthManager、AuthDtos、TokenManager 未修改；原场景文件及其引用保留。

## 测试结果

| 检查 | 结果 |
| --- | --- |
| Unity 6000.6.2f1 Windows Development Build | 初次及布局修正后的最终构建均通过，未发现 C# 编译错误 |
| 原连接回归 | 注册、真实登录按钮、JWT/Bearer GET、POST/DELETE 204、退出、错误密码通过 |
| 双账号社交回归 | 好友申请/接受/删除、聊天、动态评论/点赞/删除、断网恢复、401、过期及重新登录通过 |
| 第九阶段新增检查 | 中文登录按钮与字体、密码掩码、主页三个真实用户字段、切换账号后数据、三个模块入口通过 |
| 实际 UGUI 渲染 | 检查 960×540、1280×720 的登录页和主页；中文可见，主页快捷入口完整显示 |
| 数据与接口保护 | 通过文件哈希核对受保护源码；不修改后端逻辑或数据库结构 |

初次截图发现主页底部按钮略有裁切，已去除重复标题占用并重新构建、回归及渲染检查。截图中的账号为隔离测试随机账号，昵称中的 <b> 等字符故意按原文显示，用于检查用户文本没有被解释为富文本。

自动回归使用独立临时 MySQL 和临时 API，不连接现有 social_system。本次未重跑后端完整 41 项测试套件。界面截图检查不代替用户本机全部人工交互验收。

构建日志及测试摘要保存在 client/Build/ValidationLogs/Stage9。最终 Windows 客户端与截图位于 client/SocialSystem.Unity/Build，UI 子目录有 login-960.png、login-1280.png、home-960.png、home-1280.png。

## 人工验收

1. 启动现有后端，打开原 LoginScene 并 Play，或运行最新 Windows 客户端；无需新增场景或手工挂脚本。
2. 检查深色登录卡片、中文字段/状态、密码掩码。空输入、错误密码和断网应有提示；正确登录进入主页。
3. 核对昵称、用户名、用户 ID，分别点击“进入好友”“查看消息”“浏览动态”，再通过顶部“主页”返回。
4. 退出 A、登录 B，主页应显示 B 的数据；好友、消息和动态原功能应仍可操作。
5. 检查按钮悬停、点击、禁用反馈及窗口尺寸。登录页新布局只在运行时出现。

## 复现

```powershell
./client/scripts/Build-Client.ps1 -Label stage9-ui
./client/scripts/Test-Connection.ps1 -Mode social -CaptureUi -UnityProject 'client/Build/Stage8Validation'
./client/scripts/Test-Connection.ps1 -Mode connection -UnityProject 'client/Build/Stage8Validation'
```

-CaptureUi 使用图形设备，在测试工程 Build/UI 下输出截图；不传该参数时保留原 -nographics 方式。构建副本路径沿用既有脚本名称，不表示仍在开发第八阶段。本次临时验证副本完成交付后清理，修改前备份和正式日志保留。
