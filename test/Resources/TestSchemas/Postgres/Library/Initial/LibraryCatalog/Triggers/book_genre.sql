CREATE FUNCTION library_catalog.sync_book_genres()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    target_book_id integer;
BEGIN
    target_book_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.book_id ELSE NEW.book_id END;
    UPDATE library_catalog.book_search AS s
    SET genres = (
        SELECT string_agg(g.full_name, ', ' ORDER BY bg.display_order)
        FROM library_catalog.book_genre AS bg
        JOIN library_catalog.genre AS g ON g.genre_id = bg.genre_id
        WHERE bg.book_id = target_book_id
    )
    WHERE s.book_id = target_book_id;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER tr_book_genre_after_insert
AFTER INSERT ON library_catalog.book_genre
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_genres();
CREATE TRIGGER tr_book_genre_after_update
AFTER UPDATE ON library_catalog.book_genre
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_genres();
CREATE TRIGGER tr_book_genre_after_delete
AFTER DELETE ON library_catalog.book_genre
FOR EACH ROW EXECUTE FUNCTION library_catalog.sync_book_genres();
