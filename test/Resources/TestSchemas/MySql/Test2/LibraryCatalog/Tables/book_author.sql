CREATE TABLE book_author
(
    book_id INT NOT NULL,
    contributor_id INT NOT NULL,
    display_order INT NOT NULL DEFAULT 0,
    PRIMARY KEY (book_id, contributor_id),
    KEY idx_book_author_contributor_id (contributor_id),
    CONSTRAINT fk_book_author_book FOREIGN KEY (book_id) REFERENCES book(book_id) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_book_author_contributor FOREIGN KEY (contributor_id) REFERENCES contributor(contributor_id) ON DELETE RESTRICT ON UPDATE CASCADE
);
