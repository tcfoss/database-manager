CREATE FUNCTION library_activity.search_available_books(search_term varchar(255))
RETURNS TABLE
(
    book_id integer,
    full_title varchar(400),
    title varchar(400),
    description text,
    authors varchar(5000),
    genres varchar(1000)
)
LANGUAGE SQL
STABLE
AS $$
    SELECT b.book_id, b.full_title, s.title, s.description, s.authors, s.genres
    FROM library_activity.available_books AS b
    JOIN library_catalog.book_search AS s ON s.book_id = b.book_id
    WHERE to_tsvector('english', coalesce(s.title, '') || ' ' || coalesce(s.description, '') || ' '
          || coalesce(s.authors, '') || ' ' || coalesce(s.genres, ''))
          @@ plainto_tsquery('english', search_term)
       OR b.full_title ILIKE '%' || search_term || '%'
       OR s.description ILIKE '%' || search_term || '%'
$$;
