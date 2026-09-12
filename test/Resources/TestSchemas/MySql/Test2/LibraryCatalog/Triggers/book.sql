DELIMITER //

CREATE 
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_after_insert
AFTER INSERT ON book
FOR EACH ROW
BEGIN

    INSERT INTO book_search 
    (
        book_id, 
        title, 
        description, 
        authors, 
        genres
    )
    VALUES
    (
        NEW.book_id,
        NEW.full_title,
        NEW.description,
        NULL,
        NULL
    );

END //

CREATE 
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_after_update
AFTER UPDATE ON book
FOR EACH ROW
BEGIN
    UPDATE book_search
    SET
        title = NEW.full_title,
        description = NEW.description,
        authors = (
            SELECT GROUP_CONCAT(c.full_name ORDER BY ba.display_order SEPARATOR ', ')
            FROM book_author AS ba
            INNER JOIN contributor AS c 
                ON ba.contributor_id = c.contributor_id
            WHERE ba.book_id = NEW.book_id
        ),
        genres = (
            SELECT GROUP_CONCAT(g.full_name ORDER BY bg.display_order SEPARATOR ', ')
            FROM book_genre AS bg
            INNER JOIN genre AS g 
                ON bg.genre_id = g.genre_id
            WHERE bg.book_id = NEW.book_id
        )
    WHERE book_id = NEW.book_id;
END//

CREATE 
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_before_delete
BEFORE DELETE ON book
FOR EACH ROW
BEGIN
    DELETE FROM book_search WHERE book_id = OLD.book_id;
END//

CREATE 
    DEFINER = 'admin'@'localhost'
TRIGGER tr_genre_before_insert
BEFORE INSERT ON genre
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
    AND EXISTS (SELECT 1 FROM genre WHERE short_name = NEW.short_name) THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'A genre with this short name already exists at the root level.';
    END IF;
    
    SET NEW.full_name = IF (parent_name IS NOT NULL, CONCAT(parent_name, ' / ', NEW.short_name), NEW.short_name);

END//

DELIMITER ;
