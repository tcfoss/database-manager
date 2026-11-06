CREATE TABLE library_catalog.book_genre
(
    book_id integer NOT NULL,
    genre_id integer NOT NULL,
    display_order smallint NOT NULL DEFAULT 0,
    PRIMARY KEY (book_id, genre_id),
    CONSTRAINT fk_book_genre_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id) ON DELETE RESTRICT,
    CONSTRAINT fk_book_genre_genre FOREIGN KEY (genre_id) REFERENCES library_catalog.genre (genre_id) ON DELETE RESTRICT
);
CREATE INDEX idx_book_genre_genre_id ON library_catalog.book_genre (genre_id);
