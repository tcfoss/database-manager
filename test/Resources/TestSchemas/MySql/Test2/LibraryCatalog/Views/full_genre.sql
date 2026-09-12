CREATE
    DEFINER = 'admin'@'localhost'
    SQL SECURITY DEFINER
VIEW `full_genre`
AS
    WITH RECURSIVE `my_cte` (`genre_id`, `genre`, `depth`)
    AS
    (
        SELECT
            `genre_id` AS `genre_id`,
            CAST(`short_name` AS CHAR(1000)) AS `genre`,
            0 AS `depth`
        FROM `genre`
        WHERE `parent_id` IS NULL
        UNION ALL
        SELECT
            `g`.`genre_id` AS `genre_id`,
            CONCAT(`my_cte`.`genre`, ' - ', `g`.`short_name`) AS `genre`,
            `my_cte`.`depth` + 1 AS `depth`
        FROM `genre` AS `g`
        INNER JOIN `my_cte`
            ON `g`.`parent_id` = `my_cte`.`genre_id`
        WHERE `my_cte`.`depth` < 10
    )
    SELECT
        `my_cte`.`genre_id`,
        `my_cte`.`genre`
    FROM `my_cte`;
