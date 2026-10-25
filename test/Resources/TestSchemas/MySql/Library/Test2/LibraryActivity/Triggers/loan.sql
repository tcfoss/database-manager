DELIMITER //

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_loan_after_insert
AFTER INSERT ON loan
FOR EACH ROW
BEGIN
    INSERT INTO rental_log
    (
        loan_id,
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
        NEW.loan_id,
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
TRIGGER tr_loan_after_update
AFTER UPDATE ON loan
FOR EACH ROW
BEGIN

    INSERT INTO rental_log
    (
        loan_id,
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
        NEW.loan_id,
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

        DELETE FROM loan WHERE loan_id = NEW.loan_id;

    END IF;

END//

CREATE
    DEFINER = 'admin'@'localhost'
TRIGGER tr_loan_before_delete
BEFORE DELETE ON loan
FOR EACH ROW
BEGIN

    IF OLD.fees_charged > OLD.fees_paid THEN
        SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Cannot delete active rental with outstanding fees.';
    END IF;

END//

DELIMITER ;