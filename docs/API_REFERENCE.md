# 后端 API 接口文档

生成日期：2026-09-22。依据当前 Controller 路由和认证特性自动扫描，并读取 DTO、Service、JWT 配置及异常处理代码补充参数、响应与权限说明。共 **5 个 Controller、16 个接口，其中 13 个要求 JWT**。

本文仅整理当前实现，不修改业务代码，不新增接口，也不代表重新执行了接口测试。示例中的账号、ID、正文及时间均为格式示例，不是实库数据；令牌使用占位符。

## 1. 通用约定

| 项目 | 约定 |
| --- | --- |
| 本地基址 | `http://localhost:5080`，来自 http 启动配置 |
| 请求正文 | JSON 对象，使用 `Content-Type: application/json` |
| JSON 字段 | camelCase |
| JWT 请求头 | `Authorization: Bearer <accessToken>` |
| ID | JSON 整数，对应 C# long；参数范围 1–9223372036854775807 |
| 分页 | page 默认 1，范围 1–1000000；pageSize 默认 20，范围 1–100 |
| 日期 | JSON 日期字符串；服务端按 UTC 处理时间 |
| 空成功响应 | 204 No Content，无 JSON 正文 |
| CancellationToken | ASP.NET Core 注入的请求取消信号，不是客户端参数 |

所有带 `{id}`、`{friendId}`、`{requestId}` 的实际路由具有 `:long` 约束；本文省略约束以便阅读。无法解析为 long 的路径段通常不匹配路由，返回 404；匹配 long 但小于 1 的值由参数校验返回 400。

字符串长度遵循 .NET UTF-16 代码单元计数。Required 字符串不允许缺失、null、空串或全空白。正文不会因为数据库支持 emoji 就绕过长度限制。

### JWT 与身份

注册、登录显式允许匿名；数据库检查没有 Authorize 标记且只在 Development 提供功能。其余接口需要有效 Bearer JWT，没有配置额外管理员角色或角色分级策略。

服务端使用 HS256，验证签名、算法、issuer、audience、有效期，时钟偏差为 0。当前用户 ID 从 JWT 的 sub 声明取得，不能通过正文指定发送者、作者或点赞者。登录令牌默认 60 分钟有效，实际以 expiresAtUtc 为准。

当前没有 refresh-token 或服务端 logout 接口；Unity 退出登录清除本地令牌，不会撤销已经签发的 JWT。

## 2. 接口总览

| 方法 | URL | 功能 | JWT | 成功结果 |
| --- | --- | --- | --- | --- |
| POST | /api/auth/register | 注册 | 否 | 201 UserResponse |
| POST | /api/auth/login | 登录 | 否 | 200 LoginResponse |
| GET | /api/auth/me | 当前身份 | 是 | 200 UserResponse |
| POST | /api/friends/request | 发送好友申请 | 是 | 201 FriendRequestResponse |
| POST | /api/friends/accept/{requestId} | 接受好友申请 | 是 | 200 FriendRequestResponse |
| GET | /api/friends | 好友分页列表 | 是 | 200 FriendListResponse |
| DELETE | /api/friends/{friendId} | 删除好友 | 是 | 204 |
| POST | /api/messages/send | 发送私聊 | 是 | 201 MessageResponse |
| GET | /api/messages/{friendId} | 查询双方历史 | 是 | 200 MessageResponse[] |
| POST | /api/posts | 发布动态 | 是 | 201 PostResponse |
| GET | /api/posts | 动态分页列表 | 是 | 200 PostListResponse |
| DELETE | /api/posts/{id} | 删除本人动态 | 是 | 204 |
| POST | /api/posts/{id}/comments | 提交评论 | 是 | 201 CommentResponse |
| POST | /api/posts/{id}/like | 点赞 | 是 | 204 |
| DELETE | /api/posts/{id}/like | 取消点赞 | 是 | 204 |
| GET | /api/health/database | 开发环境数据库检查 | 否 | 200 DatabaseCheckResponse |

## 3. AuthController

来源：[Controller](../backend/SocialSystem.Api/Controllers/AuthController.cs)、[认证 DTO](../backend/SocialSystem.Api/DTOs/Auth)、[AuthService](../backend/SocialSystem.Api/Services/AuthService.cs)。

### POST /api/auth/register

注册用户；**JWT：不需要**。

| 参数 | 位置 | 类型 | 必填 | 校验 |
| --- | --- | --- | --- | --- |
| username | JSON | string | 是 | 3–32 位 ASCII 字母、数字或下划线 |
| password | JSON | string | 是 | 8–128 个代码单元 |
| nickname | JSON | string | 是 | 非空白，最多 32 个代码单元 |

```json
{"username":"demo_user","password":"ExamplePassword123!","nickname":"演示用户"}
```

成功：**201**，返回 UserResponse：

```json
{"id":1,"username":"demo_user","nickname":"演示用户"}
```

用户名转为小写后保存，昵称去除首尾空白；密码使用哈希保存，不返回密码或哈希。注册不自动返回 JWT，需另行登录。

错误：400 参数验证失败；409 `username_taken`（包含大小写变体重复用户名）。

### POST /api/auth/login

登录并取得令牌；**JWT：不需要**。

| 参数 | 位置 | 类型 | 必填 | 校验 |
| --- | --- | --- | --- | --- |
| username | JSON | string | 是 | 与注册相同的用户名格式 |
| password | JSON | string | 是 | 8–128 个代码单元 |

```json
{"username":"demo_user","password":"ExamplePassword123!"}
```

成功：**200**，返回 LoginResponse：

```json
{
  "accessToken":"<accessToken>",
  "tokenType":"Bearer",
  "expiresAtUtc":"2026-09-22T11:00:00Z",
  "user":{"id":1,"username":"demo_user","nickname":"演示用户"}
}
```

错误：400 参数验证失败；401 `invalid_credentials`。用户名不存在与密码错误使用同一业务错误，不区分返回。

### GET /api/auth/me

读取当前认证身份；**JWT：需要**。无路径、查询或正文参数。

成功：**200**，返回 UserResponse，字段为 id、username、nickname，与注册响应相同。

错误：401（令牌无效、sub 无法解析或对应用户不存在）。此接口不是个人资料编辑接口，不返回 bio、avatarKey 或密码哈希。

## 4. FriendsController

来源：[Controller](../backend/SocialSystem.Api/Controllers/FriendsController.cs)、[DTO](../backend/SocialSystem.Api/DTOs/Friends/FriendDtos.cs)、[FriendService](../backend/SocialSystem.Api/Services/FriendService.cs)。本控制器全部接口要求 JWT。

### POST /api/friends/request

发送好友申请；**JWT：需要**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| receiverId | JSON | long | 是 | 接收者 users.id，正整数 |

```json
{"receiverId":2}
```

成功：**201**，返回 FriendRequestResponse：

```json
{"requestId":10,"senderId":1,"receiverId":2,"status":"pending"}
```

发送者取自 JWT。双方不能是同一个用户，也不能已有好友关系或任一方向的待处理申请。

错误：400 `self_friendship`；401 当前用户不存在；404 目标用户不存在；409 `already_friends` 或 `request_pending`；无效参数返回 400。

### POST /api/friends/accept/{requestId}

接受申请并建立双向好友关系；**JWT：需要，且当前用户必须是申请接收者**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| requestId | 路径 | long | 是 | 正整数，好友申请记录 ID |

不需要 JSON 正文。

成功：**200**，返回 FriendRequestResponse：

```json
{"requestId":10,"senderId":1,"receiverId":2,"status":"accepted"}
```

错误：400 ID 无效；403 `not_request_receiver`；404 `request_not_found` 或目标用户不存在；409 `request_handled` 或 `already_friends`。重复接受已处理申请不会再次成功建立关系。认证及当前用户检查也可能返回 401。

### GET /api/friends

返回当前用户的好友列表；**JWT：需要**。

| 参数 | 位置 | 类型 | 必填 | 默认值 / 范围 |
| --- | --- | --- | --- | --- |
| page | 查询 | int | 否 | 1；1–1000000 |
| pageSize | 查询 | int | 否 | 20；1–100 |

请求示例：`GET /api/friends?page=1&pageSize=10`。

成功：**200**，返回 FriendListResponse：

```json
{
  "items":[
    {"friendId":2,"username":"demo_friend","nickname":"好友","avatarKey":"default","createdAt":"2026-09-22T10:00:00Z"}
  ],
  "page":1,
  "pageSize":10,
  "total":1
}
```

friendId 是对方的用户 ID，不是 friends 表主键。createdAt 是本次好友关系建立时间。按好友关系记录 ID 降序；空列表返回 items=[]，total 为当前好友总数。越过末页时 items 为空，total 仍为总数。

错误：400 分页校验失败；401 无效身份或当前用户不存在。

### DELETE /api/friends/{friendId}

解除当前用户与指定用户的好友关系；**JWT：需要**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| friendId | 路径 | long | 是 | 对方 users.id，正整数 |

无正文。成功：**204，无响应正文**。

只删除双方关系，不删除申请历史或聊天消息。目标用户存在但关系已不存在时仍返回 204；不能借此删除其他两名用户的好友关系。

错误：400 ID 无效或 `self_friendship`；401 当前用户不存在；404 目标用户不存在。

## 5. MessagesController

来源：[Controller](../backend/SocialSystem.Api/Controllers/MessagesController.cs)、[DTO](../backend/SocialSystem.Api/DTOs/Messages/MessageDtos.cs)、[MessageService](../backend/SocialSystem.Api/Services/MessageService.cs)。全部接口要求 JWT。

### POST /api/messages/send

发送文字私聊；**JWT：需要，且发送时双方必须仍为好友**。

| 参数 | 位置 | 类型 | 必填 | 校验 |
| --- | --- | --- | --- | --- |
| receiverId | JSON | long | 是 | 对方用户 ID，正整数 |
| content | JSON | string | 是 | 非空白，最多 2000 |

```json
{"receiverId":2,"content":"你好！"}
```

成功：**201**，返回 MessageResponse：

```json
{"id":20,"senderId":1,"receiverId":2,"content":"你好！","createdAt":"2026-09-22T10:10:00Z"}
```

senderId 取自 JWT；仅发送申请但尚未接受，不算好友。正文按传入内容保存。

错误：400 参数错误或 `self_message`；401 当前用户不存在；403 `not_friends`；404 目标用户不存在。

### GET /api/messages/{friendId}

读取当前用户与指定用户的完整双向历史；**JWT：需要，不要求当前仍为好友**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| friendId | 路径 | long | 是 | 对方 users.id，正整数 |

无查询分页参数，无正文。

成功：**200**，直接返回 MessageResponse 数组：

```json
[
  {"id":20,"senderId":1,"receiverId":2,"content":"你好！","createdAt":"2026-09-22T10:10:00Z"},
  {"id":21,"senderId":2,"receiverId":1,"content":"你好，收到。","createdAt":"2026-09-22T10:11:00Z"}
]
```

按 createdAt 升序，同时间按 id 升序。无历史时返回 []。解除好友后仍能查询本人参与的历史；从未聊过的非好友也只返回空数组。查询条件始终包含当前用户，不能指定另一发送者查看第三方会话。

错误：400 无效 ID 或 `self_message`；401 当前用户不存在；404 目标用户不存在。

## 6. PostsController

来源：[Controller](../backend/SocialSystem.Api/Controllers/PostsController.cs)、[DTO](../backend/SocialSystem.Api/DTOs/Posts/PostDtos.cs)、[PostService](../backend/SocialSystem.Api/Services/PostService.cs)。全部接口要求 JWT。

### POST /api/posts

发布文字动态；**JWT：需要**。

| 参数 | 位置 | 类型 | 必填 | 校验 |
| --- | --- | --- | --- | --- |
| content | JSON | string | 是 | 非空白，最多 2000 |

```json
{"content":"今天完成了项目演示。"}
```

成功：**201**，返回 PostResponse：

```json
{
  "id":30,
  "author":{"id":1,"username":"demo_user","nickname":"演示用户","avatarKey":"default"},
  "content":"今天完成了项目演示。",
  "createdAt":"2026-09-22T10:20:00Z",
  "commentCount":0,
  "likeCount":0,
  "isLikedByMe":false
}
```

作者取自 JWT。新发布响应的评论数和点赞数为 0，isLikedByMe 为 false。

错误：400 内容校验失败；401 无效身份或当前用户不存在。

### GET /api/posts

读取所有登录用户可见的动态广场；**JWT：需要**。

| 参数 | 位置 | 类型 | 必填 | 默认值 / 范围 |
| --- | --- | --- | --- | --- |
| page | 查询 | int | 否 | 1；1–1000000 |
| pageSize | 查询 | int | 否 | 20；1–100 |

请求示例：`GET /api/posts?page=1&pageSize=10`。

成功：**200**，返回 PostListResponse：

```json
{
  "items":[
    {
      "id":30,
      "author":{"id":1,"username":"demo_user","nickname":"演示用户","avatarKey":"default"},
      "content":"今天完成了项目演示。",
      "createdAt":"2026-09-22T10:20:00Z",
      "commentCount":1,
      "likeCount":1,
      "isLikedByMe":true
    }
  ],
  "page":1,
  "pageSize":10,
  "total":1
}
```

按 createdAt 降序，同时间按 id 降序；不按好友关系过滤。commentCount、likeCount 从数据库计算，isLikedByMe 针对 JWT 当前用户。total 与 items 分别查询，并发写入时可能短暂不一致。空页 items=[]。

错误：400 分页校验失败；401 无效身份或当前用户不存在。

### DELETE /api/posts/{id}

删除动态；**JWT：需要，且当前用户必须是作者**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| id | 路径 | long | 是 | 动态 ID，正整数 |

无正文。成功：**204，无响应正文**。数据库级联删除该动态的评论和点赞。

错误：400 无效 ID；401 当前用户不存在；403 `not_post_author`；404 `post_not_found`。重复删除已不存在的动态返回 404。

### POST /api/posts/{id}/comments

提交评论；**JWT：需要，不要求与作者为好友**。

| 参数 | 位置 | 类型 | 必填 | 校验 |
| --- | --- | --- | --- | --- |
| id | 路径 | long | 是 | 动态 ID，正整数 |
| content | JSON | string | 是 | 非空白，最多 500 |

```json
{"content":"演示顺利！"}
```

成功：**201**，返回 CommentResponse：

```json
{
  "id":40,
  "postId":30,
  "author":{"id":2,"username":"demo_friend","nickname":"好友","avatarKey":"default"},
  "content":"演示顺利！",
  "createdAt":"2026-09-22T10:21:00Z"
}
```

author 是评论者，不是动态作者。当前没有历史评论列表接口。

错误：400 路径或内容验证失败；401 当前用户不存在；404 `post_not_found`。

### POST /api/posts/{id}/like

点赞动态；**JWT：需要，不要求与作者为好友**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| id | 路径 | long | 是 | 动态 ID，正整数 |

无正文。成功：**204，无响应正文**。重复点赞幂等，不插入重复记录；不直接返回新点赞数，可刷新动态列表获取。

错误：400 无效 ID；401 当前用户不存在；404 `post_not_found`。

### DELETE /api/posts/{id}/like

取消本人点赞；**JWT：需要**。

| 参数 | 位置 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- | --- |
| id | 路径 | long | 是 | 动态 ID，正整数 |

无正文。成功：**204，无响应正文**。动态存在但本人未点赞时也成功；只操作当前用户的点赞。

错误：400 无效 ID；401 当前用户不存在；404 `post_not_found`。动态不存在与“动态存在但未点赞”不同，前者返回 404。

## 7. HealthController

来源：[Controller](../backend/SocialSystem.Api/Controllers/HealthController.cs)、[DTO](../backend/SocialSystem.Api/DTOs/DatabaseCheckResponse.cs)。

### GET /api/health/database

只读检查数据库连接和 7 张表映射；**JWT：不需要，仅 Development 可用**。

无路径、查询或正文参数。成功：**200**：

```json
{"status":"ok","database":"social_system","tablesChecked":7}
```

先确认实际数据库名，再通过 EF 读取各表映射字段，每表最多一行，不返回行内容；空表也能通过。该检查不等于完整约束验证。

| 失败条件 | 状态码 | title |
| --- | --- | --- |
| 非 Development 环境 | 404 | 无自定义业务错误 code |
| 缺少连接配置 | 503 | Database configuration missing |
| 连接库名不符 | 503 | Unexpected database |
| 连接或映射检查异常 | 503 | Database check failed |

这些结果以应用能够启动并处理请求为前提。缺失或无效 JWT 启动配置可能先导致应用启动失败。

## 8. 响应 DTO 字段字典

以下与示例配合使用，明确字段类型。所有集合响应均无额外统一 data 包装层。

| DTO | JSON 字段及类型 |
| --- | --- |
| UserResponse | id: long；username: string；nickname: string |
| LoginResponse | accessToken: string；tokenType: string；expiresAtUtc: DateTime；user: UserResponse |
| FriendRequestResponse | requestId: long；senderId: long；receiverId: long；status: string（成功路径 pending / accepted） |
| FriendResponse | friendId: long；username: string；nickname: string；avatarKey: string；createdAt: DateTime |
| FriendListResponse | items: FriendResponse[]；page: int；pageSize: int；total: int |
| MessageResponse | id: long；senderId: long；receiverId: long；content: string；createdAt: DateTime |
| PostAuthorResponse | id: long；username: string；nickname: string；avatarKey: string |
| PostResponse | id: long；author: PostAuthorResponse；content: string；createdAt: DateTime；commentCount: int；likeCount: int；isLikedByMe: bool |
| PostListResponse | items: PostResponse[]；page: int；pageSize: int；total: int |
| CommentResponse | id: long；postId: long；author: PostAuthorResponse；content: string；createdAt: DateTime |
| DatabaseCheckResponse | status: string；database: string；tablesChecked: int |

avatarKey 是头像标识字符串，不是上传接口或图片二进制内容。DateTime 在 JSON 中序列化为日期字符串，数字 ID 不以字符串形式返回。

## 9. 错误响应

Controller 业务错误使用 ProblemDetails，并在顶层扩展 code。下例只展示核心字段，实际还可能含 type、traceId 等：

```json
{"title":"只有好友之间可以发送消息","status":403,"code":"not_friends"}
```

参数验证由 ApiController 自动处理，通常返回 400 ValidationProblemDetails，包含 errors 字段；不保证有业务 code。Bearer 中间件、Unauthorized 或路由 404 的响应也不保证包含同一结构，客户端应先判断 HTTP 状态码。

### 业务错误码全集

| code | 状态码 | 含义 |
| --- | --- | --- |
| username_taken | 409 | 用户名已存在 |
| invalid_credentials | 401 | 用户名或密码错误 |
| self_friendship | 400 | 好友操作对象为自己 |
| user_not_found | 401 / 404 | 当前用户不存在 / 目标用户不存在 |
| already_friends | 409 | 双方已经是好友 |
| request_pending | 409 | 任一方向已有待处理申请 |
| request_not_found | 404 | 好友申请不存在 |
| not_request_receiver | 403 | 当前用户不是接收者 |
| request_handled | 409 | 申请已处理 |
| self_message | 400 | 给自己发送消息或查询与自己的私聊 |
| not_friends | 403 | 非好友不能发送消息 |
| post_not_found | 404 | 动态不存在 |
| not_post_author | 403 | 只有作者能删除动态 |
| database_unavailable | 503 | MySqlException / DbUpdateException 被全局异常处理器捕获 |
| internal_error | 500 | 其他未处理异常被全局异常处理器捕获 |

多个条件同时不满足时，实际返回取决于代码检查顺序。健康检查自行返回的 ProblemDetails 不使用上述全局数据库错误 code。

## 10. 在线查看与范围

Development 环境已有 Swagger UI：`http://localhost:5080/swagger`；OpenAPI JSON：`http://localhost:5080/swagger/v1/swagger.json`。它们是中间件文档入口，不计入 16 个 Controller 接口。

Swagger 的 Authorize 中只粘贴 accessToken，由界面添加 Bearer 前缀。本文依据源代码生成，没有为收集示例而发起注册、发送消息等写请求。

当前 Controller 中**没有**用户搜索、资料编辑、收到申请列表、拒绝申请、完整会话列表、历史评论查询、单条动态详情、消息删除或刷新令牌接口，不应根据已有表字段推断这些 API 已存在。

扫描和核对来源：

- [Controllers](../backend/SocialSystem.Api/Controllers)：HttpGet、HttpPost、HttpDelete、Route、Authorize、AllowAnonymous。
- [DTOs](../backend/SocialSystem.Api/DTOs)：参数类型、Required、StringLength、Range 与响应字段。
- [Services](../backend/SocialSystem.Api/Services)：身份、资源权限、排序、分页和业务错误。
- [Program.cs](../backend/SocialSystem.Api/Program.cs)、[JwtSettings.cs](../backend/SocialSystem.Api/Services/JwtSettings.cs)：JWT 与中间件注册。
- [ApiExceptionHandler.cs](../backend/SocialSystem.Api/Middleware/ApiExceptionHandler.cs)：500 / 503 响应。
- [launchSettings.json](../backend/SocialSystem.Api/Properties/launchSettings.json)：本机地址与环境。
