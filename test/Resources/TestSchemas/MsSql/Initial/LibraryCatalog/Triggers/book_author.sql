CREATE TRIGGER library_catalog.tr_book_author_after_insert ON library_catalog.book_author
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET authors = (
        SELECT STRING_AGG(c.full_name, ', ') WITHIN GROUP (ORDER BY ba.display_order)
        FROM library_catalog.book_author AS ba
        INNER JOIN library_catalog.contributor AS c
            ON ba.contributor_id = c.contributor_id
        WHERE ba.book_id = bs.book_id
    )
    FROM library_catalog.book_search AS bs
    WHERE bs.book_id IN (SELECT DISTINCT book_id FROM inserted);
END;

GO

CREATE TRIGGER library_catalog.tr_book_author_after_update ON library_catalog.book_author
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET authors = (
        SELECT STRING_AGG(c.full_name, ', ') WITHIN GROUP (ORDER BY ba.display_order)
        FROM library_catalog.book_author AS ba
        INNER JOIN library_catalog.contributor AS c
            ON ba.contributor_id = c.contributor_id
        WHERE ba.book_id = bs.book_id
    )
    FROM library_catalog.book_search AS bs
    WHERE bs.book_id IN (SELECT DISTINCT book_id FROM inserted);
END;

GO

CREATE TRIGGER library_catalog.tr_book_author_after_delete ON library_catalog.book_author
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET authors = (
        SELECT STRING_AGG(c.full_name, ', ') WITHIN GROUP (ORDER BY ba.display_order)
        FROM library_catalog.book_author AS ba
        INNER JOIN library_catalog.contributor AS c
            ON ba.contributor_id = c.contributor_id
        WHERE ba.book_id = bs.book_id
    )
    FROM library_catalog.book_search AS bs
    WHERE bs.book_id IN (SELECT DISTINCT book_id FROM deleted);
END;
