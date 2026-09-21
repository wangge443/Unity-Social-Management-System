# 数据库初始化与验证

文件顺序：

1. `01_init.sql`：首次创建 social_system 与 7 张表，无测试数据。
2. `02_inspect.sql`：只读查看表、字段、索引、外键和 CHECK。
3. `03_verify.sql`：插入事务内临时数据，自动断言约束、级联删除及消息保留，最后回滚。

详细字段和 E-R 图见 [数据库设计](../docs/database-design.md)。

## 在 MySQL Workbench 中执行

1. 打开已验证的 localhost:3306 连接。密码只在本机输入，不写进 SQL。
2. 先执行 `SELECT VERSION(), @@SESSION.sql_mode;`，确认 MySQL 8.0.16+（当前为 8.0.46），且 sql_mode 包含 STRICT_TRANS_TABLES 或 STRICT_ALL_TABLES。
3. 在 SCHEMAS 中刷新，检查 social_system。如果不存在或为空，可以继续；如果已有同名业务表，先检查来源，不要删表或直接重复运行初始化。
4. 使用 File → Open SQL Script 打开 `01_init.sql`。启用执行遇错停止；取消文本选区，执行整个脚本。Action Output 应无错误。脚本只设置当前连接时区，不修改服务器全局设置。
5. 刷新 SCHEMAS，展开 social_system → Tables，应有 users、friend_requests、friends、messages、posts、comments、likes 共 7 张表。
6. 打开并完整执行 `02_inspect.sql`。检查表引擎均为 InnoDB、表字符集排序规则为 utf8mb4_0900_ai_ci，外键 11 条、CHECK 12 条且 ENFORCED 为 YES。
7. 为验证脚本新开一个独立连接（避免提交你其他未提交的事务），打开 `03_verify.sql`，取消选区并执行整个文件，包括 DELIMITER、CREATE PROCEDURE、CALL、DROP PROCEDURE。使用已有 root 连接即可；普通账号需要 CREATE ROUTINE、ALTER ROUTINE、EXECUTE 以及相关表读写权限。
8. 预期收到 15 个 PASS 结果，最后为 `PASS: all checks passed; fixture data rolled back`。预期的唯一约束/CHECK/FK 错误已在过程中捕获，不应出现未捕获错误。

## 验证脚本的影响与失败处理

验证覆盖大小写用户名唯一、密码哈希非空、自加好友、双向重复待处理申请、处理时间一致性、历史申请与重发、重复/反向好友关系、孤立外键、空消息、中文与 emoji、删除好友保留消息、重复点赞、限制用户删除和动态级联删除。

测试用户名带随机后缀，不依赖固定业务 ID。所有测试行在同一事务中创建，成功或 SQL 异常均回滚；不会留下演示账号。自增编号可能产生空洞，这是正常现象。该脚本应在无并发业务的开发库运行。

如果 CALL 报错，先查看错误信息，测试事务由异常处理器回滚。遇错停止可能导致最后的 DROP PROCEDURE 未执行；修复前可在同一连接执行：

```sql
ROLLBACK;
DROP PROCEDURE IF EXISTS social_system.verify_social_schema_phase1;
```

然后重新完整执行验证文件。DDL 不受事务回滚保护，验证脚本必须在无其他待提交事务的独立连接执行。初始建表脚本如中途失败，不能假定整个数据库已经回滚，也不要通过忽略错误继续执行来当作初始化成功。

需要查看真实结构时可执行：

```sql
SHOW CREATE TABLE social_system.friend_requests;
SHOW CREATE TABLE social_system.friends;
SHOW CREATE TABLE social_system.likes;
```

本阶段暂不提交永久种子数据；后续真实登录演示账号应通过注册流程生成密码哈希。

## 后续 EF Core 衔接

本阶段使用初始化 SQL 验证设计。创建 EF Core 项目后，将字段、默认值、生成列、约束和索引映射到实体配置，并明确选择空库迁移或已存在库的基线方案。不得直接对已手工建表的库重复执行初始建表迁移；之后以迁移作为唯一结构变更来源。
