CREATE TABLE library_catalog.accidentally_added_table
(
    accidentally_added_table_id INT NOT NULL IDENTITY(1,1),
    value NVARCHAR(255) NOT NULL,
    CONSTRAINT pk_accidentally_added_table PRIMARY KEY (accidentally_added_table_id)
);
