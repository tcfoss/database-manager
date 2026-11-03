CREATE FUNCTION library_activity.log_active_rental()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        INSERT INTO library_activity.rental_log
            (rental_id, book_id, patron_id, rental_date, due_date, return_date,
             fees_charged, fees_paid, notes, log_date)
        VALUES
            (OLD.rental_id, OLD.book_id, OLD.patron_id, OLD.rental_date, OLD.due_date,
             OLD.return_date, OLD.fees_charged, OLD.fees_paid, OLD.notes, CURRENT_TIMESTAMP);
        RETURN OLD;
    END IF;

    INSERT INTO library_activity.rental_log
        (rental_id, book_id, patron_id, rental_date, due_date, return_date,
         fees_charged, fees_paid, notes, log_date)
    VALUES
        (NEW.rental_id, NEW.book_id, NEW.patron_id, NEW.rental_date, NEW.due_date,
         NEW.return_date, NEW.fees_charged, NEW.fees_paid, NEW.notes, CURRENT_TIMESTAMP);
    RETURN NEW;
END;
$$;

CREATE FUNCTION library_activity.guard_active_rental_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.fees_charged > OLD.fees_paid THEN
        RAISE EXCEPTION 'Cannot delete active rental with outstanding fees.';
    END IF;
    RETURN OLD;
END;
$$;

CREATE TRIGGER tr_active_rental_after_insert
AFTER INSERT ON library_activity.active_rental
FOR EACH ROW EXECUTE FUNCTION library_activity.log_active_rental();
CREATE TRIGGER tr_active_rental_after_update
AFTER UPDATE ON library_activity.active_rental
FOR EACH ROW EXECUTE FUNCTION library_activity.log_active_rental();
CREATE TRIGGER tr_active_rental_before_delete
BEFORE DELETE ON library_activity.active_rental
FOR EACH ROW EXECUTE FUNCTION library_activity.guard_active_rental_delete();
