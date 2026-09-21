# Unity 社交管理系统

课程项目，架构为 Unity → HTTP/JSON → ASP.NET Core → EF Core → MySQL。

已实现数据库、EF Core、注册登录、JWT、User Secrets、好友、私聊消息，以及动态发布/列表/删除、评论、点赞和取消点赞；41 项隔离 MySQL 集成测试通过。尚未实现 Unity 业务。

第三阶段启动、JWT 本机配置与接口测试见 [注册登录说明](docs/auth-api.md)。

好友系统接口格式及两账号测试步骤见 [好友接口说明](docs/friends-api.md)。

消息发送、历史查询与测试步骤见 [消息接口说明](docs/messages-api.md)。

## 目录

| 目录 | 用途 |
| --- | --- |
| `backend/` | 后续 ASP.NET Core Web API 与后端测试 |
| `client/` | 后续 Unity 工程 |
| `database/` | 初始化 SQL、验证 SQL 和执行说明 |
| `docs/` | E-R 图、数据字典与设计决策 |

先阅读 [数据库设计](docs/database-design.md)，再按 [数据库执行说明](database/README.md) 操作。

## 固定环境

- .NET SDK 9.0.318；目标框架 net9.0；ASP.NET Core 9。
- EF Core 9.x；Pomelo.EntityFrameworkCore.MySql 9.0.0。
- MySQL 8.0.x，本机已确认 8.0.46 可运行。
- Unity 6.6 + C# + UGUI，编辑器启动与许可证已由用户验证。
- MySQL Workbench 到 localhost:3306 的连接已由用户验证。

数据库密码与 JWT 密钥不得写入源码。后续使用本机 User Secrets 或环境变量。

本阶段 SQL 是数据库结构基线。进入 EF Core 阶段时，必须明确采用空库迁移或对已有库建立迁移基线，不能在手动建表后的库上直接重复运行建表迁移；此后结构变更统一由 EF Core Migrations 管理。

第六阶段动态接口与测试步骤见 [动态接口说明](docs/posts-api.md)。
