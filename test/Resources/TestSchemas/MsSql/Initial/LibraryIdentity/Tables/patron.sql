CREATE TABLE library_identity.patron
(
    patron_id INT NOT NULL IDENTITY(1,1),
    card_number NVARCHAR(20) NOT NULL,
    first_name NVARCHAR(50) NOT NULL,
    last_name NVARCHAR(50) NOT NULL,
    email NVARCHAR(100) NULL,
    phone_number NVARCHAR(20) NOT NULL,
    address NVARCHAR(255) NOT NULL,
    city NVARCHAR(100) NOT NULL,
    state CHAR(2) NOT NULL,
    zip_code NVARCHAR(10) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    registration_date DATE NOT NULL,
    CONSTRAINT pk_patron PRIMARY KEY (patron_id),
    CONSTRAINT uc_patron UNIQUE (card_number)
);
