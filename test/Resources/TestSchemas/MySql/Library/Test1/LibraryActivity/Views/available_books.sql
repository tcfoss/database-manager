CREATE
    DEFINER = 'admin'@'localhost'
    SQL SECURITY INVOKER
VIEW available_books
AS
SELECT
    bc.book_id,
    bc.full_title
FROM library_catalog.book AS bc
INNER JOIN library_catalog.item AS itm
    ON bc.book_id = itm.book_id
WHERE NOT EXISTS (
    SELECT 1
    FROM active_rental AS ar
    WHERE ar.item_id = itm.item_id
    AND ar.really_active = 'Y'
);
