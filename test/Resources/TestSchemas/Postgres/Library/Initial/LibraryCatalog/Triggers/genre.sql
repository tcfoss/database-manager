CREATE FUNCTION library_catalog.set_genre_full_name()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    parent_name varchar(25);
BEGIN
    IF NEW.parent_id IS NOT NULL THEN
        SELECT g.short_name INTO parent_name
        FROM library_catalog.genre AS g
        WHERE g.genre_id = NEW.parent_id;
        IF parent_name IS NULL THEN
            RAISE EXCEPTION 'Parent genre does not exist.';
        END IF;
    END IF;

    IF NEW.parent_id IS NULL AND EXISTS (
        SELECT 1 FROM library_catalog.genre AS g
        WHERE g.short_name = NEW.short_name AND g.genre_id <> coalesce(NEW.genre_id, -1)
    ) THEN
        RAISE EXCEPTION 'A genre with this short name already exists at the root level.';
    END IF;

    NEW.full_name := CASE
        WHEN parent_name IS NULL THEN NEW.short_name
        ELSE parent_name || ' / ' || NEW.short_name
    END;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_genre_set_full_name
BEFORE INSERT OR UPDATE OF parent_id, short_name ON library_catalog.genre
FOR EACH ROW EXECUTE FUNCTION library_catalog.set_genre_full_name();
