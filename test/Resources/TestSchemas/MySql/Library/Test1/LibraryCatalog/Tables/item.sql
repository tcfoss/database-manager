CREATE TABLE item
(
    item_id INT NOT NULL AUTO_INCREMENT,
    book_id INT NOT NULL,
    acquisition_id INT NOT NULL,
    item_status_code CHAR(2) NOT NULL DEFAULT 'AV',
    barcode VARCHAR(50) NOT NULL,
    location_code VARCHAR(10) NOT NULL,
    shelf_code VARCHAR(10) NULL,
    PRIMARY KEY (item_id),
    KEY idx_item_book (book_id),
    KEY idx_item_acquisition (acquisition_id),
    KEY idx_item_item_status_code (item_status_code),
    CONSTRAINT fk_item_book FOREIGN KEY (book_id) REFERENCES book(book_id) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_item_acquisition FOREIGN KEY (acquisition_id) REFERENCES acquisition(acquisition_id) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_item_status FOREIGN KEY (item_status_code) REFERENCES item_status(item_status_code) ON DELETE RESTRICT ON UPDATE CASCADE
);