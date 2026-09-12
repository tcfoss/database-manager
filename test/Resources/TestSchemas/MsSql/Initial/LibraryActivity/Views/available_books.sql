CREATE VIEW library_activity.available_books
AS
SELECT
    bc.book_id,
    bc.full_title
FROM library_catalog.book AS bc
WHERE NOT EXISTS (
    SELECT 1
    FROM library_activity.active_rental AS ar
    WHERE ar.book_id = bc.book_id
    AND ar.really_active = 'Y'
);
