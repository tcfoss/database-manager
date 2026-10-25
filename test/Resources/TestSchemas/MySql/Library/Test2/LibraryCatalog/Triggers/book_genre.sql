DELIMITER //

CREATE 
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_genre_after_insert
AFTER INSERT ON book_genre
FOR EACH ROW
BEGIN

    UPDATE book_search
    SET genres = (
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
TRIGGER tr_book_genre_after_update
AFTER UPDATE ON book_genre
FOR EACH ROW
BEGIN

    UPDATE book_search
    SET genres = (
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
TRIGGER tr_book_genre_after_delete
AFTER DELETE ON book_genre
FOR EACH ROW
BEGIN

    UPDATE book_search
    SET genres = (
        SELECT GROUP_CONCAT(g.full_name ORDER BY bg.display_order SEPARATOR ', ')
        FROM book_genre AS bg
        INNER JOIN genre AS g 
            ON bg.genre_id = g.genre_id
        WHERE bg.book_id = OLD.book_id
    )
    WHERE book_id = OLD.book_id;

END//

DELIMITER ;