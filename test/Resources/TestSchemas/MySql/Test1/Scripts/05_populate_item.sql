DELIMITER //

CREATE PROCEDURE populate_item()
    SQL SECURITY DEFINER
BEGIN

    INSERT INTO item
    (
        book_id,
        acquisition_id,
        item_status_code,
        barcode,
        location_code,
        shelf_code
    )
    SELECT
        bs.book_id,
        aid.acquisition_id,
        IF(ars.item_status IS NOT NULL, ars.item_status, 'AV'),
        CONCAT('BC', LPAD(bs.book_id, 8, '0')) AS barcode,
        'MAIN',
        'SHELF1'
    FROM book_save AS bs
    INNER JOIN acquisition_initial_data AS aid
        ON bs.book_id = aid.book_id
    LEFT JOIN active_rental_save AS ars
        ON bs.book_id = ars.book_id;

    UPDATE library_activity.active_rental AS ar
    INNER JOIN active_rental_save AS ars
        ON ar.rental_id = ars.rental_id
    INNER JOIN book_save AS bs
        ON ars.book_id = bs.book_id
    INNER JOIN item AS itm
        ON bs.book_id = itm.book_id
    SET ar.item_id = itm.item_id;

    UPDATE library_activity.rental_log AS rl
    INNER JOIN active_rental_save AS ars
        ON rl.rental_id = ars.rental_id
    INNER JOIN book_save AS bs
        ON ars.book_id = bs.book_id
    INNER JOIN item AS itm
        ON bs.book_id = itm.book_id
    SET rl.item_id = itm.item_id;

END//

CALL populate_item()//

DROP PROCEDURE populate_item//

DELIMITER ;