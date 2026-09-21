# 第三阶段：用户注册与登录

不修改数据库结构。users 只有 username 唯一登录字段，没有 email，因此本阶段不接受邮箱作为登录标识，也不新增邮箱字段。请求中应仅发送下文列出的字段。

## 本机启动

ConnectionStrings:SocialSystem 和 JWT 配置已保存在 SocialSystem.Api 的本机 User Secrets 中。JWT 包含 SigningKey、Issuer、Audience、ExpirationMinutes；密钥不放进源码或 Git，正常启动无需重复设置。

在 PowerShell 7 中进入 backend，执行：

```powershell
dotnet restore SocialSystem.sln
dotnet build SocialSystem.sln --no-restore
dotnet run --project SocialSystem.Api --launch-profile http --no-build
```

User Secrets 文件位于 `%APPDATA%\Microsoft\UserSecrets\SocialSystem.Api-local-development\secrets.json`，可在 Visual Studio 中右键 SocialSystem.Api → “管理用户机密”打开。Development 启动配置自动加载该文件，后续重启沿用同一密钥。不要把该文件复制到仓库。

如果之前的终端仍设置了 Jwt__SigningKey，环境变量会覆盖 User Secrets；在该终端执行 `Remove-Item Env:Jwt__SigningKey -ErrorAction SilentlyContinue`，再启动应用。迁移时新生成的密钥会使使用旧临时密钥签发的令牌失效，需要重新登录。

未设置密钥、非 Base64 或解码后少于 32 字节时，应用启动会明确失败，不会使用默认密钥。默认 issuer 为 SocialSystem.Api，audience 为 SocialSystem.Client，有效期 60 分钟。

若 dotnet 不在 PATH，用 `& 'C:\Program Files\dotnet\dotnet.exe'` 替代 dotnet。之前的 API 进程须先 Ctrl+C 停止，否则可能锁住编译输出或占用 5080 端口。

## API

基础地址：http://localhost:5080。所有请求体使用 Content-Type: application/json。

### POST /api/auth/register

```json
{
  "username": "student01",
  "password": "<仅在本机填写密码>",
  "nickname": "同学甲"
}
```

- username：3–32 位 ASCII 字母、数字、下划线；不接受前后空格，存储为小写；数据库大小写不敏感唯一约束也会检查并发请求。
- password：8–128 个字符，不能全为空白；不截断、不 Trim，输入原样用于哈希和验证。
- nickname：必填，最多 32 个 .NET 字符单元，拒绝纯空白；保存前 Trim。此长度校验对 emoji 比 MySQL 的字符计数更保守。
- 注册成功返回 201；不会自动登录。

```json
{"id":1,"username":"student01","nickname":"同学甲"}
```

用户名重复返回 409，ProblemDetails 的 code 为 username_taken。参数缺失、超长、非法格式或无效 JSON 返回 400 ValidationProblemDetails。

### POST /api/auth/login

```json
{
  "username": "student01",
  "password": "<与注册时一致的本机密码>"
}
```

成功返回 200：

```json
{
  "accessToken": "<JWT>",
  "tokenType": "Bearer",
  "expiresAtUtc": "<UTC ISO 8601 时间>",
  "user": {"id":1,"username":"student01","nickname":"同学甲"}
}
```

用户名不存在或密码错误统一返回 401，code 为 invalid_credentials，提示“用户名或密码错误”。参数验证规则与注册时的用户名、密码规则一致。

### GET /api/auth/me

用于验证认证结果，添加请求头：

```text
Authorization: Bearer <登录返回的 accessToken>
```

成功返回 200 和当前用户的 id、username、nickname。无令牌、令牌无效/过期或用户已不存在时返回 401。不实现资料修改。

数据库连接或写入故障返回 503 database_unavailable；未预期异常返回通用 500。响应不暴露连接字符串、内部堆栈或 password_hash。EF 未启用敏感数据日志。

## 密码和认证实现

密码通过 ASP.NET Core PasswordHasher<User> 的 Identity V3 格式保存：PBKDF2-HMAC-SHA512、210000 次迭代、每次哈希独立随机盐。数据库 password_hash 保存算法参数、盐和派生值，不能还原明文。登录时验证哈希，成功且参数过旧时更新哈希。

JWT 使用 HS256 签名，携带用户 ID（sub）、用户名、唯一令牌 ID（jti）及时间声明。Bearer 中间件校验签名、算法、issuer、audience 和有效期；允许时间偏差设为 0。仅登录成功后签发令牌，响应 DTO 不包含实体密码字段。

首版不实现刷新令牌或服务器注销名单。令牌到期后重新登录；客户端退出时删除本地令牌，但已签发令牌在有效期内仍有效。

参考：[微软 PasswordHasher 实现](https://source.dot.net/Microsoft.Extensions.Identity.Core/PasswordHasher.cs.html)、[JWT Bearer 验证](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-9.0)。

## 交互式验收

本阶段提供 PowerShell 测试方式，无需 Swagger 或手动复制令牌。

API 启动后，在另一个 PowerShell 7 窗口进入 backend：

```powershell
./scripts/Test-AuthManual.ps1
```

脚本要求输入新用户名、昵称和隐藏输入的密码，依次调用注册、登录、me。不会输出密码或 JWT，只显示用户资料和到期时间。该操作会在你的 social_system 中创建并保留一个真实测试用户；请使用新用户名，重复名称会收到 409。

也可在 Postman 中按上述 JSON 请求；同一用户名重复注册应为 409，错误密码应为 401，不带 Authorization 调用 me 应为 401。

## 自动测试

先 restore 和 build，再运行：

```powershell
./scripts/Test-Auth.ps1
```

脚本使用本机 MySQL 8.0 程序启动独立临时实例，随机端口、随机数据库测试密码、随机 JWT 密钥，载入第一阶段 SQL 的隔离副本 social_system_auth_tests，然后执行 dotnet test --no-build --no-restore。测试不读取本机 User Secrets，不连接现有 3306 数据库；结束关闭实例、删除临时文件。可用 -MySqlBin 指定 MySQL bin 目录，-Dotnet 指定 dotnet.exe。

测试套件通过真实 MySQL 和内存 HTTP 服务验证注册登录；直接执行 dotnet test 而不启动测试实例会提示使用此脚本，不会回退到真实库。

已验证：18 项全部通过，包含安全响应、哈希验证和随机盐、用户名大小写与并发重复、输入长度/空白/无效 JSON、错误密码/不存在用户、Bearer 认证、缺失/损坏/过期/错误签名/issuer/audience 令牌。并发重复测试可能输出数据库唯一键冲突日志，这是被捕获并转换为 409 的预期路径。

dotnet restore 成功；dotnet build 为 0 警告、0 错误。用户本机真实账号流程仍待本阶段人工验收。
