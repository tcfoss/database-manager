CREATE TABLE library_catalog.book_genre
(
    book_id integer NOT NULL REFERENCES library_catalog.book (book_id) ON DELETE RESTRICT,
    genre_id integer NOT NULL REFERENCES library_catalog.genre (genre_id) ON DELETE RESTRICT,
    display_order smallint NOT NULL DEFAULT 0,
    PRIMARY KEY (book_id, genre_id)
);
CREATE INDEX idx_book_genre_genre_id ON library_catalog.book_genre (genre_id);
