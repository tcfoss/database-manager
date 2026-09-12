CREATE TABLE library_identity.site_user
(
    user_id INT NOT NULL IDENTITY(1,1),
    patron_id INT NULL,
    employee_id INT NULL,
    password_hash VARCHAR(100) COLLATE Latin1_General_BIN NOT NULL,
    email NVARCHAR(100) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    registration_date DATETIME2 NOT NULL DEFAULT GETDATE(),
    last_login DATETIME2 NULL,
    CONSTRAINT pk_site_user PRIMARY KEY (user_id),
    CONSTRAINT uc_site_user UNIQUE (email),
    CONSTRAINT fk_site_user_patron FOREIGN KEY (patron_id) REFERENCES library_identity.patron (patron_id),
    CONSTRAINT fk_site_user_employee FOREIGN KEY (employee_id) REFERENCES library_identity.employee (employee_id),
    CONSTRAINT chk_site_user_one_of_patron_or_employee CHECK (patron_id IS NOT NULL OR employee_id IS NOT NULL)
);

GO

CREATE INDEX idx_site_user_patron_id ON library_identity.site_user (patron_id);
CREATE INDEX idx_site_user_employee_id ON library_identity.site_user (employee_id);
