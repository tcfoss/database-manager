CREATE TABLE library_catalog.book_author
(
    book_id INT NOT NULL,
    contributor_id INT NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    CONSTRAINT pk_book_author PRIMARY KEY (book_id, contributor_id),
    CONSTRAINT fk_book_author_book FOREIGN KEY (book_id) REFERENCES library_catalog.book (book_id) ON DELETE NO ACTION ON UPDATE CASCADE,
    CONSTRAINT fk_book_author_contributor FOREIGN KEY (contributor_id) REFERENCES library_catalog.contributor (contributor_id) ON DELETE NO ACTION ON UPDATE CASCADE
);

GO

CREATE INDEX idx_book_author_contributor_id ON library_catalog.book_author (contributor_id);
