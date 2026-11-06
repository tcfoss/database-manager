CREATE TABLE library_catalog.book_search
(
    book_id integer PRIMARY KEY,
    title varchar(400) NOT NULL,
    description text,
    authors varchar(5000),
    genres varchar(1000),
    CONSTRAINT fk_book_search_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id) ON DELETE CASCADE
);
CREATE INDEX idx_book_search ON library_catalog.book_search USING GIN
    (to_tsvector('english', coalesce(title, '') || ' ' || coalesce(description, '') || ' ' || coalesce(authors, '') || ' ' || coalesce(genres, '')));
