CREATE TEMPORARY TABLE acquisition_initial_data
(
    acquisition_date DATE NOT NULL,
    acquisition_cost DECIMAL(10,2) NOT NULL,
    book_id INT NOT NULL,
    acquisition_id INT NOT NULL AUTO_INCREMENT,
    PRIMARY KEY (acquisition_id)
);

INSERT INTO acquisition_initial_data
(
    acquisition_date, 
    acquisition_cost,
    book_id
)
SELECT 
    bs.acquisition_date, 
    bs.acquisition_cost,
    bs.book_id
FROM book_save AS bs;

INSERT INTO acquisition
(
    acquisition_id,
    acquisition_date, 
    cost
)
SELECT
    acquisition_id,
    acquisition_date, 
    acquisition_cost
FROM acquisition_initial_data;