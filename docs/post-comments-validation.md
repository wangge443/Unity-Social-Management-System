# 动态评论与布局优化验证

日期：2026-09-23。仅补充动态评论查看和动态页面布局，不新增开发阶段。

## 修改范围

- backend/SocialSystem.Api/Controllers/PostsController.cs：新增 JWT 评论查询入口。
- backend/SocialSystem.Api/Services/PostService.cs：按动态查询评论及作者，分页和排序。
- backend/SocialSystem.Api/DTOs/Posts/PostDtos.cs：CommentListResponse。
- backend/SocialSystem.Api.Tests/PostsApiTests.cs：评论查询权限、分页、隔离、删除及参数测试。
- client/SocialSystem.Unity/Assets/Scripts/Posts/PostApi.cs：评论分页 DTO 与 HTTP 请求。
- client/SocialSystem.Unity/Assets/Scripts/Posts/PostsView.cs：紧凑布局、展开评论、刷新、重试和分页。
- client/SocialSystem.Unity/Assets/Scripts/Posts/PostCommentsScrollRect.cs 及 .meta：内层滚动到边界后将滚轮传给外层。
- client/SocialSystem.Unity/Assets/Tests/ClientSocialSmokeTest.cs：动态双账号和布局回归。
- client/SocialSystem.Unity/Assets/Tests/ClientUiCapture.cs：动态三种尺寸截图与布局断言。
- docs/API_REFERENCE.md、client/README.md、本文：接口和验收说明。

修改前备份：client/Build/Backups/post-comments-20260923-224325。保护清单 114 个文件的哈希核对一致，Auth、Friend、Message、注册、JWT、数据库结构和映射均未改动。

## 评论实现

新增 GET /api/posts/{id}/comments?page=1&pageSize=20，要求 JWT。page 范围 1–1000000，pageSize 范围 1–100。返回 items、page、pageSize、total；每条包含 id、postId、author（昵称、用户名等安全信息）、content、createdAt。

复用 comments 的 post_id、user_id、content、created_at 和现有索引，关联 users 获取作者。不新增表、列或迁移。只查询指定动态，按 createdAt 和 id 降序。无评论返回空列表，动态不存在或删除后返回 404，无 JWT 返回 401。列表及总数分别读取，并发写入可能短暂不一致。

Unity 每页 10 条，点击查看评论展开，再次点击收起；提交成功后自动展开并刷新最新评论及数量。昵称优先，同时显示用户名，时间转本地显示。无记录显示“暂无评论”，读取失败显示重试入口。继续使用既有 ApiClient 和令牌失效处理。

## 布局

保留深蓝主题和顶部导航。发布区域高度 52、分页 32、操作按钮 32、评论输入 44（均为 Canvas 参考单位）；正文自动换行。移除原说明行，把主要空间给外层动态 ScrollView。动态为独立卡片；评论区域固定 150 高，内部滚动和分页，滚轮到边界继续滚动外层动态列表。卡片超过视口时由 ScrollView 正常裁剪并可滚动查看。

## 自动测试结果

| 检查 | 结果 |
| --- | --- |
| dotnet build SocialSystem.sln --no-restore | 通过，0 警告、0 错误 |
| backend/scripts/Test-Auth.ps1 | 52/52 通过，含 Auth/Friend/Message/Post |
| Unity C# 编译 | 通过，无 C# 编译错误 |
| Windows Development Build | 最终 post-comments-final 构建通过 |
| Unity 双账号 HTTP/MySQL 社交回归 | 通过，使用隔离数据库及临时账号 |
| 评论流程 | A 发布→B 评论→数量更新→B 展开查看→A 重登录看到 B 评论；空状态、11 条评论两页、展开收起、失败重试通过 |
| 原动态功能 | 发布、评论、点赞/取消点赞、作者权限、删除通过 |
| 其他模块回归 | 登录、退出重登、401/过期、好友申请/拒绝、聊天/历史通过 |
| 布局与滚动 | 960×540、1280×720、2560×1440 布局断言通过；实际渲染截图已检查；内外滚动范围及边界滚轮交接通过 |

构建是 Windows 开发版；交互自动测试在 Unity Editor 内调用真实 HTTP API，不等同于人工操作 Windows exe 的验收。人工验收仍待执行。

日志：client/Build/ValidationLogs/PostComments 下 backend-build.txt、backend-tests.txt、social-final.txt、post-comments-final.log。截图位于 client/SocialSystem.Unity/Build/UI/posts-comments-*.png 与 posts-scrolled-*.png。构建同步到 client/SocialSystem.Unity/Build，运行 SocialSystem.Client.exe，必须保留同目录依赖。

本地后端已恢复，GET /api/auth/me 未携带 JWT 返回 401。没有重启系统服务。

## 人工验收

1. 启动后端，在 Unity 打开 Assets/Scenes/LoginScene.unity 并 Play，或运行 Windows 构建。使用 A 登录，打开动态并发布多行文字。
2. B 登录，打开动态，点击无评论动态的“查看评论”，确认“暂无评论”；再次点击收起。
3. B 输入评论并发表，确认数量增加、评论区自动展开，显示 B 的昵称、用户名、正文和时间。收起再展开仍能查到。
4. A 退出重登，打开同一动态查看评论，确认可见 B 的评论。
5. 添加超过 10 条评论，测试评论上一页/下一页、内部滚动、长正文换行；评论数应与记录一致。
6. 发布多条动态，滚动外层列表；展开多个评论区，再滚动评论，确认到边界后仍能继续浏览动态。收起不影响其他卡片。
7. 点赞、取消点赞、发表评论、发布动态、二次确认删除本人动态，确认仍正常；B 不应出现删除 A 动态的入口。
8. Game 视图分别设置 960×540、1280×720、2560×1440，检查导航、发布区、分页、卡片按钮和输入框；超出视口的卡片应能滚动到达。
9. 返回主页，检查登录退出、好友、聊天及注册原流程未受影响。
