CREATE TRIGGER library_catalog.tr_book_after_insert ON library_catalog.book
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO library_catalog.book_search
    (
        book_id,
        title,
        description,
        authors,
        genres
    )
    SELECT
        i.book_id,
        i.full_title,
        i.description,
        NULL,
        NULL
    FROM inserted AS i;
END;

GO

CREATE TRIGGER library_catalog.tr_book_after_update ON library_catalog.book
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE library_catalog.book_search
    SET
        title = i.full_title,
        description = i.description,
        authors = (
            SELECT STRING_AGG(c.full_name, ', ') WITHIN GROUP (ORDER BY ba.display_order)
            FROM library_catalog.book_author AS ba
            INNER JOIN library_catalog.contributor AS c
                ON ba.contributor_id = c.contributor_id
            WHERE ba.book_id = bs.book_id
        ),
        genres = (
            SELECT STRING_AGG(g.full_name, ', ') WITHIN GROUP (ORDER BY bg.display_order)
            FROM library_catalog.book_genre AS bg
            INNER JOIN library_catalog.genre AS g
                ON bg.genre_id = g.genre_id
            WHERE bg.book_id = bs.book_id
        )
    FROM library_catalog.book_search AS bs
    INNER JOIN inserted AS i ON bs.book_id = i.book_id;
END;

GO

CREATE TRIGGER library_catalog.tr_book_after_delete ON library_catalog.book
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM library_catalog.book_search
    WHERE book_id IN (SELECT book_id FROM deleted);
END;

GO

CREATE TRIGGER library_catalog.tr_genre_after_insert ON library_catalog.genre
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Validate: parent must exist if specified
    IF EXISTS (
        SELECT 1 FROM inserted AS i
        WHERE i.parent_id IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM library_catalog.genre AS g WHERE g.genre_id = i.parent_id)
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Parent genre does not exist.', 1;
    END

    -- Validate: no duplicate short_name at root level
    IF EXISTS (
        SELECT 1 FROM inserted AS i
        WHERE i.parent_id IS NULL
        AND EXISTS (
            SELECT 1 FROM library_catalog.genre AS g
            WHERE g.short_name = i.short_name AND g.genre_id <> i.genre_id
        )
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'A genre with this short name already exists at the root level.', 1;
    END

    -- Compute full_name from parent
    UPDATE library_catalog.genre
    SET full_name = IIF(p.short_name IS NOT NULL, p.short_name + ' / ' + i.short_name, i.short_name)
    FROM library_catalog.genre AS g
    INNER JOIN inserted AS i ON g.genre_id = i.genre_id
    LEFT JOIN library_catalog.genre AS p ON p.genre_id = i.parent_id;
END;
