-- Insert genres
SET IDENTITY_INSERT library_catalog.genre ON;
INSERT INTO library_catalog.genre
(genre_id, parent_id, short_name, full_name)
VALUES
    (1, NULL, 'Fiction', 'Fiction'),
    (2, 1, 'Science Fiction', 'Fiction / Science Fiction'),
    (3, 1, 'Fantasy', 'Fiction / Fantasy'),
    (4, NULL, 'Nonfiction', 'Nonfiction');
SET IDENTITY_INSERT library_catalog.genre OFF;

-- Insert contributors
SET IDENTITY_INSERT library_catalog.contributor ON;
INSERT INTO library_catalog.contributor
(contributor_id, first_name, last_name, disambiguation, birth_year, death_year)
VALUES
    (1, 'Isaac', 'Asimov', '', 1920, 1992),
    (2, 'J.R.R.', 'Tolkien', '', 1892, 1973),
    (3, 'Mary', 'Shelley', '', 1797, 1851),
    (4, 'Stephen', 'Hawking', '', 1942, 2018),
    (5, 'Neil', 'Gaiman', '', 1960, NULL),
    (6, 'Terry', 'Pratchett', '', 1948, 2015),
    (7, 'Douglas', 'Adams', '', 1952, 2001),
    (8, 'John', 'Lloyd', '', 1951, NULL);
SET IDENTITY_INSERT library_catalog.contributor OFF;

-- Insert books
SET IDENTITY_INSERT library_catalog.book ON;
INSERT INTO library_catalog.book
(book_id, title, subtitle, description, publication_year, publisher, isbn, acquisition_cost, acquisition_date)
VALUES
    (1, 'Foundation', NULL, 'A science fiction classic.', 1951, 'Gnome Press', '978-0-553-80371-0', 10.99, '2020-01-15'),
    (2, 'The Hobbit', NULL, 'A fantasy adventure.', 1937, 'George Allen & Unwin', '978-0-618-00221-3', 12.50, '2020-02-20'),
    (3, 'Frankenstein', NULL, 'A gothic novel.', 1818, 'Lackington, Hughes, Harding, Mavor & Jones', '978-0-141-43947-1', 8.75, '2020-03-10'),
    (4, 'A Brief History of Time', NULL, 'Popular-science book on cosmology.', 1988, 'Bantam Books', '978-0-553-17521-3', 15.00, '2020-04-05'),
    (5, 'Good Omens', NULL, 'A comedic novel about the apocalypse.', 1990, 'Gollancz', '978-0-575-04800-1', 13.99, '2022-10-01'),
    (6, 'The Meaning of Liff', NULL, 'A humorous dictionary of toponymy.', 1983, 'Pan Books', '978-0-330-28121-4', 9.99, '2022-10-02');
SET IDENTITY_INSERT library_catalog.book OFF;

-- Insert book_author relationships
INSERT INTO library_catalog.book_author
(book_id, contributor_id, display_order)
VALUES
    (1, 1, 1),
    (2, 2, 1),
    (3, 3, 1),
    (4, 4, 1),
    (5, 5, 1),
    (5, 6, 2),
    (6, 7, 1),
    (6, 8, 2);

-- Insert book_genre relationships
INSERT INTO library_catalog.book_genre
(book_id, genre_id, display_order)
VALUES
    (1, 2, 1),
    (2, 3, 1),
    (3, 1, 1),
    (4, 4, 1),
    (5, 3, 1),
    (6, 1, 1);

-- Insert patrons
INSERT INTO library_identity.patron
(card_number, first_name, last_name, email, phone_number, address, city, state, zip_code, is_active, registration_date)
VALUES
    ('P1001', 'Alice', 'Smith', 'alice.smith@example.com', '555-1234', '123 Main St', 'Springfield', 'IL', '62701', 1, '2022-01-10'),
    ('P1002', 'Bob', 'Johnson', NULL, '555-5678', '456 Oak Ave', 'Springfield', 'IL', '62702', 1, '2022-02-15'),
    ('P1003', 'Eve', 'Davis', 'eve.davis@example.com', '555-2468', '987 Elm St', 'Springfield', 'IL', '62705', 1, '2022-03-12'),
    ('P1004', 'Frank', 'Miller', 'frank.miller@example.com', '555-1357', '654 Cedar Ave', 'Springfield', 'IL', '62706', 1, '2022-04-18'),
    ('P1005', 'Grace', 'Lee', 'grace.lee@example.com', '555-1111', '101 Birch St', 'Springfield', 'IL', '62707', 1, '2022-05-10'),
    ('P1006', 'Henry', 'Nguyen', 'henry.nguyen@example.com', '555-2222', '202 Willow Ave', 'Springfield', 'IL', '62708', 1, '2022-06-15'),
    ('P1007', 'Ivy', 'Martinez', 'ivy.martinez@example.com', '555-3333', '303 Aspen Rd', 'Springfield', 'IL', '62709', 1, '2022-07-20'),
    ('P1008', 'Jack', 'Wilson', 'jack.wilson@example.com', '555-4444', '404 Poplar Dr', 'Springfield', 'IL', '62710', 1, '2022-08-25'),
    ('P1009', 'Kara', 'Patel', 'kara.patel@example.com', '555-5555', '505 Spruce Ln', 'Springfield', 'IL', '62711', 1, '2022-09-30');

-- Insert employees
INSERT INTO library_identity.employee
(first_name, last_name, email, phone_number, address, city, state, zip_code, hire_date, job_title, salary, is_active, is_volunteer, date_of_birth)
VALUES
    ('Carol', 'Williams', 'carol.williams@example.com', '555-8765', '789 Pine Rd', 'Springfield', 'IL', '62703', '2021-03-01', 'Librarian', 42000.00, 1, 0, '1985-07-20'),
    ('David', 'Brown', 'david.brown@example.com', '555-4321', '321 Maple St', 'Springfield', 'IL', '62704', '2020-06-15', 'Assistant', 35000.00, 1, 1, '1990-11-05');

-- Insert site users
INSERT INTO library_identity.site_user
(patron_id, employee_id, password_hash, email, is_active, registration_date, last_login)
VALUES
    (1, NULL, 'hash1', 'alice.smith@example.com', 1, '2022-01-10 08:00:00', '2022-07-01 09:30:00'),
    (2, NULL, 'hash2', 'bob.johnson@example.com', 1, '2022-02-15 10:00:00', NULL),
    (NULL, 1, 'hash3', 'carol.williams@example.com', 1, '2021-03-01 08:30:00', '2022-07-02 10:00:00'),
    (NULL, 2, 'hash4', 'david.brown@example.com', 1, '2020-06-15 09:00:00', '2022-07-03 11:00:00'),
    (3, NULL, 'hash5', 'eve.davis@example.com', 1, '2022-03-12 11:00:00', NULL),
    (4, NULL, 'hash6', 'frank.miller@example.com', 1, '2022-04-18 12:00:00', NULL),
    (5, NULL, 'hash7', 'grace.lee@example.com', 1, '2022-05-10 09:00:00', NULL),
    (6, NULL, 'hash8', 'henry.nguyen@example.com', 1, '2022-06-15 10:30:00', NULL),
    (7, NULL, 'hash9', 'ivy.martinez@example.com', 1, '2022-07-20 11:15:00', NULL),
    (8, NULL, 'hash10', 'jack.wilson@example.com', 1, '2022-08-25 13:45:00', NULL),
    (9, NULL, 'hash11', 'kara.patel@example.com', 1, '2022-09-30 15:20:00', NULL);

-- Insert active rentals
INSERT INTO library_activity.active_rental
(book_id, patron_id, rental_date, due_date, return_date, fees_charged, fees_paid, notes)
VALUES
    (1, 1, '2025-07-01 10:00:00', '2025-07-15 10:00:00', NULL, 0.00, 0.00, 'First rental'),
    (2, 2, '2025-07-02 11:00:00', '2025-07-16 11:00:00', NULL, 0.00, 0.00, 'Second rental'),
    (3, 3, '2025-07-03 12:00:00', '2025-07-17 12:00:00', NULL, 0.00, 0.00, 'Third rental with fee');
