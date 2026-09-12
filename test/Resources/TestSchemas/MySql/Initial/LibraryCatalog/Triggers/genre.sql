DELIMITER //

CREATE 
    DEFINER = 'admin'@'localhost'
TRIGGER tr_genre_before_update
BEFORE UPDATE ON genre
FOR EACH ROW
BEGIN

    DECLARE parent_name VARCHAR(25);
    IF NEW.parent_id IS NOT NULL THEN

        SELECT g.short_name
        INTO parent_name
        FROM genre AS g
        WHERE g.genre_id = NEW.parent_id;

        IF parent_name IS NULL THEN
            SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Parent genre does not exist.';
        END IF;

    END IF;

    IF NEW.parent_id IS NULL
    AND EXISTS (SELECT 1 FROM genre WHERE short_name = NEW.short_name AND genre_id <> NEW.genre_id) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'A genre with this short name already exists at the root level.';
    END IF;

    SET NEW.full_name = IF (parent_name IS NOT NULL, CONCAT(parent_name, ' / ', NEW.short_name), NEW.short_name);

END//

DELIMITER ;