SELECT t1.col1
FROM table1 AS t1
FAKEJOIN othertable AS ot
    ON t1.id = ot.t1_id;
