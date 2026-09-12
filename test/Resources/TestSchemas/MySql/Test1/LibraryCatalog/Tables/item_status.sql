CREATE TABLE item_status
(
    item_status_code CHAR(2) NOT NULL,
    item_status_name VARCHAR(50) NOT NULL,
    item_status_description VARCHAR(200) NOT NULL,
    PRIMARY KEY (item_status_code),
    CONSTRAINT uc_item_status UNIQUE KEY (item_status_name)
)