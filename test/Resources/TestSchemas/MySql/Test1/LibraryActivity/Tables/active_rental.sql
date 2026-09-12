CREATE TABLE active_rental
(
    rental_id INT NOT NULL AUTO_INCREMENT,
    item_id INT NOT NULL,
    patron_id INT NOT NULL,
    rental_date TIMESTAMP NOT NULL DEFAULT (CURRENT_TIMESTAMP()),
    due_date TIMESTAMP NOT NULL DEFAULT (CURRENT_TIMESTAMP() + INTERVAL 14 DAY),
    return_date TIMESTAMP NULL,
    fees_charged DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    fees_paid DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    notes TEXT NULL,
    really_active ENUM('Y') GENERATED ALWAYS AS (IF (return_date IS NULL, 'Y', NULL)) COMMENT 'If null, the book is available to loan but cannot be deleted until fees are paid.',
    PRIMARY KEY (rental_id),
    CONSTRAINT uc_active_rental_item_id UNIQUE KEY (item_id, really_active),
    KEY idx_active_rental_patron_id (patron_id),
    CONSTRAINT fk_active_rental_item FOREIGN KEY (item_id) REFERENCES library_catalog.item (item_id) ON DELETE RESTRICT,
    CONSTRAINT fk_active_rental_patron FOREIGN KEY (patron_id) REFERENCES library_identity.patron (patron_id) ON DELETE RESTRICT
);
