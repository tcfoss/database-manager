SELECT
    t1.col1, -- line comment
    t1.col2,
    t2.col1 AS t2_col1
FROM table1 AS t1
INNER JOIN table2 AS t2
    ON t1.id = t2.t1_id
WHERE t1.col3 = 'value'
ORDER BY t1.col1 DESC;
