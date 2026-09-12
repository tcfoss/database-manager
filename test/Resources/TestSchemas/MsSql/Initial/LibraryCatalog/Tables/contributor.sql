CREATE TABLE library_catalog.contributor
(
    contributor_id INT NOT NULL IDENTITY(1,1),
    first_name NVARCHAR(100) NOT NULL,
    last_name NVARCHAR(100) NOT NULL DEFAULT '',
    disambiguation NVARCHAR(100) NOT NULL DEFAULT '',
    birth_year SMALLINT NULL,
    death_year SMALLINT NULL,
    full_name AS (IIF(last_name <> '', first_name + ' ' + last_name, first_name)) PERSISTED,
    sortable_name AS (IIF(last_name <> '', last_name + ', ' + first_name, first_name)) PERSISTED,
    CONSTRAINT pk_contributor PRIMARY KEY (contributor_id),
    CONSTRAINT uc_contributor UNIQUE (first_name, last_name, disambiguation)
);

CREATE INDEX idx_contributor_sortable_name ON library_catalog.contributor (sortable_name);
