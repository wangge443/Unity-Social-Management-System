# 消息接口与测试

复用已有 Message 实体、messages 表与 EF 映射，新增 MessageService、MessagesController、消息 DTO；Program.cs 仅新增服务注册。未修改 Auth 和 Friend 模块、数据库结构或凭据。

## 启动

在 backend 目录运行：

```powershell
dotnet build SocialSystem.sln
dotnet run --project SocialSystem.Api --launch-profile http --no-build
```

沿用本机 User Secrets，基础地址为 http://localhost:5080。两个接口均需：

```text
Authorization: Bearer <登录取得的 accessToken>
```

## POST /api/messages/send

Content-Type: application/json：

```json
{"receiverId":2,"content":"hello"}
```

receiverId 是接收者的 users.id，必须是当前用户的好友。senderId 从 JWT 取得，不能由请求指定。content 必填，不能全为空白，最多 2000 个 .NET 字符单元，保存原文，不裁剪首尾空白。

成功返回 HTTP 201：

```json
{
  "id": 1,
  "senderId": 1,
  "receiverId": 2,
  "content": "hello",
  "createdAt": "2026-09-21T08:00:00Z"
}
```

时间由 MySQL 生成，响应标记为 UTC。非好友或申请尚未被接受时返回 403 not_friends；发送给自己返回 400 self_message；接收者不存在返回 404 user_not_found；无效 ID、空白或超长内容返回 400。无有效 JWT 返回 401。

发送事务按双方用户 ID 升序锁定用户行，与既有 FriendService 顺序一致，防止检查好友关系后被并发删除再写入消息。

## GET /api/messages/{friendId}

friendId 为对方的 users.id。返回当前登录用户与对方之间的双向消息，HTTP 200：

```json
[
  {"id":1,"senderId":1,"receiverId":2,"content":"hello","createdAt":"2026-09-21T08:00:00Z"},
  {"id":2,"senderId":2,"receiverId":1,"content":"hi","createdAt":"2026-09-21T08:01:00Z"}
]
```

严格按 created_at 升序，同一时间按 id 升序。没有历史消息时返回 []。本阶段返回整个会话，未做分页。

查询条件始终包含 JWT 中的当前用户 ID，不能通过 friendId 查看其他两人的会话。删除好友后仍可查询自己参与的历史消息，但禁止继续发送；从未聊过的非好友只返回空数组。目标用户不存在返回 404，查询自己或非法 ID 返回 400。

## 手动测试

1. 用已有注册登录接口准备 A、B、C 三个账号，记录各自 user.id 和 accessToken。
2. 先用 A 的令牌发送给 B，预期 403。通过好友申请、接受流程让 A/B 成为好友。
3. 用 A 的令牌 POST /api/messages/send，receiverId 填 B 的 ID，content 填 hello；预期 201。
4. 用 B 的令牌向 A 回复 hi；预期 201。
5. 用 A 的令牌 GET /api/messages/{B的ID}，以及 B 的令牌 GET /api/messages/{A的ID}，都应返回同一组双向消息，按时间升序。
6. 用 C 的令牌 GET /api/messages/{B的ID}，不应出现 A/B 的消息；C/B 无历史时返回 []。
7. 测试空白内容、超过 2000 字符、自发消息、无 Authorization，分别应返回 400、400、400、401。
8. A 删除 B 好友后再发送，预期 403；双方查询仍可看到原历史消息。

可使用 Postman：登录请求保存令牌，后续请求在 Authorization 中选择 Bearer Token。不要将真实密码和令牌写入项目文件。

## 自动测试结果

```powershell
dotnet build SocialSystem.sln
./scripts/Test-Auth.ps1
```

既有 Test-Auth.ps1 运行整个解决方案测试，不仅运行 Auth；它启动独立临时 MySQL 实例并在结束后清理，不使用本机现有 social_system 数据。

本次 build 成功，0 警告、0 错误。31 项集成测试全部通过：原有 25 项与新增 6 项消息测试。覆盖 JWT、好友限制、申请未接受、双向历史、会话隔离、时间排序及同时间 ID 排序、中文/emoji、2000 字符边界、删除好友后禁止发送但保留历史。
