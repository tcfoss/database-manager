CREATE TABLE library_catalog.book_search
(
    book_id INT NOT NULL,
    title NVARCHAR(400) NOT NULL,
    description NVARCHAR(MAX) NULL,
    authors NVARCHAR(MAX) NULL,
    genres NVARCHAR(1000) NULL,
    CONSTRAINT pk_book_search PRIMARY KEY (book_id),
    CONSTRAINT fk_book_search_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id) ON DELETE CASCADE
);
