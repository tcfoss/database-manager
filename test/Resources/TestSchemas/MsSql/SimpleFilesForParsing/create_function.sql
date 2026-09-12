CREATE FUNCTION simple_function (@param1 INT)
RETURNS INT
AS
BEGIN
    RETURN @param1 * 2;
END;
