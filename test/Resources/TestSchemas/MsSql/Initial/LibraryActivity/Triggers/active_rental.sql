CREATE TRIGGER library_activity.tr_active_rental_after_insert ON library_activity.active_rental
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO library_activity.rental_log
    (
        rental_id,
        book_id,
        patron_id,
        rental_date,
        due_date,
        return_date,
        fees_charged,
        fees_paid,
        notes,
        log_date
    )
    SELECT
        i.rental_id,
        i.book_id,
        i.patron_id,
        i.rental_date,
        i.due_date,
        i.return_date,
        i.fees_charged,
        i.fees_paid,
        i.notes,
        GETDATE()
    FROM inserted AS i;
END;

GO

CREATE TRIGGER library_activity.tr_active_rental_after_update ON library_activity.active_rental
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO library_activity.rental_log
    (
        rental_id,
        book_id,
        patron_id,
        rental_date,
        due_date,
        return_date,
        fees_charged,
        fees_paid,
        notes,
        log_date
    )
    SELECT
        i.rental_id,
        i.book_id,
        i.patron_id,
        i.rental_date,
        i.due_date,
        i.return_date,
        i.fees_charged,
        i.fees_paid,
        i.notes,
        GETDATE()
    FROM inserted AS i;

    DELETE FROM library_activity.active_rental
    WHERE rental_id IN (
        SELECT rental_id FROM inserted
        WHERE return_date IS NOT NULL
        AND fees_charged <= fees_paid
    );
END;

GO

CREATE TRIGGER library_activity.tr_active_rental_after_delete ON library_activity.active_rental
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM deleted WHERE fees_charged > fees_paid)
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Cannot delete active rental with outstanding fees.', 1;
    END
END;
