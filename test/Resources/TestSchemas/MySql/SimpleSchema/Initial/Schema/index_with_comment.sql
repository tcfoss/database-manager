CREATE TABLE index_with_comment (
    id INT NOT NULL,
    INDEX idx_index_with_comment (id) COMMENT 'This is an index comment',
    CONSTRAINT uc_index_with_comment UNIQUE (id) COMMENT 'This is a unique constraint comment'
) COMMENT 'This is a table comment';