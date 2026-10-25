CREATE TABLE patron
(
    patron_id INT NOT NULL AUTO_INCREMENT,
    card_number VARCHAR(20) NOT NULL,
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50) NOT NULL,
    email VARCHAR(100) NULL,
    phone_number VARCHAR(20) NOT NULL,
    address VARCHAR(255) NOT NULL,
    city VARCHAR(100) NOT NULL,
    state CHAR(2) NOT NULL,
    zip_code VARCHAR(10) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    registration_date DATE NOT NULL,
    PRIMARY KEY (patron_id),
    CONSTRAINT uc_patron UNIQUE KEY (card_number)
);
