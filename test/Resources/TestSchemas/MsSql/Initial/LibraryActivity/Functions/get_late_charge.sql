CREATE FUNCTION library_activity.get_late_charge(@rental_id INT, @lateness_rate DECIMAL(10,2))
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @late_charge DECIMAL(10,2) = 0.00;

    IF CAST(GETDATE() AS DATE) <= (
        SELECT ar.due_date
        FROM library_activity.active_rental AS ar
        WHERE ar.rental_id = @rental_id
    )
    BEGIN
        RETURN 0.00;
    END

    SELECT
        @late_charge = DATEDIFF(day, ar.due_date, CAST(GETDATE() AS DATE)) * @lateness_rate
    FROM library_activity.active_rental AS ar
    WHERE ar.rental_id = @rental_id;

    RETURN ISNULL(@late_charge, 0);
END;
