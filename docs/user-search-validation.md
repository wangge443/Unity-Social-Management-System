# 搜索添加好友验证记录

日期：2026-09-23。仅替换发送好友申请时手工输入用户 ID 的交互，不开启新阶段。

## 修改文件

- backend/SocialSystem.Api/Controllers/FriendsController.cs：新增搜索 GET 入口，沿用 JWT。
- backend/SocialSystem.Api/Services/FriendService.cs：用户名/昵称查询、分页和好友关系状态投影；原发送、同意、拒绝、删除逻辑未修改。
- backend/SocialSystem.Api/DTOs/Friends/FriendDtos.cs：UserSearchResponse、UserSearchListResponse。
- backend/SocialSystem.Api.Tests/FriendsApiTests.cs：新增搜索认证、匹配、分页、参数、关系状态及重复请求回归。
- client/SocialSystem.Unity/Assets/Scripts/Friends/FriendApi.cs：搜索 DTO 与统一 ApiClient GET 封装。
- client/SocialSystem.Unity/Assets/Scripts/Friends/FriendsView.cs：移除 ReceiverInput，增加搜索添加页和状态按钮。
- client/SocialSystem.Unity/Assets/Tests/ClientSocialSmokeTest.cs：改为通过用户名/昵称搜索并点击添加的完整好友流程。
- docs/API_REFERENCE.md、client/README.md、本文：同步实际接口和验收步骤。

备份：client/Build/Backups/user-search-20260923-231046。保护清单 117 个文件哈希一致，确认本轮未改数据库、消息、动态、注册登录和 JWT 相关文件。没有数据库迁移或表结构变更。

## API 与流程

扫描现有 Controller 确认原项目没有用户搜索接口。本次在已有好友 Controller/Service 中增加最小入口：

`GET /api/friends/search-users?keyword=xxx&page=1&pageSize=20`

要求 JWT。keyword 必填、非空白、最多 32 字符，去掉首尾空白后进行包含匹配；page 为 1–1000000，pageSize 为 1–100。按用户名和内部用户 ID 升序分页，排除登录者本人。响应包含 items、page、pageSize、total；每项仅返回 userId、username、nickname、relationship。

关系状态：none 可添加；friends 已是好友；outgoing 已申请；incoming 对方已申请（去现有申请列表同意/拒绝）。不返回密码或哈希。用户名列原为 ASCII，非 ASCII 关键词只查询昵称，避免中文关键词与 ASCII 列比较的字符集冲突；不修改数据库字符集。百分号等作为普通文本，不是搜索通配符。

Unity：好友页→添加好友→输入用户名/昵称→搜索→查看昵称和用户名→添加好友。每页 10 条，结果可滚动和翻页。userId 只存在内部数据及控件标识中，搜索界面不显示。点击后使用原 POST /api/friends/request，成功后按钮显示已申请且禁用；已是好友、对方已申请同样不能重复点击添加。

状态是搜索时的快照，原发送接口仍在事务中检查双方关系及待处理申请，防止通过旧结果或并发请求重复添加。409/404 时客户端显示错误并尝试刷新搜索结果；重新进入搜索页也会重新查询。发送超时后应重新搜索确认状态，再决定是否重试。

## 自动验证

| 检查 | 结果 |
| --- | --- |
| 后端 build | 通过，0 警告、0 错误 |
| 后端集成测试 | 56/56 通过，使用隔离 MySQL；新增 4 项搜索测试用例 |
| Unity C# 编译 | 通过，无 C# 编译错误 |
| Windows Development Build | user-search-final 构建通过 |
| Unity 社交端到端 | 通过：A 搜索 B→点击添加→B 收到并同意→好友列表→点击好友进入聊天 |
| 搜索状态 | 用户名/中文昵称匹配、隐藏 ID、排除自己、空输入、空结果、已申请/对方已申请/已是好友、重复点击与后端 409 通过 |
| 原好友功能 | 收到多条申请、同意、拒绝、删除、聊天入口与历史消息回归通过 |
| 其他模块回归 | 登录/退出重登、JWT 失效、动态发布/评论查看/点赞/删除均通过，业务代码未改 |
| UI | 已检查 960×540 实际搜索截图；另生成 1280×720 截图 |

首次集成测试暴露中文关键词与 ASCII 用户名列的排序规则冲突，调整查询分支后全量测试通过。最终日志保存在 client/Build/ValidationLogs/UserSearch：backend-build.txt、backend-tests-final.txt、social-final.txt、user-search-final.log。

Windows 产物完整同步到 client/SocialSystem.Unity/Build，Assembly-CSharp.dll 与验证产物哈希一致。自动交互测试在 Unity Editor 内使用真实 HTTP 和隔离 MySQL；Windows exe 人工操作尚未验收。原本运行的本地后端已恢复，匿名访问 /api/auth/me 返回预期 401。

## Unity 人工验收

1. 启动本地后端，在 Unity 打开 Assets/Scenes/LoginScene.unity 并 Play，或运行 client/SocialSystem.Unity/Build/SocialSystem.Client.exe，保留构建目录全部依赖。
2. A 登录，好友→添加好友，分别输入 B 的用户名、昵称搜索。确认显示 B 昵称/用户名，无用户 ID；自己的账号不出现在搜索结果。
3. 点击 B 的添加好友，确认提示已发送，按钮变成“已申请”且不可重复点击。重新搜索仍应显示已申请。
4. B 登录，打开好友页，收到申请列表自动显示 A。也可搜索 A，确认显示“对方已申请”；返回申请列表点击同意。
5. 确认 B 好友列表出现 A；A 重新登录或刷新好友列表出现 B。再次搜索显示“已是好友”。点击好友昵称或聊天，确认对象正确且可发送消息。
6. 使用另一组账号搜索并发送申请，接收方拒绝；双方不成为好友，重新搜索可以再次添加。
7. 输入不存在的名称，应出现空结果；空白关键词应提示输入。准备多条匹配记录时测试滚动和分页。
8. 验证已有好友申请列表、删除好友、历史聊天以及原动态和登录流程正常。

本轮完成后停止，未增加其他功能。
