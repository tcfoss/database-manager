CREATE VIEW library_activity.available_books AS
SELECT b.book_id, b.full_title
FROM library_catalog.book AS b
WHERE NOT EXISTS (
    SELECT 1
    FROM library_activity.active_rental AS r
    WHERE r.book_id = b.book_id
      AND r.really_active = 'Y'
);
