CREATE TABLE library_catalog.genre
(
    genre_id INT NOT NULL IDENTITY(1,1),
    parent_id INT NULL,
    short_name NVARCHAR(25) NOT NULL,
    full_name NVARCHAR(100) NOT NULL,
    CONSTRAINT pk_genre PRIMARY KEY (genre_id),
    CONSTRAINT uc_genre UNIQUE (parent_id, short_name),
    CONSTRAINT fk_genre_parent FOREIGN KEY (parent_id) REFERENCES library_catalog.genre (genre_id)
);
