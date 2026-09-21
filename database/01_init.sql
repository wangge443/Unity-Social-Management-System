-- MySQL 8.0.16+ (CHECK constraints are enforced); verified target: 8.0.46.
-- Fresh schema only. No DROP, no credentials, no seed accounts.
-- Stop on the first error. Existing tables are intentionally NOT skipped.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;
SET SESSION time_zone = '+00:00';

CREATE DATABASE IF NOT EXISTS social_system
    CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
USE social_system;

CREATE TABLE users (
    id BIGINT NOT NULL AUTO_INCREMENT,
    username VARCHAR(32) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    password_hash VARCHAR(512) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    nickname VARCHAR(32) NOT NULL,
    bio VARCHAR(200) NOT NULL DEFAULT '',
    avatar_key VARCHAR(64) NOT NULL DEFAULT 'default',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    updated_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    CONSTRAINT uq_users_username UNIQUE (username),
    INDEX ix_users_nickname (nickname),
    CONSTRAINT ck_users_username CHECK (REGEXP_LIKE(username, '^[A-Za-z0-9_]{3,32}$', 'c')),
    CONSTRAINT ck_users_password_hash CHECK (CHAR_LENGTH(TRIM(password_hash)) > 0),
    CONSTRAINT ck_users_nickname CHECK (CHAR_LENGTH(TRIM(nickname)) > 0),
    CONSTRAINT ck_users_avatar_key CHECK (CHAR_LENGTH(TRIM(avatar_key)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE friend_requests (
    id BIGINT NOT NULL AUTO_INCREMENT,
    sender_id BIGINT NOT NULL,
    receiver_id BIGINT NOT NULL,
    status TINYINT NOT NULL DEFAULT 0 COMMENT '0=Pending, 1=Accepted, 2=Rejected',
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    handled_at DATETIME(6) NULL DEFAULT NULL,
    pending_low_id BIGINT GENERATED ALWAYS AS
        (CASE WHEN status = 0 THEN LEAST(sender_id, receiver_id) ELSE NULL END) VIRTUAL,
    pending_high_id BIGINT GENERATED ALWAYS AS
        (CASE WHEN status = 0 THEN GREATEST(sender_id, receiver_id) ELSE NULL END) VIRTUAL,
    PRIMARY KEY (id),
    CONSTRAINT uq_friend_requests_pending_pair UNIQUE (pending_low_id, pending_high_id),
    INDEX ix_friend_requests_receiver_status_id (receiver_id, status, id),
    INDEX ix_friend_requests_sender_id (sender_id, id),
    CONSTRAINT fk_friend_requests_sender FOREIGN KEY (sender_id) REFERENCES users (id),
    CONSTRAINT fk_friend_requests_receiver FOREIGN KEY (receiver_id) REFERENCES users (id),
    CONSTRAINT ck_friend_requests_different_users CHECK (sender_id <> receiver_id),
    CONSTRAINT ck_friend_requests_status CHECK (status IN (0, 1, 2)),
    CONSTRAINT ck_friend_requests_handled_at CHECK (
        (status = 0 AND handled_at IS NULL) OR
        (status IN (1, 2) AND handled_at IS NOT NULL AND handled_at >= created_at)
    )
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE friends (
    id BIGINT NOT NULL AUTO_INCREMENT,
    user_low_id BIGINT NOT NULL,
    user_high_id BIGINT NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    CONSTRAINT uq_friends_pair UNIQUE (user_low_id, user_high_id),
    INDEX ix_friends_high_id (user_high_id),
    CONSTRAINT fk_friends_low_user FOREIGN KEY (user_low_id) REFERENCES users (id),
    CONSTRAINT fk_friends_high_user FOREIGN KEY (user_high_id) REFERENCES users (id),
    CONSTRAINT ck_friends_order CHECK (user_low_id < user_high_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE messages (
    id BIGINT NOT NULL AUTO_INCREMENT,
    sender_id BIGINT NOT NULL,
    receiver_id BIGINT NOT NULL,
    content VARCHAR(2000) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    INDEX ix_messages_sender_receiver_id (sender_id, receiver_id, id),
    INDEX ix_messages_receiver_sender_id (receiver_id, sender_id, id),
    CONSTRAINT fk_messages_sender FOREIGN KEY (sender_id) REFERENCES users (id),
    CONSTRAINT fk_messages_receiver FOREIGN KEY (receiver_id) REFERENCES users (id),
    CONSTRAINT ck_messages_different_users CHECK (sender_id <> receiver_id),
    CONSTRAINT ck_messages_content CHECK (CHAR_LENGTH(TRIM(content)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE posts (
    id BIGINT NOT NULL AUTO_INCREMENT,
    user_id BIGINT NOT NULL,
    content VARCHAR(2000) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    INDEX ix_posts_user_id (user_id, id),
    CONSTRAINT fk_posts_user FOREIGN KEY (user_id) REFERENCES users (id),
    CONSTRAINT ck_posts_content CHECK (CHAR_LENGTH(TRIM(content)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE comments (
    id BIGINT NOT NULL AUTO_INCREMENT,
    post_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    content VARCHAR(500) NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (id),
    INDEX ix_comments_post_id (post_id, id),
    INDEX ix_comments_user_id (user_id),
    CONSTRAINT fk_comments_post FOREIGN KEY (post_id) REFERENCES posts (id) ON DELETE CASCADE,
    CONSTRAINT fk_comments_user FOREIGN KEY (user_id) REFERENCES users (id),
    CONSTRAINT ck_comments_content CHECK (CHAR_LENGTH(TRIM(content)) > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;

CREATE TABLE likes (
    post_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    created_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
    PRIMARY KEY (post_id, user_id),
    INDEX ix_likes_user_id (user_id),
    CONSTRAINT fk_likes_post FOREIGN KEY (post_id) REFERENCES posts (id) ON DELETE CASCADE,
    CONSTRAINT fk_likes_user FOREIGN KEY (user_id) REFERENCES users (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
