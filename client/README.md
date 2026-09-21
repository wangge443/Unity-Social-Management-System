# Unity 客户端预留目录

Unity 6.6 Editor 启动与许可证已由用户验证。本阶段不创建 Unity 工程或业务脚本。

后续工程放在 `SocialSystem.Unity/`，业务资源集中于 `Assets/_Project/`，包括 Scenes、Scripts、Prefabs、Art、Settings。

Scripts 按 Core（统一网络、会话、导航）、Models、Services、UI 划分。UI 模块为 Login、Register、Main、Friends、Chat、Posts、Profile，使用 UGUI。

客户端通过 HTTP/JSON 调用后端，不直接连接 MySQL。
