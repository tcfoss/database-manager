CREATE TABLE library_catalog.book_genre
(
    book_id INT NOT NULL,
    genre_id INT NOT NULL,
    display_order SMALLINT NOT NULL DEFAULT 0,
    CONSTRAINT pk_book_genre PRIMARY KEY (book_id, genre_id),
    CONSTRAINT fk_book_genre_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id),
    CONSTRAINT fk_book_genre_genre FOREIGN KEY (genre_id) REFERENCES library_catalog.genre (genre_id)
);

GO

CREATE INDEX idx_book_genre_genre_id ON library_catalog.book_genre (genre_id);
