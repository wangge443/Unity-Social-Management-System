# 好友接口与验证

本阶段仅新增四个好友接口。沿用 users、friend_requests、friends，不修改数据库结构、AuthController、AuthService 或 JWT 实现。Program.cs 仅新增 FriendService 注册。

## 启动

在 backend 目录运行：

```powershell
dotnet build SocialSystem.sln
dotnet run --project SocialSystem.Api --launch-profile http --no-build
```

沿用已有 User Secrets。先停止占用输出文件或 5080 端口的旧 API 实例。本次开发为完成构建，已停止核实属于本项目的旧 API。

以下所有请求都需要有效的 JWT：

```text
Authorization: Bearer <accessToken>
```

基础地址 http://localhost:5080。JSON 请求设置 Content-Type: application/json。当前用户从 JWT 的 sub 取得，调用者不提供 senderId。

## POST /api/friends/request

请求体中的 receiverId 是目标用户在 users 表中的 ID：

```json
{"receiverId": 2}
```

成功返回 201：

```json
{"requestId": 10, "senderId": 1, "receiverId": 2, "status": "pending"}
```

不能向自己申请；目标必须存在。双方已有好友关系或任一方向已有待处理申请时返回 409。不自动接受反向申请。

## POST /api/friends/accept/{requestId}

例如 POST /api/friends/accept/10，无需请求体。只能由申请接收者调用。

成功返回 200：

```json
{"requestId": 10, "senderId": 1, "receiverId": 2, "status": "accepted"}
```

同一事务内更新 status、handled_at 并插入 friends。user_low_id 与 user_high_id 按用户 ID 升序保存；双方只有一行关系。

重复接受或接受已处理申请返回 409。发送者及第三方接受返回 403。不存在的申请返回 404。

## DELETE /api/friends/{friendId}

friendId 明确表示“好友的 users.id”，不是 friends.id 或 friend_requests.id。

例如用户 1 调用 DELETE /api/friends/2，解除自己与用户 2 的关系。成功返回 204，无响应体。

双方列表都会移除对方，保留申请历史和消息。目标用户存在但双方已无关系时重复调用仍返回 204；目标不存在返回 404，删除自己返回 400。接口始终限制当前用户参与的关系，不能删除两个其他用户之间的关系。

删除后可以重新申请，必须使用新申请的 requestId；旧的已接受申请不能重新建立关系。

## GET /api/friends

支持 page（默认 1，范围 1–1000000）、pageSize（默认 20，范围 1–100）。按关系 ID 降序排列。

GET /api/friends?page=1&pageSize=20：

```json
{
  "items": [
    {"friendId": 2, "username": "student02", "nickname": "同学乙", "avatarKey": "default", "createdAt": "<成为好友的 UTC 时间>"}
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

两端均可查到对方，不返回 password_hash。无好友时 items 为空数组、total 为 0。并发修改期间 total 和 items 是两次读取，可能存在短暂差异。

## 错误

| HTTP | 情形 / code |
| --- | --- |
| 400 | 无效 ID、请求体或分页参数；self_friendship |
| 401 | JWT 缺失/无效/过期，或当前用户已不存在 |
| 403 | not_request_receiver |
| 404 | user_not_found、request_not_found |
| 409 | already_friends、request_pending、request_handled |

业务错误采用 ProblemDetails，含 code；参数错误使用现有自动模型验证。数据库故障由已有异常处理器处理。

## 两账号手动测试

使用 Postman 或其他 HTTP 工具：

1. 用已有注册登录接口准备 A、B 两个不同账号。分别登录，保存 A/B 的 accessToken 和 user.id。不要把令牌或密码写入项目文件。
2. 用 A 的令牌 POST /api/friends/request，请求体 receiverId 为 B 的 user.id。记录响应 requestId，预期 201。
3. 再发同样申请，预期 409；用 B 向 A 反向申请也预期 409。
4. 用 A 的令牌接受该 requestId，预期 403；改用 B 的令牌接受，预期 200。重复接受预期 409。
5. 分别用 A/B 令牌 GET /api/friends，两边应各包含对方。
6. 用 A 的令牌 DELETE /api/friends/{B的用户ID}，预期 204；两边再次查列表均无该关系。
7. A 再次向 B 申请，预期新的 requestId；不带 Authorization 调用任一好友接口应返回 401。

本阶段未增加收到申请列表、拒绝申请或用户搜索接口。测试时从发送响应取得 requestId，从登录响应取得用户 ID。

## 自动验证

复用现有隔离 MySQL 测试入口（文件名仍为 Test-Auth.ps1，但运行解决方案全部测试）：

```powershell
dotnet restore SocialSystem.sln
dotnet build SocialSystem.sln --no-restore
./scripts/Test-Auth.ps1
```

使用临时 MySQL 实例和随机测试账号，不连接现有 social_system；结束后清理临时实例与数据。

本次结果：build 0 警告、0 错误；25 项测试全部通过（18 项原认证测试，7 项好友测试）。好友测试涵盖四个接口的认证要求、完整生命周期、接收者权限、双向列表、第三方删除隔离、输入验证、删除后重加、并发双向申请和并发接受。

并发控制采用同一事务内按用户 ID 顺序锁定两条用户记录，再检查当前关系和申请状态。所有新增好友写操作遵循同样锁顺序；数据库唯一约束作为最终保护。
