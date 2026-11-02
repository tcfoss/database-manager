CREATE FUNCTION library_catalog.sync_book_authors()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    target_book_id integer;
BEGIN
    target_book_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.book_id ELSE NEW.book_id END;
    UPDATE library_catalog.book_search AS s
    SET authors = (
        SELECT string_agg(c.full_name, ', ' ORDER BY ba.display_order)
        FROM library_catalog.book_author AS ba
        JOIN library_catalog.contributor AS c ON c.contributor_id = ba.contributor_id
        WHERE ba.book_id = target_book_id
    )
    WHERE s.book_id = target_book_id;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_book_author_after_insert
AFTER INSERT ON library_catalog.book_author
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_authors();
CREATE TRIGGER tr_book_author_after_update
AFTER UPDATE ON library_catalog.book_author
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_authors();
CREATE TRIGGER tr_book_author_after_delete
AFTER DELETE ON library_catalog.book_author
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_authors();
