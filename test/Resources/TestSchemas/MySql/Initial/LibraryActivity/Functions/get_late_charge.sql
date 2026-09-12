DELIMITER //

CREATE
    DEFINER = 'admin'@'localhost'
FUNCTION get_late_charge(rental_id INT, lateness_rate DECIMAL(10,2))
RETURNS DECIMAL(10,2)
    LANGUAGE SQL
    READS SQL DATA
    SQL SECURITY DEFINER
BEGIN

    DECLARE late_charge DECIMAL(10,2) DEFAULT 0.00;

    IF CURRENT_DATE() <= (SELECT due_date
                          FROM active_rental AS ar
                          WHERE ar.rental_id = rental_id) THEN
        RETURN 0.00; -- No late charge if not overdue
    END IF;

    SELECT
        (DATEDIFF(CURRENT_DATE(), ar.due_date) * lateness_rate) INTO late_charge
    FROM active_rental AS ar
    WHERE
        ar.rental_id = rental_id;

    RETURN IFNULL(late_charge, 0);
END//

DELIMITER ;
