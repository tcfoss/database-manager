CREATE 
    DEFINER = 'admin'@'localhost'
PROCEDURE end_loan (
    IN p_rental_id INT,
    IN p_barcode VARCHAR(50),
    IN p_return_date TIMESTAMP,
    IN p_late_fee DECIMAL(10,2),
    IN p_fees_paid DECIMAL(10,2),
    IN p_notes TEXT,
    OUT p_active_rental_concluded BOOLEAN
)
    SQL SECURITY DEFINER
BEGIN

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    IF p_rental_id IS NULL 
    THEN

        SELECT ar.rental_id INTO p_rental_id
        FROM active_rental AS ar
        INNER JOIN library_catalog.item AS itm
            ON ar.item_id = itm.item_id
        WHERE itm.barcode = p_barcode
        AND   ar.really_active = 'Y';

        IF p_rental_id IS NULL 
        THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Active rental not found for given item.';
        END IF;

    END IF;

    IF p_late_fee IS NULL 
    THEN
        SET p_late_fee = 0.00;
    END IF;

    IF p_fees_paid IS NULL 
    THEN
        SET p_fees_paid = 0.00;
    END IF;

    IF p_late_fee > 0.00 
    THEN

        UPDATE active_rental AS ar
        SET ar.fees_charged = p_late_fee,
            ar.fees_paid = ar.fees_paid + p_fees_paid,
            ar.return_date = p_return_date,
            ar.notes = CONCAT(IFNULL(ar.notes, ''), '\n', IFNULL(p_notes, ''))
        WHERE rental_id = p_rental_id;

    ELSE

        UPDATE active_rental AS ar
        SET ar.fees_paid = ar.fees_paid + p_fees_paid,
            ar.return_date = p_return_date,
            ar.notes = CONCAT(IFNULL(ar.notes, ''), '\n', IFNULL(p_notes, ''))
        WHERE rental_id = p_rental_id;

    END IF;


    UPDATE library_catalog.item AS itm
    JOIN active_rental AS ar 
        ON itm.item_id = ar.item_id
    SET itm.item_status_code = 'AV'
    WHERE ar.rental_id = p_rental_id;


    IF (SELECT ar.fees_charged - ar.fees_paid
        FROM active_rental AS ar
        WHERE ar.rental_id = p_rental_id) > 0.00 
    THEN

        SET p_active_rental_concluded = FALSE;

    ELSE

        SET p_active_rental_concluded = TRUE;

        DELETE FROM library_activity.active_rental
        WHERE rental_id = p_rental_id;

    END IF;

    COMMIT;

END;