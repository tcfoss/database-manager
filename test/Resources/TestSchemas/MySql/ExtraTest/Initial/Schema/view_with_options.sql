CREATE
    ALGORITHM = MERGE
VIEW view_with_options
AS
    SELECT tnc.id,
           tnc.val
    FROM table_no_constraints AS tnc
    WITH LOCAL CHECK OPTION;