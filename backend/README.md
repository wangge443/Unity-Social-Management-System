# 后端基础工程

消息系统已新增发送和历史查询接口，复用已有 Message 实体及映射，全部接口要求 JWT。请求格式和验证见 [消息接口说明](../docs/messages-api.md)。

好友系统已新增四个 JWT 保护接口，使用既有表且未修改 Auth 模块；接口和验证步骤见 [好友接口说明](../docs/friends-api.md)。

第二阶段的本机数据库连接已由用户实际验证通过。第三阶段注册与登录已实现，JWT 配置已持久保存到本项目 User Secrets，Development 启动时自动加载，无需每次设置密钥。启动及接口验收见 [注册登录说明](../docs/auth-api.md)。

目标框架 net9.0，SDK 9.0.318。打开 SocialSystem.sln 可使用 Visual Studio 开发。

## 目录和 NuGet 依赖

- Controllers/HealthController.cs：仅开发环境开放的只读数据库检查。
- Models/Entities/：7 个实体及 FriendRequestStatus 枚举。
- Data/SocialDbContext.cs：7 个 DbSet。
- Data/Configurations/：字段、类型、主外键、索引、CHECK、默认值、虚拟生成列和删除行为。
- Data/UtcConnectionInterceptor.cs：每次打开 EF 连接设置当前会话时区为 UTC。
- DTOs/：检查响应；Services/：业务服务预留。

直接依赖为 Microsoft.EntityFrameworkCore 9.0.20、Microsoft.EntityFrameworkCore.Relational 9.0.20、Pomelo.EntityFrameworkCore.MySql 9.0.0、Microsoft.AspNetCore.Authentication.JwtBearer 9.0.20。MySqlConnector 2.4.0 由 Pomelo 传递引入。ASP.NET Core 使用 SDK 共享框架。

本阶段没有 EF Design、dotnet-ef、迁移或数据库初始化调用。不得在现有库上执行 EnsureCreated、EnsureDeleted、Migrate。users.updated_at 依靠现有 ON UPDATE 自动更新，EF 将该属性标记为新增/更新生成且忽略写入。生成列由数据库计算。

## 在本机配置连接

在 Visual Studio 中右键 SocialSystem.Api 项目 → “管理用户机密”，把以下结构写入打开的 secrets.json；只在本机替换占位文字：

```json
{
  "ConnectionStrings": {
    "SocialSystem": "Server=localhost;Port=3306;Database=social_system;User ID=填写本机用户名;Password=填写本机密码;Character Set=utf8mb4;DateTimeKind=Utc;Connection Timeout=5;Default Command Timeout=10"
  }
}
```

实际文件位置：

```text
%APPDATA%\Microsoft\UserSecrets\SocialSystem.Api-local-development\secrets.json
```

也可在资源管理器地址栏输入该目录，创建目录与文件。User Secrets 是仓库外的本机开发配置，不是加密保险库。不要把密码或此文件发到聊天、复制到源码或 Git。

密码包含分号等连接字符串特殊字符时需要正确引用 Password 值，JSON 中的双引号必须转义。可改用进程环境变量 ConnectionStrings__SocialSystem 配置完整连接字符串；该环境变量会覆盖 User Secrets。

appsettings.json 不保存连接字符串。http 启动配置指定 Development，自动加载 User Secrets。

## 编译和运行

在 backend 目录执行：

```powershell
dotnet restore SocialSystem.sln
dotnet build SocialSystem.sln --no-restore
dotnet run --project SocialSystem.Api --launch-profile http --no-build
```

当前终端找不到 dotnet 时，可以用 & 'C:\Program Files\dotnet\dotnet.exe' 替代 dotnet，或重新打开终端。

访问 http://localhost:5080/api/health/database，成功时应为 HTTP 200：

```json
{"status":"ok","database":"social_system","tablesChecked":7}
```

接口执行 SELECT DATABASE()，确认实际数据库名称，然后通过 EF 查询全部 7 张表的映射字段（每表最多一行，不返回任何行数据）。空表也可以通过。只修改连接会话时区，不写数据、不改表；该检查不替代第一阶段的约束验证。

| 结果 | 含义 |
| --- | --- |
| 503 / Database configuration missing | 需要配置本机 User Secrets |
| 503 / Database check failed | 检查服务、凭据、权限与已有表结构 |
| 503 / Unexpected database | 实际连接库不是 social_system |
| 404 | 不是 Development 环境，接口不开放 |

按 Ctrl+C 停止。确认连接成功后再进入注册登录阶段。

## 当前验证状态

第二阶段本机 API 与 social_system 连接、7 张表映射读取均已由用户验证通过。第三阶段 restore、build 成功，0 警告、0 错误，18 项隔离 MySQL 集成测试通过。第一阶段 SQL 文件和现有数据库结构未修改。第三阶段本机人工验收步骤见注册登录说明。
