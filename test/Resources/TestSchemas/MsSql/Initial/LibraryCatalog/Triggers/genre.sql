CREATE TRIGGER library_catalog.tr_genre_after_update ON library_catalog.genre
AFTER UPDATE
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
            WHERE g.short_name = i.short_name
            AND g.genre_id <> i.genre_id
        )
    )
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'A genre with this short name already exists at the root level.', 1;
    END

    -- Recompute full_name from parent
    UPDATE g
    SET g.full_name = IIF(p.short_name IS NOT NULL, p.short_name + ' / ' + i.short_name, i.short_name)
    FROM library_catalog.genre AS g
    INNER JOIN inserted AS i
        ON g.genre_id = i.genre_id
    LEFT JOIN library_catalog.genre AS p
        ON p.genre_id = i.parent_id;
END;
