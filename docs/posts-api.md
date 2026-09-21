# 第六阶段：动态系统

复用现有 Post、Comment、Like 实体和 EF 映射，使用 posts、comments、likes、users，不修改数据库结构。新增 PostsController、PostService 和 DTO；Program.cs 只增加 PostService 注册。不修改 Auth、Friend、Message 模块。

## 启动和认证

在 backend 目录执行：

```powershell
dotnet build SocialSystem.sln
dotnet run --project SocialSystem.Api --launch-profile http --no-build
```

沿用本机 User Secrets。基础地址 http://localhost:5080，以下六个接口均需 JWT：

```text
Authorization: Bearer <accessToken>
```

当前用户 ID 来自 JWT，不能通过 JSON 指定发布者或评论者。现阶段采用所有登录用户可见的动态广场，不按好友关系过滤。

## 发布动态：POST /api/posts

Content-Type: application/json：

```json
{"content":"hello social system"}
```

正文必填，不能全为空白，最多 2000 个 .NET 字符单元（对 emoji 的长度校验比 MySQL 字符计数更保守）。保存原文。

成功返回 201：

```json
{
  "id": 1,
  "author": {"id": 1, "username": "student01", "nickname": "同学甲", "avatarKey": "default"},
  "content": "hello social system",
  "createdAt": "2026-09-21T08:00:00Z",
  "commentCount": 0,
  "likeCount": 0,
  "isLikedByMe": false
}
```

时间由数据库生成，返回 UTC。author 仅包含公开资料，不返回 password_hash。

## 动态列表：GET /api/posts

支持 page（默认 1，范围 1–1000000）、pageSize（默认 20，范围 1–100）。

GET /api/posts?page=1&pageSize=20：

```json
{
  "items": [
    {
      "id": 1,
      "author": {"id": 1, "username": "student01", "nickname": "同学甲", "avatarKey": "default"},
      "content": "hello social system",
      "createdAt": "2026-09-21T08:00:00Z",
      "commentCount": 1,
      "likeCount": 1,
      "isLikedByMe": true
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1
}
```

按 created_at 降序，同时间按 id 降序。评论数和点赞数通过数据库聚合计算，不增加冗余计数字段；isLikedByMe 为当前用户是否点赞。列表投影包含作者和计数，不逐条加载实体导航属性。total 和 items 是两次读取，并发发布或删除时可能短暂不一致。

## 删除动态：DELETE /api/posts/{id}

无需请求体。仅作者可删除，成功返回 204，无响应体。非作者返回 403 not_post_author，不存在的动态返回 404 post_not_found。

数据库已有外键会同步级联删除该动态的评论和点赞。重复删除已不存在的动态返回 404。

## 评论：POST /api/posts/{id}/comments

```json
{"content":"good"}
```

正文必填，不能全为空白，最多 500 个 .NET 字符单元。成功返回 201：

```json
{
  "id": 1,
  "postId": 1,
  "author": {"id": 2, "username": "student02", "nickname": "同学乙", "avatarKey": "default"},
  "content": "good",
  "createdAt": "2026-09-21T08:01:00Z"
}
```

采用一级评论。本阶段按要求提供发表评论和评论数量，不新增评论列表或删除评论接口。

## 点赞：POST /api/posts/{id}/like

无需请求体。成功返回 204。当前用户重复或并发点赞仍返回 204，但最多保存一行，不重复计数。可以给自己的动态点赞。

## 取消点赞：DELETE /api/posts/{id}/like

无需请求体。成功返回 204。未点赞或重复取消也返回 204；只能取消自己的点赞。动态不存在时返回 404。

评论、点赞、取消点赞及删除均在事务内锁定目标动态，防止并发重复写入以及检查存在后被删除。likes 的联合主键仍作为数据库唯一性保证。

## 错误约定

| HTTP | 情形 |
| --- | --- |
| 400 | 缺失/空白/过长正文、非法 ID、非法分页或无效 JSON |
| 401 | JWT 缺失、无效、过期或当前用户已不存在 |
| 403 | 删除他人的动态，code=not_post_author |
| 404 | 动态不存在，code=post_not_found |
| 503 | 数据库异常，由已有统一异常处理器返回 |

## 两账号手动验收

使用 Postman 或类似 HTTP 工具，登录 A、B 两个账号并分别保存令牌。不要把密码或令牌写进项目文件。

1. A 发布动态，预期 201，保存返回的 id。
2. B 获取动态列表，确认能看到 A 的作者信息、正文、时间，两个计数均为 0。
3. B 评论 good，预期 201；再查列表，commentCount 为 1。
4. B 点赞两次，均返回 204；列表 likeCount 仍为 1，B 的 isLikedByMe 为 true，A 的为 false。
5. B 取消点赞两次，均返回 204；likeCount 为 0。
6. B 删除 A 的动态，预期 403；A 删除自己的动态，预期 204，列表不再包含该动态。
7. 向被删除的 id 评论、点赞或取消点赞，预期 404；不携带令牌访问任一接口，预期 401。
8. 测试空白动态、超过 2000 字符的动态、超过 500 字符的评论，预期 400。

## 自动测试

```powershell
dotnet build SocialSystem.sln
./scripts/Test-Auth.ps1
```

既有 Test-Auth.ps1 运行整个解决方案的集成测试。它启动独立临时 MySQL，使用第一阶段 SQL 的隔离副本，不操作用户现有 social_system。完成后关闭实例并删除临时文件。

本次结果：build 成功，0 警告、0 错误；41 项测试通过，包括原有 31 项和新增 10 项动态测试。新增测试覆盖所有接口认证、发布者身份、公开作者信息、正文边界、评论/点赞计数、重复及并发点赞、用户间点赞独立性、越权删除、级联删除、分页和排序。
