# Unity 基础客户端验证记录

环境：Unity 6000.6.2f1 / Windows，UGUI 2.6.0，现有 ASP.NET Core net9.0 后端与 MySQL 8.0.46。

已在已有工程基础上继续验证，未重复创建 LoginScene。原始 LoginScene 已保存，并列入 EditorBuildSettings；场景引用由 Unity Editor 生成，不是手写 YAML。

## 已完成验证

- Unity Editor 成功导入、编译客户端代码并生成 Windows Development Build。
- 原有场景包含用户名 InputField、Password InputField、登录 Button、状态 Text、Canvas 与 EventSystem。
- 独立 MySQL 和既有后端进程启动后，Unity 真实进入 LoginScene 的 Play Mode。
- 使用 AuthManager.Register 创建隔离测试账号，调用登录按钮，成功请求 /api/auth/login。
- 收到 JWT 后，ApiClient 自动附加 Bearer 请求 /api/auth/me，并核对用户 ID。
- ApiClient 带认证 POST 创建测试动态，再用 DELETE 删除，收到 204，正确处理空响应。该操作只存在于验证代码，不是新增动态客户端功能。
- 清除令牌后请求 /api/auth/me 收到 401；错误密码无法登录。
- 测试结束后临时实例和数据清理。用户现有数据库与本机凭据未修改。
- 弃用的 FindFirstObjectByType 已改为 Unity 6.6 建议的 FindAnyObjectByType。

## 范围说明

端到端测试使用相同后端程序集和第一阶段 SQL 的隔离副本，随机端口、随机凭据。它证明 Unity 到 ASP.NET Core / MySQL 的调用链可用；用户自己的 localhost:5080 与实际账号仍按 client/README.md 手动运行验收。

JWT 只保存在当前客户端内存，不跨应用重启持久化。当前不提供注册 UI、好友 UI、消息 UI 或动态 UI。

项目开始时已有的后端 Swagger 改动保持原样，本阶段未修改任何后端文件。
