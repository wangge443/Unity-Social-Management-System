-- Run the WHOLE file in an independent Workbench connection (no unsaved transaction).
-- Creates one short-lived test procedure. Test rows are always rolled back.
-- AUTO_INCREMENT gaps are normal. This is not login/demo account provisioning.
SET NAMES utf8mb4 COLLATE utf8mb4_0900_ai_ci;
SET SESSION time_zone = '+00:00';
USE social_system;

DELIMITER $$
CREATE PROCEDURE verify_social_schema_phase1()
BEGIN
    DECLARE user_a BIGINT;
    DECLARE user_b BIGINT;
    DECLARE request_id BIGINT;
    DECLARE post_id_value BIGINT;
    DECLARE suffix_value VARCHAR(20);
    DECLARE hash_value VARCHAR(512);
    DECLARE rejected BOOLEAN DEFAULT FALSE;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET suffix_value = LEFT(REPLACE(UUID(), '-', ''), 20);
    -- ASP.NET Identity V3 PBKDF2-SHA512 hash of a discarded random password.
    SET hash_value = 'AQAAAAIAAYagAAAAEMBp6Ln+uCMmGOwXkX3N7+Omj0YaanShmS3YVfkW+0oyg71c+++98C3sXqHRCLq55g==';
    START TRANSACTION;
    INSERT INTO users (username, password_hash, nickname)
    VALUES (CONCAT('qa_a_', suffix_value), hash_value, '验证用户甲');
    SET user_a = LAST_INSERT_ID();
    INSERT INTO users (username, password_hash, nickname)
    VALUES (CONCAT('qa_b_', suffix_value), hash_value, '验证用户乙');
    SET user_b = LAST_INSERT_ID();

    BEGIN
        DECLARE CONTINUE HANDLER FOR 1062 SET rejected = TRUE;
        INSERT INTO users (username, password_hash, nickname)
        VALUES (UPPER(CONCAT('qa_a_', suffix_value)), hash_value, '重复用户名');
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: case-insensitive unique username'; END IF;
    SELECT 'PASS: unique username (case insensitive)' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1048 SET rejected = TRUE;
        INSERT INTO users (username, password_hash, nickname)
        VALUES (CONCAT('qa_c_', suffix_value), NULL, '空密码哈希');
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: password_hash NOT NULL'; END IF;
    SELECT 'PASS: password_hash NOT NULL' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 3819 SET rejected = TRUE;
        INSERT INTO friend_requests (sender_id, receiver_id) VALUES (user_a, user_a);
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: self friend request'; END IF;
    SELECT 'PASS: self friend request rejected' AS result;

    INSERT INTO friend_requests (sender_id, receiver_id) VALUES (user_a, user_b);
    SET request_id = LAST_INSERT_ID();
    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1062 SET rejected = TRUE;
        INSERT INTO friend_requests (sender_id, receiver_id) VALUES (user_b, user_a);
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: reversed pending request'; END IF;
    SELECT 'PASS: reverse-direction duplicate pending request rejected' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 3819 SET rejected = TRUE;
        UPDATE friend_requests SET status = 1 WHERE id = request_id;
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: handled_at required'; END IF;
    SELECT 'PASS: handled status requires handled_at' AS result;

    UPDATE friend_requests SET status = 2, handled_at = UTC_TIMESTAMP(6) WHERE id = request_id;
    INSERT INTO friend_requests (sender_id, receiver_id) VALUES (user_b, user_a);
    SET request_id = LAST_INSERT_ID();
    UPDATE friend_requests SET status = 1, handled_at = UTC_TIMESTAMP(6) WHERE id = request_id;
    INSERT INTO friends (user_low_id, user_high_id) VALUES (user_a, user_b);
    SELECT 'PASS: request history retained and reapplication allowed' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1062 SET rejected = TRUE;
        INSERT INTO friends (user_low_id, user_high_id) VALUES (user_a, user_b);
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: duplicate friendship'; END IF;
    SELECT 'PASS: duplicate friendship rejected' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 3819 SET rejected = TRUE;
        INSERT INTO friends (user_low_id, user_high_id) VALUES (user_b, user_a);
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: unordered friendship'; END IF;
    SELECT 'PASS: reversed friendship rejected' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1452 SET rejected = TRUE;
        INSERT INTO posts (user_id, content) VALUES (0, '不存在的作者');
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: missing author foreign key'; END IF;
    SELECT 'PASS: missing author rejected by foreign key' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 3819 SET rejected = TRUE;
        INSERT INTO messages (sender_id, receiver_id, content) VALUES (user_a, user_b, '   ');
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: blank message'; END IF;
    SELECT 'PASS: blank message rejected' AS result;

    INSERT INTO messages (sender_id, receiver_id, content) VALUES (user_a, user_b, '你好，UTF-8 四字节字符 😀');
    IF NOT EXISTS (SELECT 1 FROM messages WHERE sender_id = user_a AND receiver_id = user_b
                   AND content = '你好，UTF-8 四字节字符 😀') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: utf8mb4 round trip';
    END IF;
    DELETE FROM friends WHERE user_low_id = user_a AND user_high_id = user_b;
    IF (SELECT COUNT(*) FROM messages WHERE sender_id = user_a AND receiver_id = user_b) <> 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: messages lost after unfriend';
    END IF;
    SELECT 'PASS: utf8mb4 text and message retention after unfriend' AS result;

    INSERT INTO posts (user_id, content) VALUES (user_a, '数据库结构验证动态 😀');
    SET post_id_value = LAST_INSERT_ID();
    INSERT INTO comments (post_id, user_id, content) VALUES (post_id_value, user_b, '验证评论');
    INSERT INTO likes (post_id, user_id) VALUES (post_id_value, user_b);
    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1062 SET rejected = TRUE;
        INSERT INTO likes (post_id, user_id) VALUES (post_id_value, user_b);
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: duplicate like'; END IF;
    SELECT 'PASS: duplicate like rejected' AS result;

    SET rejected = FALSE;
    BEGIN
        DECLARE CONTINUE HANDLER FOR 1451 SET rejected = TRUE;
        DELETE FROM users WHERE id = user_a;
    END;
    IF NOT rejected THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: referenced user deleted'; END IF;
    SELECT 'PASS: referenced user deletion restricted' AS result;

    DELETE FROM posts WHERE id = post_id_value;
    IF EXISTS (SELECT 1 FROM comments WHERE post_id = post_id_value)
       OR EXISTS (SELECT 1 FROM likes WHERE post_id = post_id_value) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: post cascade delete';
    END IF;
    SELECT 'PASS: post deletion cascades to comments and likes' AS result;

    ROLLBACK;
    IF EXISTS (SELECT 1 FROM users WHERE id IN (user_a, user_b)) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'FAIL: fixture rollback';
    END IF;
    SELECT 'PASS: all checks passed; fixture data rolled back' AS result;
END$$
DELIMITER ;

CALL verify_social_schema_phase1();
DROP PROCEDURE verify_social_schema_phase1;
