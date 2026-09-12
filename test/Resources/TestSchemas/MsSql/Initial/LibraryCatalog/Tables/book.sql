CREATE TABLE library_catalog.book
(
    book_id INT NOT NULL IDENTITY(1,1),
    title NVARCHAR(255) NOT NULL,
    subtitle NVARCHAR(100) NULL,
    description NVARCHAR(MAX) NULL,
    publication_year SMALLINT NULL,
    publisher NVARCHAR(100) NULL,
    acquisition_cost DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    acquisition_date DATE NULL,
    isbn NVARCHAR(20) NULL,
    full_title AS (title + ISNULL(': ' + subtitle, '') + IIF(publication_year IS NOT NULL, ' (' + CAST(publication_year AS NVARCHAR(4)) + ')', '')),
    CONSTRAINT pk_book PRIMARY KEY (book_id)
);
