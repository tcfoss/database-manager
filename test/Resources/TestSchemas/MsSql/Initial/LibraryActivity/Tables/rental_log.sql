CREATE TABLE library_activity.rental_log
(
    log_id INT NOT NULL IDENTITY(1,1),
    rental_id INT NOT NULL,
    book_id INT NOT NULL,
    patron_id INT NOT NULL,
    rental_date DATETIME2 NOT NULL,
    due_date DATETIME2 NOT NULL,
    return_date DATETIME2 NULL,
    fees_charged DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    fees_paid DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    notes NVARCHAR(MAX) NULL,
    log_date DATETIME2 NOT NULL DEFAULT GETDATE(),
    CONSTRAINT pk_rental_log PRIMARY KEY (log_id),
    CONSTRAINT fk_rental_log_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id),
    CONSTRAINT fk_rental_log_patron FOREIGN KEY (patron_id) REFERENCES library_identity.patron (patron_id)
);

GO

CREATE INDEX idx_rental_log_rental_id ON library_activity.rental_log (rental_id);
CREATE INDEX idx_rental_log_book_id ON library_activity.rental_log (book_id);
CREATE INDEX idx_rental_log_patron_id ON library_activity.rental_log (patron_id);
