CREATE TABLE library_catalog.book_author
(
    book_id integer NOT NULL REFERENCES library_catalog.book (book_id) ON DELETE RESTRICT ON UPDATE CASCADE,
    contributor_id integer NOT NULL REFERENCES library_catalog.contributor (contributor_id) ON DELETE RESTRICT ON UPDATE CASCADE,
    display_order integer NOT NULL DEFAULT 0,
    PRIMARY KEY (book_id, contributor_id)
);
CREATE INDEX idx_book_author_contributor_id ON library_catalog.book_author (contributor_id);
