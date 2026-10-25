DELIMITER //

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_active_rental_after_insert
AFTER INSERT ON active_rental
FOR EACH ROW
BEGIN
    INSERT INTO rental_log
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
    VALUES
    (
        NEW.rental_id,
        NEW.book_id,
        NEW.patron_id,
        NEW.rental_date,
        NEW.due_date,
        NEW.return_date,
        NEW.fees_charged,
        NEW.fees_paid,
        NEW.notes,
        CURRENT_TIMESTAMP()
    );
END//

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_active_rental_after_update
AFTER UPDATE ON active_rental
FOR EACH ROW
BEGIN

    INSERT INTO rental_log
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
    VALUES
    (
        NEW.rental_id,
        NEW.book_id,
        NEW.patron_id,
        NEW.rental_date,
        NEW.due_date,
        NEW.return_date,
        NEW.fees_charged,
        NEW.fees_paid,
        NEW.notes,
        CURRENT_TIMESTAMP()
    );

    IF NEW.return_date IS NOT NULL
    AND NEW.fees_charged <= NEW.fees_paid THEN

        DELETE FROM active_rental WHERE rental_id = NEW.rental_id;

    END IF;

END//

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_active_rental_before_delete
BEFORE DELETE ON active_rental
FOR EACH ROW
BEGIN

    IF OLD.fees_charged > OLD.fees_paid THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Cannot delete active rental with outstanding fees.';
    END IF;

END//

DELIMITER ;