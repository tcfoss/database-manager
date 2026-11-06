CREATE TABLE library_catalog.book_author
(
    book_id integer NOT NULL,
    contributor_id integer NOT NULL,
    display_order integer NOT NULL DEFAULT 0,
    PRIMARY KEY (book_id, contributor_id),
    CONSTRAINT fk_book_author_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_book_author_contributor FOREIGN KEY (contributor_id) REFERENCES library_catalog.contributor (contributor_id) ON DELETE RESTRICT ON UPDATE CASCADE
);
CREATE INDEX idx_book_author_contributor_id ON library_catalog.book_author (contributor_id);
