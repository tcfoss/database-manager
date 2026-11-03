CREATE FUNCTION library_catalog.sync_book_search()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        DELETE FROM library_catalog.book_search WHERE book_id = OLD.book_id;
        RETURN OLD;
    ELSIF TG_OP = 'INSERT' THEN
        INSERT INTO library_catalog.book_search (book_id, title, description)
        VALUES (NEW.book_id, NEW.full_title, NEW.description);
        RETURN NEW;
    ELSE
        UPDATE library_catalog.book_search
        SET title = NEW.full_title, description = NEW.description
        WHERE book_id = NEW.book_id;
        RETURN NEW;
    END IF;
END;
$$;

CREATE TRIGGER tr_book_after_insert
AFTER INSERT ON library_catalog.book
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_search();
CREATE TRIGGER tr_book_after_update
AFTER UPDATE ON library_catalog.book
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_search();
CREATE TRIGGER tr_book_before_delete
BEFORE DELETE ON library_catalog.book
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_search();
