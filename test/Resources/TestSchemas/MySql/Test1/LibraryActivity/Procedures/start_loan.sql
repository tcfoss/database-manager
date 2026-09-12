CREATE 
    DEFINER = 'admin'@'localhost'
PROCEDURE start_loan
(
    IN p_item_id INT,
    IN p_barcode VARCHAR(50),
    IN p_patron_id INT,
    IN p_due_date TIMESTAMP,
    IN p_notes TEXT
)
    SQL SECURITY DEFINER
BEGIN

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    IF p_item_id IS NULL 
    THEN
        SELECT itm.item_id INTO p_item_id
        FROM library_catalog.item AS itm
        WHERE itm.barcode = p_barcode;
    END IF;

    IF p_item_id IS NULL 
    THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Item not found for given barcode.';
    END IF;

    INSERT INTO active_rental
    (
        item_id,
        patron_id,
        rental_date,
        due_date,
        notes
    )
    VALUES
    (
        p_item_id,
        p_patron_id,
        CURRENT_TIMESTAMP(),
        p_due_date,
        p_notes
    );

    UPDATE library_catalog.item
    SET item_status_code = 'LO'
    WHERE item_id = p_item_id;

    COMMIT;

END;