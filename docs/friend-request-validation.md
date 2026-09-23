# 好友申请列表交互验证

日期：2026-09-23。本次仅完善好友申请交互，不新增数据库结构或其他业务功能。修改前备份位于 client/Build/Backups/friend-requests-20260923-173124。

## 修改文件

| 文件 | 内容 |
| --- | --- |
| backend/SocialSystem.Api/Controllers/FriendsController.cs | 新增收到的申请列表、拒绝申请路由，继承控制器 JWT 要求 |
| backend/SocialSystem.Api/Services/FriendService.cs | 分页查询接收者待处理申请；事务拒绝、接收者权限及状态检查 |
| backend/SocialSystem.Api/DTOs/Friends/FriendDtos.cs | 新增申请列表与申请人公开信息响应 |
| backend/SocialSystem.Api.Tests/FriendsApiTests.cs | 新增认证、隔离、分页、拒绝、重复处理及并发测试 |
| client/SocialSystem.Unity/Assets/Scripts/Friends/FriendApi.cs | 封装新接口和对应 DTO |
| client/SocialSystem.Unity/Assets/Scripts/Friends/FriendsView.cs | 移除手填申请 ID，自动显示申请人、同意/拒绝按钮、滚动及分页 |
| client/SocialSystem.Unity/Assets/Tests/ClientSocialSmokeTest.cs | 点击真实申请按钮的同意、拒绝、多记录与旧功能回归 |
| docs/API_REFERENCE.md、client/README.md、本文件 | 新接口与操作、验证说明 |

此前注册功能的未提交改动原样保留。Auth、JWT、网络模块、注册页面、消息、动态和数据库定义未修改；本轮 115 个受保护文件哈希核对无变化。

## 新接口

- GET /api/friends/requests?page=1&pageSize=20：仅查询 JWT 当前用户收到的 Pending 申请，按申请 ID 降序。响应包含 items、page、pageSize、total；每条为 requestId、senderId、username、nickname、createdAt。
- POST /api/friends/reject/{requestId}：仅接收者能拒绝待处理申请，返回原 FriendRequestResponse 格式，status=rejected。
- POST /api/friends/accept/{requestId}：复用已有接口及服务逻辑。

新接口均要求 JWT。查询不能通过传入 receiverId 查看其他人的申请。非接收者处理返回 403，不存在返回 404，已处理返回 409；参数无效返回 400。

拒绝复用 friend_requests.status=2 和 handled_at，保留历史、不创建好友记录。同意复用 status=1 和好友关系创建逻辑。拒绝与同意使用相同的用户行锁顺序，在事务中重新检查 Pending 状态，因此并发同意/拒绝只有一个成功。已拒绝后可重新发送一条新申请。

## 新用户流程

A 在“好友申请”页填写对方用户 ID 发送申请，提示等待处理，不显示申请 ID。发送方式保持原样，没有新增用户搜索。

B 登录并打开“好友”页，自动查询并显示收到的申请；也可进入“好友申请”页查看。每项仅展示申请人昵称、用户名、“同意”和“拒绝”，requestId 与 senderId 保留在程序数据中，不出现在 UI 文本或输入框。

同意：按钮闭包取得该申请 ID → 原接受接口 → 从当前申请列表移除 → 刷新好友及申请列表。拒绝：按钮取得 ID → 拒绝接口 → 移除该条待处理申请 → 刷新列表，不建立好友关系。

申请列表每页 10 条，与好友分页独立，支持滚动、上一页、下一页和手动刷新。处理最后一页最后一条时自动回退有效页。请求期间禁用重复交互；其他设备已处理的申请会提示并尝试刷新，不再要求填写新申请 ID。打开页面自动刷新，不提供实时推送。

## 自动验证

后端 dotnet build 已通过，0 警告、0 错误。完整隔离 MySQL 集成测试 48/48 通过，其中好友测试 14/14；覆盖原 Auth、Message、Post 回归和新增好友权限、分页及并发测试。

Unity 测试通过真实临时 HTTP API 和隔离 MySQL 执行，不写入已有业务库。测试账号随机生成。A→B 同意验证双方好友关系；C→D 拒绝验证双方不成为好友；另一条申请并存验证仅移除目标记录。还检查多记录滚动、重复或失效动作、空列表与申请人纯文本显示。

首轮 Unity C# 编译、Windows 构建和完整社交回归通过。随后仅修正了过期申请错误提示（不再提及申请 ID）并增加滚动检查；这些最终改动的重建在 Unity 许可证握手阶段停滞，最终社交复测及本轮注册/登录回归尚未完成，不应视为已通过。

最终 Unity 构建与回归结果以 client/Build/ValidationLogs/FriendRequests 下日志为准。原注册/登录连接回归待许可连接恢复后单独执行，覆盖注册后自动登录、JWT 和 /auth/me、HomeView、退出及再次登录。

本机开发后端曾占用构建文件，核实进程后重启；未停止 MySQL。最终构建重试记录保留，未把启动停滞当作构建成功。

## Unity 人工验收

1. 启动更新后的后端，打开原 LoginScene 并 Play，或运行更新后的 client/SocialSystem.Unity/Build/SocialSystem.Client.exe；无需重新挂脚本。
2. 准备 A、B 账号，在主页取得 B 的用户 ID。A 进入“好友申请”页发送申请，不应显示 requestId。
3. B 登录后打开“好友”，无需输入申请 ID 即可看到 A 的昵称、用户名和“同意”“拒绝”。
4. 点击“同意”，该申请消失，好友列表出现 A。A 打开或刷新好友列表应看到 B；点击好友可继续聊天。
5. 另用 C、D 两个账号发送申请，D 点击“拒绝”，该条消失；双方刷新好友列表均不能出现对方。C 可以再次发送新申请。
6. 多账号向同一接收者申请，验证可滚动显示多条；超过 10 条时使用“申请下一页”，处理后分页应正常回退。
7. 两个客户端登录同一接收账号：一处先处理，另一处再次点击旧申请，应提示已处理并刷新，不产生重复关系。
8. 回归新用户注册自动登录、原登录、好友删除、双向消息、动态评论与点赞。

## 复现命令

```powershell
# 项目根目录；先关闭或停止占用编译输出的本机后端
Set-Location backend
dotnet build SocialSystem.sln --no-restore
./scripts/Test-Auth.ps1
Set-Location ..
./client/scripts/Build-Client.ps1 -Label friend-requests-release
./client/scripts/Test-Connection.ps1 -Mode social -CaptureUi -UnityProject 'client/Build/Stage8Validation'
./client/scripts/Test-Connection.ps1 -Mode connection -CaptureUi -UnityProject 'client/Build/Stage8Validation'
```

自动与截图验证不替代全部人工验收。本轮没有提交或推送 Git。
