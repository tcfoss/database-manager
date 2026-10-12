CREATE TABLE index_with_comment (
    id INT NOT NULL,
    INDEX idx_index_with_comment (id) COMMENT 'This is an index comment'
) COMMENT 'This is a table comment';