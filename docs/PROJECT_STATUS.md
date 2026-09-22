# SocialSystem 项目状态

更新日期：2026-09-22。本文汇总当前源码、数据库定义、已有阶段文档及验证记录。

阶段 1–8 的既定开发内容已完成；第八阶段四个 Unity 业务模块及最终 Windows 开发版构建通过，双账号端到端测试通过。**第八阶段人工视觉与交互验收仍待用户完成**，不能将自动测试通过等同于全部人工验收通过。

最新进度：第九阶段第一批（统一视觉、中文登录页、独立 HomeView）已完成，Windows 构建、原登录及双账号社交回归通过，并检查了两种尺寸的实际渲染截图。第二轮已完成好友列表点击聊天、自动传递身份、移除手填聊天 ID、最近会话及消息气泡，最终 Windows 构建、登录和双账号回归通过。后端、数据库和 JWT 流程保持不变。详见 [第一轮界面验证](unity-ui-validation.md)及 [第二轮好友聊天验证](unity-chat-validation.md)。

## 1. 项目介绍

本项目是基于 Unity 的社交管理系统课程项目，面向 Windows 本机开发、课程演示与答辩。核心业务包括账号注册登录、好友关系、私聊消息和文字动态互动，优先保证能够运行、功能清楚、数据关系合理和代码容易理解。

系统调用链为：

```text
Unity Client（C# / UGUI）
    → HTTP / JSON
ASP.NET Core Web API
    → Entity Framework Core
MySQL social_system
```

Unity 不直接连接 MySQL，所有业务数据操作均通过后端 API。数据库设计预留的字段或状态不代表相应 API 和客户端功能已经实现。

| 技术 | 当前版本或约定 |
| --- | --- |
| 开发环境 | Windows |
| Unity Editor | 6000.6.2f1（Unity 6.6） |
| Unity UI | UGUI 2.6.0 |
| .NET SDK / 目标框架 | 9.0.318 / net9.0 |
| ASP.NET Core | 9 |
| EF Core / Relational / JwtBearer | 9.0.20 |
| MySQL Provider | Pomelo.EntityFrameworkCore.MySql 9.0.0 |
| MySQL Server | 8.0.x，本机已有验证版本为 8.0.46 |
| Swagger | Swashbuckle.AspNetCore 10.2.3，仅 Development 开放 |

后端主要版本固定为 .NET 9、EF Core 9 和 Pomelo 9.0.0，不混用 EF Core 8、9、10，不改为 net10.0。当前不实现语音/视频、文件传输、复杂群聊、微服务或云服务器部署。

## 2. 已完成阶段 1–8 的功能

以下“完成”指各阶段实际交付范围，不表示所有最初设想的功能均已实现。

| 阶段 | 名称 | 已完成内容 | 当前边界 |
| --- | --- | --- | --- |
| 1 | 数据库设计与验证 | social_system 七张表；字段、主外键、唯一约束、索引与 CHECK；初始化、检查、验证 SQL；设计文档 | 初始化脚本面向首次建库，不是可反复执行的迁移工具 |
| 2 | 后端基础与数据库连接 | ASP.NET Core 工程、七个实体、EF 映射、SocialDbContext、UTC 连接拦截器、只读数据库探针、本机连接配置 | 不自动建表、删表或迁移 |
| 3 | Auth 注册登录 | 用户名/密码/昵称验证、用户名唯一性、密码哈希、JWT 签发和校验、查询本人摘要 | 不支持邮箱登录、个人资料编辑或令牌刷新接口 |
| 4 | Friend 好友系统 | 发送申请、接收者接受申请、分页好友列表、删除好友、权限与并发重复检查 | 没有用户搜索、申请列表或拒绝申请接口 |
| 5 | Message 消息系统 | 好友发送文本、本人参与的双向消息历史、身份及好友关系校验 | 无实时推送、历史分页或已读接口；删好友后仍可读本人历史 |
| 6 | Post 动态系统 | 发布、分页列表、删除本人动态、评论提交、点赞/取消点赞、计数及当前用户点赞状态 | 面向所有登录用户；没有评论列表或评论删除接口 |
| 7 | Unity 登录与连接基础 | ApiClient GET/POST/DELETE、内存 JWT、注册登录调用、LoginScene、Bearer 验证、Windows 开发版、连接测试 | 有注册调用但无注册 UI |
| 8 | Unity 社交业务界面 | 主页、退出及会话失效；好友分页、申请/接受/删除；聊天发送及历史；动态发布/评论/点赞/删除；忙碌及错误提示 | 按 ID 处理申请；消息手动刷新；不显示完整评论历史；人工界面验收待完成 |

第八阶段复用已有 ApiClient、TokenManager 和 AuthManager，没有修改数据库结构和 ASP.NET Core API 逻辑。第九阶段第一批继续保留这三项基础组件，仅统一展示样式、优化登录页，并提取独立主页；第二轮已优化好友列表与聊天交互，保留原申请、删除和消息历史请求。

## 3. 当前项目目录结构

以下省略大部分 Unity .meta、项目设置细项及 Library、Temp、bin、obj 等生成文件；Build 为本机产物，Git 忽略。

```text
project1/
├── AGENTS.md
├── README.md
├── .gitignore
├── backend/
│   ├── global.json
│   ├── SocialSystem.sln
│   ├── README.md
│   ├── SocialSystem.Api/
│   │   ├── Controllers/         Auth、Friends、Messages、Posts、Health
│   │   ├── Services/            AuthService、FriendService、MessageService、
│   │   │                       PostService、TokenService、JwtSettings
│   │   ├── Models/Entities/     七个实体、FriendRequestStatus
│   │   ├── DTOs/               Auth、Friends、Messages、Posts、数据库检查响应
│   │   ├── Data/
│   │   │   ├── Configurations/  七张表的 EF 映射
│   │   │   ├── SocialDbContext.cs
│   │   │   └── UtcConnectionInterceptor.cs
│   │   ├── Middleware/ApiExceptionHandler.cs
│   │   ├── OpenApi/SwaggerAuthorizationFilter.cs
│   │   ├── Properties/launchSettings.json
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── SocialSystem.Api.csproj
│   ├── SocialSystem.Api.Tests/
│   │   ├── AuthApiTests.cs
│   │   ├── FriendsApiTests.cs
│   │   ├── MessagesApiTests.cs
│   │   ├── PostsApiTests.cs
│   │   └── SocialSystem.Api.Tests.csproj
│   └── scripts/
│       ├── Test-Auth.ps1
│       └── Test-AuthManual.ps1
├── client/
│   ├── README.md
│   ├── scripts/
│   │   ├── Build-Client.ps1
│   │   └── Test-Connection.ps1
│   ├── Build/ValidationLogs/    home/friends/chat/posts/final.log、social-test.txt
│   └── SocialSystem.Unity/
│       ├── Assets/
│       │   ├── Scenes/LoginScene.unity
│       │   ├── Scripts/
│       │   │   ├── Network/     ApiClient、ApiResponse
│       │   │   ├── Auth/        AuthDtos、AuthManager、TokenManager、
│       │   │   │               LoginSceneController、LoginSceneView
│       │   │   ├── Main/        SocialAppController、HomeView
│       │   │   ├── UI/          SocialUi、SocialTheme
│       │   │   ├── Friends/     FriendApi、FriendsView
│       │   │   ├── Messages/    MessageApi、ChatView、ConversationStore
│       │   │   └── Posts/       PostApi、PostsView
│       │   ├── UI/              预留资源目录
│       │   ├── Editor/          ClientProjectSetup、ClientSmokeRunner
│       │   └── Tests/           ClientConnectionSmokeTest、ClientSocialSmokeTest、ClientUiCapture
│       ├── Packages/
│       ├── ProjectSettings/
│       └── Build/              Windows 开发版及其依赖文件
├── database/
│   ├── 01_init.sql
│   ├── 02_inspect.sql
│   ├── 03_verify.sql
│   └── README.md
└── docs/
    ├── PROJECT_STATUS.md
    ├── database-design.md
    ├── database-validation.md
    ├── auth-api.md
    ├── friends-api.md
    ├── messages-api.md
    ├── posts-api.md
    ├── unity-client-validation.md
    ├── unity-social-validation.md
    ├── unity-ui-validation.md
    └── unity-chat-validation.md
```

第八阶段使用过的临时工程副本 client/Build/Stage8Validation 已清理；再次运行构建脚本会重新创建。原工作区已有 README、Swagger 相关文件、Unity 工程与文档等未提交内容；本次保留原状，不执行 Git 提交。

## 4. 后端架构说明

后端采用简单分层，不引入微服务或额外仓储架构：

| 层或组件 | 职责 |
| --- | --- |
| Controllers | REST 路由、参数绑定和验证、读取 JWT 身份、调用服务、映射 HTTP 响应 |
| Services | 注册登录、好友关系、消息和动态的业务规则、权限及事务处理；TokenService 签发令牌 |
| DTOs | 限定请求字段和返回结构，不直接向客户端暴露数据库实体或密码哈希 |
| Models/Entities | users 等七张表对应实体 |
| Data | SocialDbContext 提供 DbSet，Configurations 映射表、约束、生成列和删除行为 |
| Middleware | ApiExceptionHandler 返回统一 ProblemDetails；数据库异常映射 503，其他未处理异常映射 500 |
| Program.cs | 注册依赖、EF、密码哈希、JWT、授权、异常处理和开发环境 Swagger |

业务请求通常经过 Controller → Service → SocialDbContext → MySQL；/api/auth/me 和数据库探针属于直接读取 DbContext 的现有例外，没有为此额外增加服务层。

密码由 ASP.NET Core IdentityV3 PasswordHasher 处理，使用 PBKDF2-HMAC-SHA512、随机盐和 210000 次迭代。登录返回 accessToken、tokenType、expiresAtUtc 和安全用户摘要，不返回 password_hash。受保护接口通过 JWT 的 sub 确定当前用户，调用方不能自行指定发送者或作者身份来取得权限。

好友写操作通过事务和有序用户行锁协调并发，配合数据库唯一约束避免重复申请或关系。发送消息检查好友关系；动态评论、点赞和删除等写操作配合事务及目标动态行锁处理并发。计数由查询计算，不额外保存冗余计数字段。

本地数据库连接和 JWT 密钥使用 User Secrets 或进程环境变量，不在源码或本文记录真实值。UtcConnectionInterceptor 为每次打开的 EF 连接设置 UTC 会话时区。启动流程不调用 EnsureCreated、EnsureDeleted 或 Migrate，仓库当前没有 EF 迁移目录。

Swagger UI 和 OpenAPI JSON 仅在 Development 提供，分别为 /swagger 和 /swagger/v1/swagger.json。数据库探针也仅在 Development 可用，只检查库名及七张表映射，不返回业务行内容。

详见 [后端说明](../backend/README.md)及各模块 API 文档；部分说明保留了早期阶段的范围描述，应结合本文了解当前总进度。

## 5. 数据库表结构说明

结构依据 [01_init.sql](../database/01_init.sql)，完整字段字典见 [数据库设计](database-design.md)。以下是源码中的结构说明，本次未连接或重新检查正在运行的数据库。

数据库为 social_system，使用 InnoDB、utf8mb4 和 utf8mb4_0900_ai_ci；用户名、密码哈希列按设计单独使用 ASCII 排序规则。业务时间使用 DATETIME(6)，通过 UTC 会话约定写入。除明确允许为空的字段和生成列外，下表中的业务列均为 NOT NULL。

| 表 | 字段摘要 | 主键、外键和用途 |
| --- | --- | --- |
| users | id BIGINT 自增；username VARCHAR(32)；password_hash VARCHAR(512)；nickname VARCHAR(32)；bio VARCHAR(200)，默认空串；avatar_key VARCHAR(64)，默认 default；created_at、updated_at | 主键 id；username 唯一且不区分大小写；updated_at 自动更新时间；不存明文密码 |
| friend_requests | id BIGINT 自增；sender_id、receiver_id BIGINT；status TINYINT；created_at；可空 handled_at；虚拟生成列 pending_low_id、pending_high_id | 主键 id；发送者、接收者均外键关联 users.id；保存申请及处理历史 |
| friends | id BIGINT 自增；user_low_id、user_high_id BIGINT；created_at | 主键 id；双方外键关联 users.id；low < high，一对好友只存一行 |
| messages | id BIGINT 自增；sender_id、receiver_id BIGINT；content VARCHAR(2000)；created_at | 主键 id；发送者、接收者关联 users.id；消息不依赖 friends 行，删除好友不删除历史 |
| posts | id BIGINT 自增；user_id BIGINT；content VARCHAR(2000)；created_at | 主键 id；作者关联 users.id |
| comments | id BIGINT 自增；post_id、user_id BIGINT；content VARCHAR(500)；created_at | 主键 id；关联 posts.id、users.id；一级评论，无父评论字段 |
| likes | post_id、user_id BIGINT；created_at | 联合主键 (post_id, user_id)，同时关联 posts.id、users.id；防止同一用户重复点赞 |

关键约束与索引：

| 表 | 约束与索引说明 |
| --- | --- |
| users | username UNIQUE；nickname 索引；CHECK 限制用户名格式及哈希、昵称、头像标识非空白 |
| friend_requests | CHECK 禁止自申请；状态为 0=Pending、1=Accepted、2=Rejected；状态与 handled_at 一致；待处理双方 ID 生成列构成 UNIQUE，阻止同向或反向重复待处理申请；索引 (receiver_id,status,id)、(sender_id,id) |
| friends | UNIQUE(user_low_id,user_high_id) 与 CHECK(low < high) 防止重复、反向及自好友；另有 user_high_id 索引 |
| messages | CHECK 禁止自发及空白内容；索引 (sender_id,receiver_id,id)、(receiver_id,sender_id,id) 支持双方会话查询 |
| posts | CHECK 内容非空白；索引 (user_id,id) |
| comments | CHECK 内容非空白；索引 (post_id,id)、user_id |
| likes | 联合主键保证每人每条动态最多一次点赞；另有 user_id 索引 |

七张表共 7 个主键、11 条外键、12 条 CHECK、3 条独立 UNIQUE，likes 的联合主键另外保证点赞唯一。只有 comments.post_id 和 likes.post_id 使用 ON DELETE CASCADE：删除动态同时清理其评论和点赞，其余外键不级联删除用户相关业务数据。

申请处理后，待处理生成列为 NULL，允许保留历史并重新申请。数据库已有 Rejected 状态，但当前没有拒绝申请 API。users 中已有 bio、avatar_key，也不代表已实现个人资料编辑或头像上传。

01_init.sql 用于首次创建结构，02_inspect.sql 用于检查，03_verify.sql 用于约束验证。它们不是后端启动时自动执行的脚本；本次没有执行任何 SQL。

## 6. Unity 客户端结构说明

工程为 client/SocialSystem.Unity，继续使用已保存并加入构建列表的 LoginScene。场景包含 Main Camera、ClientServices、Canvas、EventSystem 和既有登录控件。登录后的业务页面由脚本运行时创建，无需手工挂新脚本或创建新场景。

| 脚本或模块 | 当前职责 |
| --- | --- |
| ApiClient / ApiResponse | UnityWebRequest 协程；统一 GET/POST/DELETE、JSON、Bearer、默认 15 秒超时、204 空响应和错误提示；不自动重试写请求 |
| TokenManager | 仅在内存保存 JWT 与到期时间；到期、退出及销毁时清除，不用 PlayerPrefs |
| AuthManager / AuthDtos | Register、Login、GetCurrentUser、Logout、当前用户与认证 DTO |
| LoginSceneController | 输入、密码掩码、登录忙碌状态；登录后调用 /api/auth/me 核对身份，再打开主页 |
| SocialAppController | 主页及导航、共享会话、统一请求忙碌状态、退出和失效返回登录 |
| SocialUi / SocialTheme | 共用 UGUI 布局、深色主题、字体、按钮状态、输入框及间距；用户内容按纯文本显示 |
| LoginSceneView | 运行时优化现有登录控件的中文提示与布局，保留场景引用和 JWT 验证 |
| HomeView | 独立主页，展示昵称、用户名、用户 ID，提供好友、消息、动态入口 |
| FriendApi / FriendsView | 每页 10 人、昵称/用户名可点击聊天；独立申请页保留 ID 申请/接受；二次确认删除 |
| MessageApi / ChatView / ConversationStore | 好友自动传入身份；最近联系人、左右消息气泡、时间和长文本；通过原 API 刷新历史及发送；本地只保存联系人导航数据 |
| PostApi / PostsView | 动态 DTO/API 封装、每页 10 条、发布、评论提交及计数、点赞/取消、作者二次确认删除 |
| ClientProjectSetup / ClientSmokeRunner | 创建或打开 LoginScene、Windows 构建入口、启动 Play Mode 连接或社交测试 |
| ClientConnectionSmokeTest / ClientSocialSmokeTest | 仅 Editor 编译的连接测试与双账号社交流程测试 |

登录、主页及业务页面在同一场景内切换，共用原有 ApiClient、TokenManager、AuthManager，不额外创建跨场景持久化单例。退出或会话失效时销毁业务视图并清除认证状态，重新登录创建新视图。当前不支持切换到外部场景后保留会话，也不跨程序重启保存登录。

消息、动态输入最多 2000 个 .NET/UTF-16 字符单元，评论最多 500；检查空白内容和正整数 ID。操作期间禁用业务交互，结束后恢复；写请求超时不自动重试，用户应先刷新确认服务器结果。

默认 API 地址为 http://localhost:5080，可在 ClientServices 的 ApiClient 中修改。当前面向同机 Windows Editor / Windows Development Build，本地 HTTP 只允许 Editor 和开发版。最新打包位置为 client/SocialSystem.Unity/Build/SocialSystem.Client.exe，运行时须保留同目录依赖。

启动、构建与测试命令见 [客户端说明](../client/README.md)。构建脚本使用独立副本；测试脚本支持 connection 和 social 两种模式，使用临时 API、随机账号和隔离 MySQL，不连接已有 social_system。

## 7. API 接口列表

本机基础地址：http://localhost:5080。JSON 请求使用 Content-Type: application/json；需要认证时添加 Authorization: Bearer <accessToken>。

| 方法 | 路径 | JWT | 请求体或参数 | 功能 / 成功状态 |
| --- | --- | --- | --- | --- |
| GET | /api/health/database | 否 | 无；仅 Development | 库名及七表映射检查，200 |
| POST | /api/auth/register | 否 | username、password、nickname | 注册，201 |
| POST | /api/auth/login | 否 | username、password | 返回 JWT、到期时间及用户摘要，200 |
| GET | /api/auth/me | 是 | 无 | 本人摘要，200 |
| POST | /api/friends/request | 是 | receiverId | 发送好友申请，201 |
| POST | /api/friends/accept/{requestId} | 是 | 路径为申请 ID，无请求体 | 接收者接受申请，200 |
| GET | /api/friends | 是 | page、pageSize | 分页好友列表，200 |
| DELETE | /api/friends/{friendId} | 是 | 路径为对方用户 ID | 删除好友，204 |
| POST | /api/messages/send | 是 | receiverId、content | 给好友发送消息，201 |
| GET | /api/messages/{friendId} | 是 | 路径为对方用户 ID | 双向历史数组，200 |
| POST | /api/posts | 是 | content | 发布动态，201 |
| GET | /api/posts | 是 | page、pageSize | 分页动态列表，200 |
| DELETE | /api/posts/{id} | 是 | 路径为动态 ID | 删除本人动态，204 |
| POST | /api/posts/{id}/comments | 是 | content | 发表评论，201 |
| POST | /api/posts/{id}/like | 是 | 无请求体 | 点赞，204 |
| DELETE | /api/posts/{id}/like | 是 | 无请求体 | 取消点赞，204 |

注册用户名为 3–32 位英文字母、数字或下划线，密码 8–128 字符，昵称 1–32 字符；不接受邮箱作为登录标识。消息和动态正文最多 2000 字符，评论最多 500 字符，均不允许全空白。

好友和动态分页默认 page=1、pageSize=20，允许 page 为 1–1000000、pageSize 为 1–100；Unity 当前显式请求每页 10 条。消息历史不分页，按时间及 ID 升序；动态按时间及 ID 倒序。friendId 是 users.id，不是好友关系行 ID 或申请 ID。

重复点赞、取消点赞不会产生重复记录；删除动态仅作者可执行，重复删除已不存在的动态返回 404。删除好友后仍可读本人参与的历史，不能继续给非好友发送消息。

| 状态码 | 常见含义 |
| --- | --- |
| 400 | 参数、ID、正文或分页不合法 |
| 401 | 登录失败、身份缺失/无效/过期，或当前用户不存在 |
| 403 | 接受他人申请、非好友发消息、删除他人动态等越权操作 |
| 404 | 用户、申请、动态不存在，或访问未开放的接口 |
| 409 | 用户名重复、已有好友/待处理申请、申请已处理等冲突 |
| 503 / 500 | 数据库暂不可用 / 其他未处理服务端异常 |

业务错误采用 ProblemDetails，包含适用的 code；参数错误使用现有模型验证响应。完整契约见 [Auth](auth-api.md)、[Friends](friends-api.md)、[Messages](messages-api.md)、[Posts](posts-api.md)。

## 8. 已完成验收记录

以下按记录来源区分自动测试与用户人工验收。后端测试数为各阶段当时的**累计数**，不能相加为总数。

| 阶段 | 已有验证结果 | 证据及人工状态 |
| --- | --- | --- |
| 1 | MySQL 8.0.46 隔离实例执行 SQL 成功；7 表、11 外键、12 个已启用 CHECK；15 个 PASS 断言；测试行全部回滚 | [数据库验证](database-validation.md)；后续用户已验证本机初始化与表检查 |
| 2 | 本机 API 连接 social_system，读取七张表 EF 映射成功 | [后端说明](../backend/README.md)记录用户已验证 |
| 3 | restore/build 成功，0 警告、0 错误；18 项隔离 MySQL 认证集成测试通过 | [Auth 验证](auth-api.md)；不据自动结果推断本机人工用例全部通过 |
| 4 | build 成功，0 警告、0 错误；累计 25 项测试通过 | [好友验证](friends-api.md)，包括申请、权限、删除、并发与重复关系 |
| 5 | build 成功，0 警告、0 错误；累计 31 项测试通过 | [消息验证](messages-api.md)，包括好友限制、双方历史、隔离、排序与删除好友后的行为 |
| 6 | build 成功，0 警告、0 错误；累计 41 项测试通过 | [动态验证](posts-api.md)，包括正文、作者权限、计数、并发点赞、分页及级联删除 |
| 7 | Unity 编译、Windows 开发版、真实 Play Mode → API → 隔离 MySQL 连接验证通过 | [基础客户端记录](unity-client-validation.md)；既有项目状态记录用户已明确验收通过 |
| 8 | home、friends、chat、posts 四次分模块构建及 final 最终构建通过，未发现 C# 编译错误；双账号社交测试、原连接回归通过 | [社交客户端验证](unity-social-validation.md)；人工视觉、窗口适配及交互验收仍待完成 |

第八阶段测试覆盖好友申请冲突和接收权限、双向聊天及排序、删除好友后读历史/拒绝发送、动态评论及点赞计数、账号间点赞状态、作者删除、登出/重新登录、断网恢复、401、令牌本地到期和错误密码。长昵称布局调整后，最终构建与社交测试再次通过。

本机日志保存在 client/Build/ValidationLogs：home.log、friends.log、chat.log、posts.log、final.log、social-test.txt。这些文件和 Windows 构建产物被 Git 忽略，不保证在新克隆环境中存在。临时测试数据库及验证工程副本已清理，测试账号不能用于正式开发库。

第九阶段两轮分别执行了 Windows 构建、原连接回归和双账号社交回归；第二轮还验证好友条目点击、消息气泡、2000 字符消息、最新消息定位、删除好友后重新登录读历史及远端删除的 403。第一轮新增验证主页用户字段、账号切换和三个入口，检查 960×540 与 1280×720 渲染。本批及第八阶段均未重跑后端完整 41 项套件；后端、数据库定义和基础认证/网络源码通过哈希核对保持不变。

## 9. 当前存在的问题

以下是已知功能缺口、运行边界和待验收项，不将尚未完成的人工验收写成通过。

| 问题或边界 | 当前影响 |
| --- | --- |
| 无 Unity 注册界面 | 新账号需要通过 Swagger 等现有 API 入口创建；AuthManager.Register 已有 |
| 无用户搜索、收到申请列表、拒绝申请接口 | 好友操作需人工获取用户 ID、传递申请 ID；不能在客户端自动发现待处理申请 |
| 无历史评论查询接口 | 只能显示评论数量及本次会话最近提交的评论，不能浏览完整讨论 |
| 无个人资料编辑接口 | 主页仅显示身份摘要；bio、avatar_key 虽有字段，尚无完整编辑或上传功能 |
| 消息无实时推送、已读或历史分页 | 依赖手动刷新，大会话一次加载；大量数据下体验与性能尚需后续验证 |
| 单场景、内存认证会话 | 不支持外部场景切换后保留会话或跨应用重启免登录；联系人导航元数据另存本地，JWT 不落盘 |
| 无服务端完整会话目录接口 | 最近会话仅覆盖本机记录的联系人，按服务器和账号隔离；不能自动发现旧版本或其他设备未在本机记录、且已删除的联系人 |
| 动态为全体登录用户广场 | 不按好友关系过滤；是否改为仅好友可见尚未决定 |
| 第八阶段人工验收待完成 | 中文字体、长文本滚动、长昵称、窗口尺寸和双客户端交互仍需本机检查；大量记录下的客户端分页也需验收 |
| 写请求超时存在结果不确定性 | 不自动重试，需刷新确认后再决定是否重新提交 |
| 旧阶段文档保留历史描述 | 例如第七阶段文档中的“没有好友 UI”等是当时状态，不能作为第八阶段现状；本次仅更新本文 |
| 工作区仍有未提交内容 | 功能已存在于本机不代表已全部提交 Git；本次不整理或提交这些代码 |

目前没有将 UI 人工检查或大量数据性能测试记为已完成。后续发现的实际错误应记录触发步骤、预期与实际结果，再安排修复。

## 10. 后续优化计划

以下为后续建议。第九阶段两轮界面及好友聊天交互优化均已完成，下一步优先完成人工验收，再按需求补齐注册、搜索和会话目录等功能；数据库与后端接口约束保持不变。

| 优先顺序 | 计划 | 前提与完成标准 |
| --- | --- | --- |
| 1 | 完成第八阶段人工验收 | 按 [逐模块步骤](unity-social-validation.md)验证主页、好友、聊天、动态、断网及账号切换；记录界面和交互问题 |
| 2 | 完善现有客户端体验 | 根据实际验收结果调整文字、布局、滚动和错误反馈；以可复现问题为依据回归编译及相关流程 |
| 3 | 增加 Unity 注册界面 | 使用现有注册 API，提供昵称、用户名、密码及确认密码，验证注册→登录→主页闭环 |
| 4 | 补齐好友申请闭环 | 后续确定 API 扩展范围后增加用户搜索、收到申请列表及拒绝申请；再接入客户端，减少手填 ID |
| 5 | 补齐评论浏览及个人资料 | 分别设计历史评论查询、资料编辑接口和相应 UI；不因数据库已有字段就视为功能已完成 |
| 6 | 按实际数据量改善聊天 | 先明确刷新策略，再考虑简单轮询、历史分页或已读；不直接引入复杂实时架构 |
| 7 | 明确动态可见性 | 保持现有广场，或在需求明确后设计好友可见规则；更改前同步权限、接口与测试约定 |
| 8 | 整理课程演示与版本保存 | 准备双账号演示流程、环境配置、API/数据库说明及 Windows 构建；按后续指令整理并提交代码 |

涉及后端 API 或数据库的优化应在后续明确开发范围后实施，并遵循固定版本与现有数据约束。当前不升级主要技术版本，不扩展语音视频、复杂群聊、微服务或云部署。

项目规则见 [AGENTS.md](../AGENTS.md)，数据库操作见 [数据库说明](../database/README.md)，日常运行见 [客户端说明](../client/README.md)。
