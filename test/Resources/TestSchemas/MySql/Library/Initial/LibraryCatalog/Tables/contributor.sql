CREATE TABLE contributor
(
    contributor_id INT NOT NULL AUTO_INCREMENT,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL DEFAULT '',
    disambiguation VARCHAR(100) NOT NULL DEFAULT '',
    birth_year SMALLINT NULL,
    death_year SMALLINT NULL,
    full_name VARCHAR(200) 
        GENERATED ALWAYS AS (IF (last_name <> '', CONCAT(first_name, ' ', last_name), first_name)) STORED,
    sortable_name VARCHAR(200) 
        GENERATED ALWAYS AS (IF (last_name <> '', CONCAT(last_name, ', ', first_name), first_name)) STORED,
    PRIMARY KEY (contributor_id),
    CONSTRAINT uc_contributor UNIQUE KEY (first_name, last_name, disambiguation),
    KEY idx_contributor_sortable_name (sortable_name)
);
