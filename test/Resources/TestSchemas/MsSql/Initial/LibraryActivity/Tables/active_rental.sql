CREATE TABLE library_activity.active_rental
(
    rental_id INT NOT NULL IDENTITY(1,1),
    book_id INT NOT NULL,
    patron_id INT NOT NULL,
    rental_date DATETIME2 NOT NULL DEFAULT GETDATE(),
    due_date DATETIME2 NOT NULL DEFAULT DATEADD(DAY, 14, GETDATE()),
    return_date DATETIME2 NULL,
    fees_charged DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    fees_paid DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    notes NVARCHAR(MAX) NULL,
    really_active AS (IIF(return_date IS NULL, 'Y', NULL)) PERSISTED,
    CONSTRAINT pk_active_rental PRIMARY KEY (rental_id),
    CONSTRAINT uc_active_rental_book_id UNIQUE (book_id, really_active),
    CONSTRAINT fk_active_rental_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id),
    CONSTRAINT fk_active_rental_patron FOREIGN KEY (patron_id) REFERENCES library_identity.patron (patron_id)
);

GO

CREATE INDEX idx_active_rental_patron_id ON library_activity.active_rental (patron_id);
