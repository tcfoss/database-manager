DELIMITER //

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_author_after_insert
AFTER INSERT ON book_author
FOR EACH ROW
BEGIN

    UPDATE book_search
    SET authors = (
        SELECT GROUP_CONCAT(c.full_name ORDER BY ba.display_order SEPARATOR ', ')
        FROM book_author AS ba
        INNER JOIN contributor AS c 
            ON ba.contributor_id = c.contributor_id
        WHERE ba.book_id = NEW.book_id
    )
    WHERE book_id = NEW.book_id;

END//

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_author_after_update
AFTER UPDATE ON book_author
FOR EACH ROW
BEGIN

    UPDATE book_search
    SET authors = (
        SELECT GROUP_CONCAT(c.full_name ORDER BY ba.display_order SEPARATOR ', ')
        FROM book_author AS ba
        INNER JOIN contributor AS c 
            ON ba.contributor_id = c.contributor_id
        WHERE ba.book_id = NEW.book_id
    )
    WHERE book_id = NEW.book_id;

END//

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_book_author_after_delete
AFTER DELETE ON book_author
FOR EACH ROW
BEGIN

    UPDATE book_search
    SET authors = (
        SELECT GROUP_CONCAT(c.full_name ORDER BY ba.display_order SEPARATOR ', ')
        FROM book_author AS ba
        INNER JOIN contributor AS c 
            ON ba.contributor_id = c.contributor_id
        WHERE ba.book_id = OLD.book_id
    )
    WHERE book_id = OLD.book_id;

END//

DELIMITER ;