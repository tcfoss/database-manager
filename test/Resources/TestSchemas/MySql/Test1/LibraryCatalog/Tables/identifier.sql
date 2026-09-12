CREATE TABLE identifier
(
    identifier_id INT NOT NULL AUTO_INCREMENT,
    book_id INT NOT NULL,
    identifier_type_code CHAR(2) NOT NULL,
    identifier_value VARCHAR(50) NOT NULL,
    PRIMARY KEY (identifier_id),
    INDEX idx_identifier_book (book_id),
    INDEX idx_identifier_identifier_type_code (identifier_type_code),
    CONSTRAINT fk_identifier_identifier_type_code FOREIGN KEY (identifier_type_code) REFERENCES identifier_type(identifier_type_code) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_identifier_book FOREIGN KEY (book_id) REFERENCES book(book_id) ON DELETE CASCADE ON UPDATE CASCADE
);