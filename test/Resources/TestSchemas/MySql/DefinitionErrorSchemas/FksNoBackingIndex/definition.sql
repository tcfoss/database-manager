CREATE TABLE table1
(
    id INT NOT NULL,
    PRIMARY KEY (id)
);

CREATE TABLE table2
(
    id INT NOT NULL,
    table1_id INT,
    CONSTRAINT fk_table1 FOREIGN KEY (table1_id) REFERENCES table1(id)
);
