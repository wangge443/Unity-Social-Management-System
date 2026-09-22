# Unity 社交客户端

工程：`client/SocialSystem.Unity`，Unity **6000.6.2f1**、UGUI **2.6.0**，Windows Editor / Windows Development Build。

第八阶段已实现用户主页、好友申请及列表、私聊、动态发布/评论/点赞。第九阶段第一批完成统一深色主题、中文登录布局和独立 HomeView，显示当前用户昵称、用户名和 ID，并提供三个模块入口。沿用 ApiClient、TokenManager 和 AuthManager，未修改后端 API 逻辑或数据库结构。

## 启动

1. 从项目根目录运行：
   ```powershell
   dotnet run --project backend/SocialSystem.Api --launch-profile http --no-build
   ```
   后端默认监听 `http://localhost:5080`。缺少编译产物时先执行 `dotnet build backend/SocialSystem.sln`。
2. Unity Hub 打开现有 `client/SocialSystem.Unity`，双击 `Assets/Scenes/LoginScene.unity`，点击 Play。
3. 用已有账号登录，认证验证成功后自动进入主页。新界面由脚本在运行时创建，无需手工挂脚本或配置新的场景。
4. 在主页可查看用户 ID；通过导航打开好友、消息和动态，或退出登录。
5. 没有账号时通过后端 Swagger 的注册接口准备账号。注册界面不在本次第八阶段业务开发范围内。

也可启动最新的 `client/SocialSystem.Unity/Build/SocialSystem.Client.exe`。请保留同目录全部构建文件，不能单独移动 exe。

默认地址可在 LoginScene 的 **ClientServices → ApiClient → Base Url** 修改。使用同机 Windows 环境；本地 HTTP 仅允许 Editor / Development Build。新增业务 UI 使用 Windows 字体，支持中文；用户输入按纯文本显示。

## 已实现功能和边界

| 模块 | 操作 | 边界 |
| --- | --- | --- |
| 主页 | 昵称、账号、用户 ID、退出登录 | 不编辑个人资料 |
| 好友 | 分页列表、按用户 ID 发送申请、按申请 ID 接受、二次确认删除 | API 没有用户搜索、收到申请列表、拒绝接口；发送方将成功响应中的申请 ID 告知接收方 |
| 消息 | 点击好友进入聊天、最近会话、消息气泡、发送文本、手动刷新 | 每次获取完整历史，无实时推送；已记录联系人删除后仍可从最近会话读历史，但不能发送 |
| 动态 | 分页广场、发布、评论提交、点赞/取消点赞、删除自己的动态 | 所有登录用户可见；API 无历史评论接口，只显示计数及本次会话最近提交的评论 |

输入框限制消息/动态 2000 字符、评论 500 字符，并检查空白内容与正整数 ID。请求中禁用操作，避免重复提交；失败后恢复交互并保留可重试的输入。超时不自动重试写请求，应先刷新确认服务器是否已完成操作。

登录、主页和业务页面在**同一场景**内切换，复用一套服务，不创建跨场景单例。JWT 仅保存在内存；退出或令牌失效时销毁业务视图、清理会话，重新登录会创建新的视图与数据。当前不支持在外部场景中保留会话。

## 代码结构

- `Scripts/Network`：统一 HTTP、JWT 请求头、JSON 和错误处理。
- `Scripts/Auth`：已有认证业务与登录界面，登录完成后打开主页。
- `Scripts/Main/SocialAppController.cs`：导航、当前会话、请求忙碌状态、退出与过期处理。
- `Scripts/UI/SocialUi.cs`：共享 UGUI 布局、输入框、滚动列表与文本。
- `Scripts/Friends`：FriendApi、DTO、FriendsView。
- `Scripts/Messages`：MessageApi、DTO、ChatView、ConversationStore；HTTP 历史解析与本地联系人入口。
- `Scripts/Posts`：PostApi、DTO、PostsView。
- `Assets/Tests`：仅 Editor 编译的原连接测试及第八阶段双账号测试。

业务视图调用各模块 API 封装，所有网络请求最终由既有 ApiClient 发出；Unity 不访问 MySQL。

## 构建和自动验证

使用 PowerShell 7.4+，从项目根目录执行：

```powershell
./client/scripts/Build-Client.ps1 -Label client
./client/scripts/Test-Connection.ps1 -Mode social -UnityProject 'client/Build/Stage8Validation'
./client/scripts/Test-Connection.ps1 -Mode connection -UnityProject 'client/Build/Stage8Validation'
```

Build-Client 将 Assets、Packages、ProjectSettings 复制到忽略目录 `client/Build/Stage8Validation`，使用独立工程编译并生成 Windows 开发版，不占用已打开的源工程。构建结果位于该目录的 Build 子目录，日志为 `<Label>.log`。副本保留用于后续测试及增量构建。

Test-Connection 默认仍运行原连接测试；`-Mode social` 运行第八阶段完整社交测试。未指定 UnityProject 时使用源工程，此时需要先关闭对应 Editor。可通过 `-Unity`、`-Dotnet`、`-MySqlBin` 指定安装路径。

测试使用随机账号、独立临时 MySQL 和临时 API，不连接现有 social_system；结束后清理临时进程、数据库与测试日志，不记录真实凭据。

逐模块人工步骤和本次结果见 [第八阶段验证记录](../docs/unity-social-validation.md)。


第九阶段登录布局在 Play 或 Windows 客户端运行时应用，原 LoginScene 引用保持不变。备份、文件清单和验收结果见 [界面验证记录](../docs/unity-ui-validation.md)。Test-Connection 可加 -CaptureUi 生成登录与主页的实际 UGUI 截图；默认测试方式不变。


第九阶段第二轮已优化好友条目及消息气泡，点击好友昵称或“聊天”自动选择对象；聊天页不再输入用户 ID。原发送/接受申请位于“好友申请”页，删除仍需确认。最近联系人通过 PlayerPrefs 按账号和服务器保存基本信息，不保存消息、密码或 JWT；它不等于服务端完整会话列表，无法自动发现本机未记录且已删除的联系人。文件、备份和测试结果见 [好友聊天验证](../docs/unity-chat-validation.md)。
