CREATE TABLE identifier_type
(
    identifier_type_code CHAR(2) NOT NULL,
    identifier_name VARCHAR(10) NOT NULL,
    identifier_description VARCHAR(100) NOT NULL,
    PRIMARY KEY (identifier_type_code),
    CONSTRAINT uc_identifier_type UNIQUE KEY (identifier_name)
);