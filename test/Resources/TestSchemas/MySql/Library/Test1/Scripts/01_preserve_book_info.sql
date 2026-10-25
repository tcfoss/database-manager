CREATE TEMPORARY TABLE book_save
(
    book_id INT NOT NULL,
    acquisition_cost DECIMAL(10,2) NOT NULL,
    acquisition_date DATE NULL,
    isbn VARCHAR(20) NULL,
    PRIMARY KEY (book_id)
);

INSERT INTO book_save 
(
    book_id, 
    acquisition_cost, 
    acquisition_date, 
    isbn
)
SELECT 
    book_id, 
    acquisition_cost, 
    acquisition_date, 
    isbn
FROM book;