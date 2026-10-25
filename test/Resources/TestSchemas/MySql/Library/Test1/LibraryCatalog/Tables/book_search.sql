CREATE TABLE book_search
(
    book_id INT NOT NULL,
    title VARCHAR(400) NOT NULL,
    description TEXT NULL,
    authors VARCHAR(5000) NULL,
    genres VARCHAR(1000) NULL,
    PRIMARY KEY (book_id),
    CONSTRAINT fk_book_search_book FOREIGN KEY (book_id) REFERENCES book(book_id) ON DELETE CASCADE,
    FULLTEXT KEY idx_book_search (title, description, authors, genres)
);
