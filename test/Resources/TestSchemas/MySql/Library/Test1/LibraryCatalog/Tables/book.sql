CREATE TABLE book
(
    book_id INT NOT NULL AUTO_INCREMENT,
    title VARCHAR(255) NOT NULL,
    subtitle VARCHAR(100) NULL,
    description TEXT NULL,
    publication_year SMALLINT NULL,
    publisher VARCHAR(100) NULL,
    full_title VARCHAR(400) GENERATED ALWAYS AS (CONCAT( CONCAT_WS(': ', title, subtitle), IF (publication_year IS NOT NULL, CONCAT(' (', publication_year, ')'), '') )) VIRTUAL,
    subtitle_gen VARCHAR(100) GENERATED ALWAYS AS (IFNULL(subtitle, '')) VIRTUAL,
    description_gen TEXT GENERATED ALWAYS AS (IFNULL(description, '')) VIRTUAL,
    PRIMARY KEY (book_id),
    CONSTRAINT uc_book_expanded_title UNIQUE (title, subtitle_gen, description_gen(200))
);
