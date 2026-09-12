CREATE TABLE library_identity.employee
(
    employee_id INT NOT NULL IDENTITY(1,1),
    first_name NVARCHAR(50) NOT NULL,
    last_name NVARCHAR(50) NOT NULL,
    email NVARCHAR(100) NOT NULL,
    phone_number NVARCHAR(20) NOT NULL,
    address NVARCHAR(255) NOT NULL,
    city NVARCHAR(100) NOT NULL,
    state CHAR(2) NOT NULL,
    zip_code NVARCHAR(10) NOT NULL,
    hire_date DATE NOT NULL,
    job_title NVARCHAR(50) NOT NULL,
    salary DECIMAL(10,2) NOT NULL DEFAULT 0.00,
    is_active BIT NOT NULL DEFAULT 1,
    is_volunteer BIT NOT NULL DEFAULT 0,
    date_of_birth DATE NOT NULL,
    CONSTRAINT pk_employee PRIMARY KEY (employee_id),
    CONSTRAINT uc_employee UNIQUE (email)
);
