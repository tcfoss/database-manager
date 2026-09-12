CREATE TABLE acquisition
(
    acquisition_id INT NOT NULL AUTO_INCREMENT,
    acquisition_date DATE NOT NULL,
    cost DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    fund_code VARCHAR(10) NULL,
    supplier VARCHAR(100) NULL,
    was_donated BOOLEAN NOT NULL DEFAULT FALSE,
    PRIMARY KEY (acquisition_id)
);