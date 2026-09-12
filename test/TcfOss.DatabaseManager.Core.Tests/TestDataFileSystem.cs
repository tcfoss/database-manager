namespace TcfOss.DatabaseManager.Core.Tests;

public static class TestDataFileSystem
{
    public static readonly List<string> Files =
    [
        "/fake/project/root/schema_1/file1.sql",
        "/fake/project/root/schema_1/file2.sql",
        "/fake/project/root/schema_1/subdir/file3.sql",
        "/fake/project/root/schema_1/subdir/mybadfile.sql",
        "/fake/project/root/schema_1/subdir/another/subdir/other_badfile.sql",
        "/fake/project/root/subdir/schema_2/subdir/file4.sql",
        "/fake/project/root/subdir/schema_2/file5.sql",
        "/fake/project/root/other_file.txt"
    ];

    public static string GetText(string filePath)
    {
        return filePath switch
        {
            "/fake/project/root/schema_1/file1.sql" => """
                CREATE TABLE table1 (
                    id INT,
                    val VARCHAR(100) NULL,
                    PRIMARY KEY (id),
                    CONSTRAINT uc_table1 UNIQUE (val)
                );
                """,

            "/fake/project/root/schema_1/file2.sql" => """
                CREATE TABLE table2 (
                    id INT,
                    val VARCHAR(100) NOT NULL,
                    PRIMARY KEY (id)
                );
                CREATE TABLE table3 (
                    id INT,
                    parent_id INT,
                    val VARCHAR(100) NOT NULL,
                    PRIMARY KEY (id),
                    FOREIGN KEY (parent_id) REFERENCES table1(id)
                );
                """,

            "/fake/project/root/schema_1/subdir/file3.sql" => """
                CREATE
                    ALGORITHM = UNDEFINED
                    DEFINER = 'root'@'localhost'
                    SQL SECURITY INVOKER
                VIEW view13
                AS SELECT
                    t1.id AS id,
                    t1.val AS val,
                    t3.val AS parent_val
                FROM table1 t1
                LEFT JOIN table3 AS t3
                    ON t1.id = t3.parent_id;
                """,

            "/fake/project/root/schema_1/subdir/mybadfile.sql" => "DROP TABLE mybadfile;",
            "/fake/project/root/subdir/schema_2/subdir/file4.sql" => """
                CREATE TABLE table4 (
                    id INT,
                    val VARCHAR(100) NOT NULL,
                    PRIMARY KEY (id)
                );
            """,

            "/fake/project/root/subdir/schema_2/file5.sql" => """
                DELIMITER //

                CREATE
                    DEFINER = 'root'@'localhost'
                PROCEDURE my_proc (IN param1 INT, OUT param2 VARCHAR(100))
                BEGIN
                    SELECT val INTO param2 FROM table4 WHERE id = param1;
                END//

                CREATE
                    DEFINER = 'root'@'localhost'
                FUNCTION my_func1 (param1 INT) RETURNS VARCHAR(100)
                BEGIN
                    DECLARE result VARCHAR(100);
                    SELECT val INTO result FROM table4 WHERE id = param1;
                    RETURN result;
                END//

                CREATE
                    DEFINER = 'root'@'localhost'
                FUNCTION my_func2 (param1 INT) RETURNS VARCHAR(100)
                RETURN SELECT val FROM table4 WHERE id = param1//

                DELIMITER ;
            """,

            _ => string.Empty
        };
    }
}
