# 第九阶段第二轮：好友与聊天交互

日期：2026-09-22。已完成好友列表展示与点击聊天、移除手填聊天 ID、明确聊天对象、消息列表优化。本轮不修改后端 API、数据库结构、登录或 JWT 逻辑。

## 用户可见变化

- 好友列表显示昵称、用户名；点击好友信息区域或“聊天”均自动选择该好友，无需记忆或输入聊天 ID。
- 列表操作按钮使用固定宽度，分页和长用户名不会挤压主要信息。
- 原申请发送及接受保留，在“好友申请”页面操作；删除仍需二次确认，切换删除目标会重置旧确认状态。
- 聊天标题显示所选好友昵称和用户名；没有选中联系人时禁用发送。
- 消息用左右气泡区分对方与本人，显示时间、支持多行和长正文、手动刷新；发送后定位到最新消息，阅读较早消息时刷新尽量保持滚动位置。
- “最近会话”提供已记录联系人的历史入口。本人删除好友后转为只读；对方删除好友后，发送由既有 API 拒绝，客户端保留历史并转为只读。

## 历史与本地数据边界

ConversationStore 用 PlayerPrefs 保存联系人的 ID、用户名、昵称及本地已移除标记，按服务器地址和登录用户 ID 分开；不保存消息正文、密码或 JWT。历史内容仍每次通过现有带 Bearer 的消息 API 获取，登录退出及令牌失效处理保持不变。

从好友列表打开过的联系人，以及在本客户端删除的好友，会保留入口；重新登录同一账号可继续打开这些已记录会话。重新添加好友后，从好友列表点击该好友会恢复发送状态，最终权限仍以服务器为准。

现有 API 没有完整会话列表接口，因此不能自动发现其他设备或旧版本从未在本机记录、且已被删除的联系人。此类历史数据未删除，但不会自动出现在本机最近会话中。更换服务器地址、设备或清理 PlayerPrefs，也会影响本地联系人入口；不能把最近会话当作服务端完整会话目录。

好友申请仍需对方用户 ID、接收申请仍需申请 ID，这是现有接口的限制；删除的是聊天页面手动输入 ID 的方式。

## 修改文件

| 文件 | 修改 |
| --- | --- |
| [FriendsView.cs](../client/SocialSystem.Unity/Assets/Scripts/Friends/FriendsView.cs) | 姓名条目可点击；自动传递 FriendDto；独立申请页面；固定宽度、分页、删除确认；删除后保留会话入口 |
| [ChatView.cs](../client/SocialSystem.Unity/Assets/Scripts/Messages/ChatView.cs) | 移除 PeerInput/OpenInput；增加 OpenFriend、最近联系人、完整对象标题、气泡及滚动定位、只读历史状态 |
| [ConversationStore.cs](../client/SocialSystem.Unity/Assets/Scripts/Messages/ConversationStore.cs) | 新增按账号和服务器隔离的联系人导航数据，另含 Unity 生成的 .meta |
| [SocialAppController.cs](../client/SocialSystem.Unity/Assets/Scripts/Main/SocialAppController.cs) | 好友点击回调改接 Chat.OpenFriend；认证及退出逻辑保持 |
| [ClientSocialSmokeTest.cs](../client/SocialSystem.Unity/Assets/Tests/ClientSocialSmokeTest.cs) | 实际点击好友信息/聊天按钮，验证身份、申请、双账号消息、布局宽度、气泡方向、长消息、跨登录历史与账号隔离 |
| [ClientUiCapture.cs](../client/SocialSystem.Unity/Assets/Tests/ClientUiCapture.cs) | 渲染不同尺寸时保持滚动位置，避免截图改变所查看消息位置 |
| [client/README.md](../client/README.md)、[PROJECT_STATUS.md](PROJECT_STATUS.md)、本文件 | 当前操作、限制、文件及验证记录 |

FriendApi、MessageApi、PostApi、ApiClient、AuthManager、TokenManager、LoginScene 及后端/数据库文件没有修改。此前工作区已有未提交修改保留，不执行 Git 提交。

## 备份

修改前文件及 SHA256 清单位于 [stage9-chat-20260922-170156](../client/Build/Backups/stage9-chat-20260922-170156)。截图工具在修改前补充备份。protected-files.json 保存本轮禁止改动文件的基线哈希。

备份、构建、截图与测试日志为本机忽略产物，原文件备份保留；临时验证工程在交付后清理。

## 自动验证结果

| 检查 | 结果 |
| --- | --- |
| Unity 6000.6.2f1 Windows Development Build | 初次、布局修正及最终 stage9-chat-release 构建均通过；未发现 C# 编译错误 |
| 原登录连接回归 | 注册、真实登录按钮、Bearer GET、POST/DELETE 204、退出和错误密码通过 |
| 双账号好友与消息 | 自申请校验、重复申请、接收权限、真实好友信息点击、身份自动传递、双方消息与排序通过 |
| 消息 UI | 可读宽度检查、左右气泡、2000 字符多行消息、发送后滚动到底部通过 |
| 历史与权限 | 删除好友后记录入口、重新登录后读取历史、本地只读、对方删除后的服务端 403 与历史保留通过 |
| 账号与认证回归 | 最近联系人按账号隔离；断网恢复、401、令牌到期、重新登录通过 |
| 其他已有业务 | 主页三个入口、动态发布/评论/点赞/取消/删除回归通过 |
| 截图检查 | 实际 UGUI 好友、聊天、长消息、已删除好友历史；检查 960×540 及 1280×720 输出中的相关页面 |

初次截图发现自动拉伸导致按钮及侧栏过宽，已局部修正布局；消息列表布局完成后再定位最新消息。修改后重新执行最终 Windows 构建和两套回归。

测试使用临时 API、隔离 MySQL 和随机账号，不访问已有 social_system；完成后清理临时实例及测试账号对应的本地联系人缓存。未重新执行后端完整 41 项套件。自动及截图验证不等同于用户本机全部人工验收。

日志与摘要：client/Build/ValidationLogs/Stage9Chat。最终客户端：client/SocialSystem.Unity/Build/SocialSystem.Client.exe。截图在同目录 UI 下，包含 friends、chat、chat-long、chat-history 的 960 和 1280 宽版本。截图中的随机用户名和富文本标记为测试数据，按纯文本显示。

## 人工验收步骤

1. 启动现有后端，进入原 LoginScene 并 Play，或运行最新 Windows 开发版，无需重新挂脚本。
2. 用 A、B 账号通过“好友申请”发送/接受申请，返回列表应看到对方昵称和用户名。
3. 点击好友信息区域进入聊天，核对顶部姓名及用户名；无需输入 ID。也可点击条目“聊天”按钮。
4. A 发中文多行消息，B 从自己的好友列表打开 A，点击“刷新消息”并回复；双方历史一致，自己的消息在右侧。
5. 使用滚轮查看较早消息；发送后应看到最新消息。空白输入不能发送，最多 2000 字符。
6. 通过“选择好友”返回列表；已有会话可从消息页“最近会话”打开。
7. A 删除 B，重新登录 A 后从最近会话打开 B，仍能读历史、不能发送；B 尝试继续发送应被拒绝但不丢历史。重新添加后从好友列表进入可继续聊天。
8. 退出并切换账号，主页及会话入口不能串用另一账号的数据；错误密码、断网和令牌失效行为保持。

## 复现命令

```powershell
./client/scripts/Build-Client.ps1 -Label stage9-chat
./client/scripts/Test-Connection.ps1 -Mode social -CaptureUi -UnityProject 'client/Build/Stage8Validation'
./client/scripts/Test-Connection.ps1 -Mode connection -UnityProject 'client/Build/Stage8Validation'
```

构建副本路径沿用既有脚本名称。省略 -CaptureUi 时采用原无图形测试方式；消息业务请求均复用现有 API 封装。
