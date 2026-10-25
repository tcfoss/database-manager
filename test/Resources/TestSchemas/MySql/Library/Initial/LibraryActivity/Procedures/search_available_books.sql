DELIMITER //

CREATE
    DEFINER = 'admin'@'localhost'
PROCEDURE search_available_books(IN search_term VARCHAR(255))
    SQL SECURITY DEFINER
BEGIN

    SELECT
        ab.book_id,
        ab.full_title,
        bs.title,
        bs.description,
        bs.authors,
        bs.genres
    FROM available_books AS ab
    INNER JOIN book_search AS bs 
        ON ab.book_id = bs.book_id
    WHERE MATCH(bs.title, bs.description, bs.authors, bs.genres) AGAINST (search_term IN NATURAL LANGUAGE MODE)
    OR ab.full_title LIKE CONCAT('%', search_term, '%')
    OR bs.description LIKE CONCAT('%', search_term, '%');

END//

DELIMITER ;
