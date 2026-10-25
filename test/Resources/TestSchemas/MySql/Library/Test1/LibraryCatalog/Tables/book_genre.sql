CREATE TABLE book_genre
(
    book_id INT NOT NULL,
    genre_id INT NOT NULL,
    display_order SMALLINT NOT NULL DEFAULT 0,
    PRIMARY KEY (book_id, genre_id),
    KEY idx_book_genre_genre_id (genre_id),
    CONSTRAINT fk_book_genre_book FOREIGN KEY (book_id) REFERENCES book(book_id) ON DELETE RESTRICT,
    CONSTRAINT fk_book_genre_genre FOREIGN KEY (genre_id) REFERENCES genre(genre_id) ON DELETE RESTRICT
);
