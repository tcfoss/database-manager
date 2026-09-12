SELECT *
FROM mytable AS mt
FAKEJOIN othertable AS ot
ON mt.id = ot.ref_id;
