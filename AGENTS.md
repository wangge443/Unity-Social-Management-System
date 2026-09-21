\# 项目说明



这是一个基于 Unity 的社交管理系统课程项目。



\## 技术栈



\- 客户端：Unity 6 + C# + UGUI

\- 后端：ASP.NET Core Web API

\- ORM：Entity Framework Core

\- 数据库：MySQL 8.0

\- 开发环境：Windows



\## 系统架构



Unity Client

→ HTTP / JSON

→ ASP.NET Core Web API

→ Entity Framework Core

→ MySQL



Unity 不允许直接连接 MySQL，所有数据库操作必须经过后端 API。



\## 核心功能



\- 用户注册、登录、个人资料

\- 用户搜索

\- 好友申请、好友列表、删除好友

\- 私聊和历史消息

\- 动态发布与删除

\- 点赞与取消点赞

\- 评论



暂时不实现：



\- 语音/视频聊天

\- 文件传输

\- 复杂群聊

\- 微服务

\- 云服务器部署



\## 数据库



数据库名称：social\_system



主要数据表：



\- users

\- friend\_requests

\- friends

\- messages

\- posts

\- comments

\- likes



要求：



\- 正确设计主键和外键

\- 合理使用 UNIQUE、NOT NULL 和 INDEX

\- 使用 utf8mb4

\- 密码不得明文存储

\- 防止重复点赞

\- 避免明显数据冗余



\## 后端规范



使用清晰但不过度复杂的结构：



\- Controllers

\- Services

\- Models / Entities

\- DTOs

\- Data



要求：



\- REST API

\- 参数验证

\- 合理 HTTP 状态码

\- 异常处理

\- 密码哈希

\- 基本身份认证

\- 权限检查



不要为了课程项目引入不必要的复杂架构。



\## 后端固定技术版本



本项目统一使用以下后端技术栈：



\- Target Framework: .NET 9 (`net9.0`)

\- ASP.NET Core 9

\- Entity Framework Core 9.x

\- MySQL Provider: Pomelo.EntityFrameworkCore.MySql 9.0.0

\- MySQL Server 8.0.x



除非出现明确的兼容性问题，否则不要自行升级或更换上述主要技术版本。



所有 EF Core 相关 Microsoft.Extensions / Microsoft.EntityFrameworkCore

包必须保持兼容的大版本，不得混用 EF Core 8、9、10。



不要使用 EF Core 10。

不要将项目目标框架修改为 net10.0。



\## Unity 规范



主要模块：



\- Login

\- Register

\- Main

\- Friends

\- Chat

\- Posts

\- Profile



Unity 使用 HTTP 与后端通信。



建立统一网络请求模块，不要让每个 UI 脚本重复实现网络请求。



UI 与业务逻辑尽量分离。



\## 开发原则



项目优先级：



1\. 能正常运行

2\. 核心功能完整

3\. 数据库设计合理

4\. 项目结构清楚

5\. 代码容易理解

6\. 方便课程答辩和演示



不要过度设计。



不得通过删除功能、绕过错误等方式假装完成任务。



修改代码后，应尽可能执行编译或测试。



如果某一步必须由用户手动完成，例如：



\- Unity Editor 操作

\- 输入数据库密码

\- 图形界面配置



应明确告诉用户操作步骤，不要假设已经完成。



不要把数据库密码、密钥等敏感信息硬编码进源码或提交到 Git。

