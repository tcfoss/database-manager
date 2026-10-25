CREATE TABLE site_user
(
    user_id INT NOT NULL AUTO_INCREMENT,
    patron_id INT NULL,
    employee_id INT NULL,
    password_hash VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    email VARCHAR(100) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    registration_date DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login DATETIME NULL,
    PRIMARY KEY (user_id),
    CONSTRAINT uc_site_user UNIQUE KEY (email),
    KEY idx_site_user_patron_id (patron_id),
    KEY idx_site_user_employee_id (employee_id),
    CONSTRAINT fk_site_user_patron FOREIGN KEY (patron_id) REFERENCES patron(patron_id) ON DELETE RESTRICT,
    CONSTRAINT fk_site_user_employee FOREIGN KEY (employee_id) REFERENCES employee(employee_id) ON DELETE RESTRICT,
    CONSTRAINT chk_site_user_one_of_patron_or_employee CHECK (patron_id IS NOT NULL OR employee_id IS NOT NULL)
);
