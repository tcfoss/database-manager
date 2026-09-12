CREATE TRIGGER library_catalog.tr_book_genre_after_insert ON library_catalog.book_genre
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET genres = (
        SELECT STRING_AGG(g.full_name, ', ') WITHIN GROUP (ORDER BY bg.display_order)
        FROM library_catalog.book_genre AS bg
        INNER JOIN library_catalog.genre AS g
            ON bg.genre_id = g.genre_id
        WHERE bg.book_id = bs.book_id
    )
    FROM library_catalog.book_search AS bs
    WHERE bs.book_id IN (SELECT DISTINCT book_id FROM inserted);
END;

GO

CREATE TRIGGER library_catalog.tr_book_genre_after_update ON library_catalog.book_genre
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET genres = (
        SELECT STRING_AGG(g.full_name, ', ') WITHIN GROUP (ORDER BY bg.display_order)
        FROM library_catalog.book_genre AS bg
        INNER JOIN library_catalog.genre AS g
            ON bg.genre_id = g.genre_id
        WHERE bg.book_id = bs.book_id
    )
    FROM library_catalog.book_search AS bs
    WHERE bs.book_id IN (SELECT DISTINCT book_id FROM inserted);
END;

GO

CREATE TRIGGER library_catalog.tr_book_genre_after_delete ON library_catalog.book_genre
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET genres = (
        SELECT STRING_AGG(g.full_name, ', ') WITHIN GROUP (ORDER BY bg.display_order)
        FROM library_catalog.book_genre AS bg
        INNER JOIN library_catalog.genre AS g
            ON bg.genre_id = g.genre_id
        WHERE bg.book_id = bs.book_id
    )
    FROM library_catalog.book_search AS bs
    WHERE bs.book_id IN (SELECT DISTINCT book_id FROM deleted);
END;
