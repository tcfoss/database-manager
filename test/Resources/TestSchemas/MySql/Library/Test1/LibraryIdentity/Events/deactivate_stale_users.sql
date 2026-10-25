DELIMITER //

CREATE
    DEFINER = 'admin'@'localhost'
EVENT deactivate_stale_users
    ON SCHEDULE EVERY 1 DAY
    STARTS '2024-01-01 00:00:00'
DO
BEGIN
    UPDATE site_user
    SET is_active = FALSE
    WHERE is_active = TRUE
      AND last_login < NOW() - INTERVAL 1 YEAR;
END//

DELIMITER ;