# Services

AuthService 负责注册、登录和密码哈希验证；TokenService 负责签发 JWT；JwtSettings 负责认证配置校验。FriendService 负责申请、接受、删除和分页列表，以及相关事务和权限检查。

MessageService 负责好友私聊发送和当前用户的双向历史查询，发送与好友删除采用相同的用户锁顺序。
