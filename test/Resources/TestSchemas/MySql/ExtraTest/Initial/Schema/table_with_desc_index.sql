CREATE TABLE table_with_desc_index (
    id INT NOT NULL,
    val VARCHAR(10) NOT NULL,
    PRIMARY KEY (id desc),
    KEY idx_with_desc_index_val (val (5) desc)
);
