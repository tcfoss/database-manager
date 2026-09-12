CREATE TEMPORARY TABLE active_rental_save
(
    rental_id INT NOT NULL,
    book_id INT NOT NULL,
    item_status CHAR(2) NOT NULL,
    PRIMARY KEY (rental_id)
);

INSERT INTO active_rental_save
(
    rental_id,
    book_id,
    item_status
)
SELECT
    ar.rental_id,
    ar.book_id,
    IF (ar.really_active IS NOT NULL, 'LO', 'AV')
FROM library_activity.active_rental AS ar;