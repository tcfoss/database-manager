UPDATE table1 AS t1
INNER JOIN table2 AS t2
    ON t1.id = t2.t1_id
SET
    t1.col2 = 'new_value',
    t2.col3 = t2.col3 + 10
WHERE t1.col1 = 'condition_value';
/* 
 * BLOCK COMMENT
 */