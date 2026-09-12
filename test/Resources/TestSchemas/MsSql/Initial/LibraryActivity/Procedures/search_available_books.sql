CREATE PROCEDURE library_activity.search_available_books
    @search_term NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        ab.book_id,
        ab.full_title,
        bs.title,
        bs.description,
        bs.authors,
        bs.genres
    FROM library_activity.available_books AS ab
    INNER JOIN library_catalog.book_search AS bs
        ON ab.book_id = bs.book_id
    WHERE ab.full_title LIKE '%' + @search_term + '%'
    OR bs.description LIKE '%' + @search_term + '%'
    OR bs.authors LIKE '%' + @search_term + '%'
    OR bs.genres LIKE '%' + @search_term + '%';
END;
