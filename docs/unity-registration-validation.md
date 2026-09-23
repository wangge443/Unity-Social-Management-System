# Unity 注册页面补充验证

日期：2026-09-23。本次仅补充现有登录模块的注册入口和注册页面，不开启新开发阶段。

## 改动文件

- Assets/Scripts/Auth/LoginSceneView.cs：增加“没有账号？注册”入口，微调登录按钮和提示位置。
- Assets/Scripts/Auth/LoginSceneController.cs：注册输入校验、错误提示及注册后的自动登录，复用同一 LoginFlow。
- Assets/Scripts/Auth/RegisterView.cs 及 .meta：新增运行时注册界面，沿用 SocialTheme 与 SocialUi。
- Assets/Tests/ClientConnectionSmokeTest.cs：保留原登录连接回归，增加真实注册 UI 回归。
- 本文：记录验证结果和人工步骤。

以上 Assets 路径相对于 client/SocialSystem.Unity。LoginScene 无需重新挂脚本。AuthManager、TokenManager、ApiClient、HomeView、好友、聊天、动态源码和后端、数据库均未修改；102 个受保护文件通过修改前后 SHA256 核对。

备份：client/Build/Backups/register-ui-20260923-165907。备份及构建日志为本机忽略产物，不提交凭据。

## 注册流程

登录页点击注册 → 填写用户名、昵称、密码、确认密码 → 本地校验 → AuthManager.Register 调用 POST /api/auth/register → 成功后调用现有 AuthManager.Login → 保存 JWT → GET /api/auth/me 验证身份 → 打开现有 HomeView。

用户名要求 3–32 位 ASCII 字母、数字或下划线；昵称非空白且最多 32；密码 8–128 且非全空白，两次输入必须一致。长度规则与现有 DTO 对齐，输入框限制最大长度。

请求期间禁用重复提交和页面切换。重复用户名显示专用中文提示，断网提示无法确认提交结果，不自动重试注册。注册成功但后续自动登录或身份验证失败时，回到登录界面提示账号已注册，用户可重新输入密码登录。密码在离开注册界面或成功注册后清空，不写入 PlayerPrefs。

## 自动验证结果

| 项目 | 结果 |
| --- | --- |
| Unity 6000.6.2f1 编译与 Windows Development Build | 通过，未发现 C# 编译错误 |
| 注册入口、返回登录和密码清理 | 通过 |
| 用户名、空白昵称、短密码、两次密码不一致 | 通过 |
| 真实 API 重复用户名 409 | 显示明确提示，不进入主页 |
| 断网后提示和恢复交互 | 通过 |
| 密码掩码、提交期间禁用按钮 | 通过 |
| 注册 → 自动登录 → JWT → /auth/me → HomeView | 通过 |
| 注册账号退出后再次从原登录按钮登录 | 通过 |
| 原登录、Bearer GET、POST/DELETE 204、退出与错误密码 | 通过 |
| 既有双账号好友、聊天、动态及认证回归 | 通过 |
| UGUI 截图 | 输出 960×540 与 1280×720；检查登录、注册页面布局与密码掩码 |

回归在 Unity Play Mode 中连接临时 API 和隔离 MySQL，使用随机测试账号；未访问已有业务数据库。没有重跑后端完整测试套件，未修改后端代码。注册后的自动登录失败分支有实现，但本轮未针对注册成功后中途断网单独注入故障。

本机结果：client/Build/ValidationLogs/RegisterUI/connection.txt、social.txt、register-ui.log。新版 Windows 客户端：client/SocialSystem.Unity/Build/SocialSystem.Client.exe。UI 截图保存在该 Build/UI 目录，名称以 register- 开头。自动验证与截图检查不替代用户本机人工验收。

## 人工验收

1. 启动本机原有后端，打开原 LoginScene 并 Play，或运行上述新版 Windows 客户端；保留 exe 同目录全部文件。
2. 点击“没有账号？注册”，确认出现用户名、昵称、密码、确认密码、“注册”和“返回登录”。
3. 输入非法用户名、空昵称、短密码或不一致密码，分别确认中文提示；用已有用户名注册应提示已被注册。
4. 填写新的合法用户名及昵称，输入两次相同密码并点击注册。无需再次点击登录，应直接进入 HomeView，显示新账号用户名、昵称和 ID。
5. 退出登录，使用刚注册的账号通过原登录按钮登录，确认能够再次进入主页。
6. 在注册页面点击返回登录，确认两处密码清空；停止后端后尝试注册，应提示连接问题并恢复按钮，重启后端后可以继续。
7. 与另一个账号建立好友关系，点击好友聊天并收发消息；发布动态、评论、点赞，检查原业务正常。
8. 若账号已创建但自动登录失败，按提示在原登录页重试，无需重复注册。

## 复现命令

从项目根目录，使用 PowerShell 7.4+：

```powershell
./client/scripts/Build-Client.ps1 -Label register-ui
./client/scripts/Test-Connection.ps1 -Mode connection -CaptureUi -UnityProject 'client/Build/Stage8Validation'
./client/scripts/Test-Connection.ps1 -Mode social -UnityProject 'client/Build/Stage8Validation'
```

构建脚本输出在独立验证副本的 Build 下；本次交付已同步至源工程 Build 目录。未提交或推送 Git。
