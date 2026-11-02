CREATE FUNCTION library_activity.get_late_charge(
    p_rental_id integer,
    p_lateness_rate numeric(10, 2)
)
RETURNS numeric(10, 2)
LANGUAGE SQL
STABLE
AS $$
    SELECT coalesce(
        GREATEST(CURRENT_DATE - r.due_date::date, 0) * p_lateness_rate,
        0
    )
    FROM library_activity.active_rental AS r
    WHERE r.rental_id = p_rental_id
$$;
