CREATE TABLE book
(
    book_id INT NOT NULL AUTO_INCREMENT,
    title VARCHAR(255) NOT NULL,
    subtitle VARCHAR(100) NULL,
    description TEXT NULL,
    publication_year SMALLINT NULL,
    publisher VARCHAR(100) NULL,
    acquisition_cost DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    acquisition_date DATE NULL,
    isbn VARCHAR(20) NULL,
    full_title VARCHAR(400) GENERATED ALWAYS AS (CONCAT( CONCAT_WS(': ', title, subtitle), IF (publication_year IS NOT NULL, CONCAT(' (', publication_year, ')'), '') )) VIRTUAL,
    PRIMARY KEY (book_id)
);
