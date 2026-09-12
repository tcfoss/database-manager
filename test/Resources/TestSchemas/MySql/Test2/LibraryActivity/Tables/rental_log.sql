CREATE TABLE rental_log
(
    log_id INT NOT NULL AUTO_INCREMENT,
    loan_id INT NOT NULL COMMENT 'Fake foreign key to allow loan to be deleted',
    book_id INT NOT NULL,
    patron_id INT NOT NULL,
    rental_date TIMESTAMP NOT NULL,
    due_date TIMESTAMP NOT NULL,
    return_date TIMESTAMP NULL,
    fees_charged DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    fees_paid DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    notes TEXT NULL,
    log_date TIMESTAMP NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    PRIMARY KEY (log_id),
    KEY idx_rental_log_loan_id (loan_id),
    KEY idx_rental_log_book_id (book_id),
    KEY idx_rental_log_patron_id (patron_id),
    CONSTRAINT fk_rental_log_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id) ON DELETE RESTRICT,
    CONSTRAINT fk_rental_log_patron FOREIGN KEY (patron_id) REFERENCES library_identity.patron (patron_id) ON DELETE RESTRICT
);
