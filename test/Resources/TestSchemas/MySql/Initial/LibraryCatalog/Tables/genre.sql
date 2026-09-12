CREATE TABLE genre
(
    genre_id INT NOT NULL AUTO_INCREMENT,
    parent_id INT NULL,
    short_name VARCHAR(25) NOT NULL,
    full_name VARCHAR(100) NOT NULL,
    PRIMARY KEY (genre_id),
    CONSTRAINT uc_genre UNIQUE KEY (parent_id, short_name),
    CONSTRAINT fk_genre_parent FOREIGN KEY (parent_id) REFERENCES genre (genre_id) ON DELETE RESTRICT
);
