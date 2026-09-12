using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using TcfOss.DatabaseManager.Core.DatabaseComms;
using TcfOss.DatabaseManager.Core.IO;

namespace TcfOss.DatabaseManager.MySql.IntegrationTests.DefinitionMapping;

public abstract class DifferTests<TBuilderEntity, TContainerEntity, TFixture> : IClassFixture<TFixture>
    where TBuilderEntity : IContainerBuilder<TBuilderEntity, TContainerEntity, IContainerConfiguration>, new()
    where TContainerEntity : IContainer, IDatabaseContainer
    where TFixture : DifferFixture<TBuilderEntity, TContainerEntity>
{
    protected TFixture Fixture { get; init; } = null!;

    protected virtual string CharacterSet => "utf8mb4";
    protected virtual string Collation => "utf8mb4_0900_ai_ci";
    protected virtual string OnDeleteRestrict => " ON DELETE RESTRICT";
    protected virtual string IntWidth => "";
    protected virtual string FunctionParameterDirection => "";
    protected virtual string CteDeclName => "`my_cte`";

    [Fact]
    public void Test_No_Changes()
    {
        // Arrange
        Assert.Empty(Fixture.TestSets[0].ActualChanges);
    }

    [Fact]
    public void Test_Set_1()
    {
        var actualStatements = Fixture.TestSets[1]
            .ActualChanges
            .OrderBy(c => c.Weight)
            .ThenBy(c => c.Statement.ToSql())
            .Select(c => c.Statement.ToSql())
            .ToArray();

        var expectedStatements = new[]
        {
            "DROP TRIGGER `library_activity`.`tr_active_rental_after_insert`",
            "DROP TRIGGER `library_activity`.`tr_active_rental_after_update`",
            "DROP VIEW `library_activity`.`available_books`",
            "ALTER TABLE `library_activity`.`active_rental` DROP FOREIGN KEY `fk_active_rental_book`",
            "ALTER TABLE `library_activity`.`rental_log` DROP FOREIGN KEY `fk_rental_log_book`",
            "ALTER TABLE `library_activity`.`active_rental` DROP CONSTRAINT `uc_active_rental_book_id`",
            "ALTER TABLE `library_activity`.`rental_log` DROP KEY `idx_rental_log_book_id`",
            "DROP TABLE `library_catalog`.`accidentally_added_table`",
            "ALTER TABLE `library_activity`.`active_rental` ADD COLUMN `item_id` INT NOT NULL AFTER `rental_id`, DROP COLUMN `book_id`",
            "ALTER TABLE `library_activity`.`rental_log` ADD COLUMN `item_id` INT NOT NULL AFTER `rental_id`, DROP COLUMN `book_id`",
            "ALTER TABLE `library_catalog`.`book` ADD COLUMN `subtitle_gen` VARCHAR(100) GENERATED ALWAYS AS (IFNULL(`subtitle`, '')) VIRTUAL AFTER `full_title`, ADD COLUMN `description_gen` TEXT GENERATED ALWAYS AS (IFNULL(`description`, '')) VIRTUAL AFTER `subtitle_gen`, DROP COLUMN `acquisition_cost`, DROP COLUMN `acquisition_date`, DROP COLUMN `isbn`",
            $"CREATE TABLE `library_catalog`.`acquisition` (`acquisition_id` INT NOT NULL AUTO_INCREMENT, `acquisition_date` DATE NOT NULL, `cost` DECIMAL(10,2) NOT NULL DEFAULT 0.00, `fund_code` VARCHAR(10) NULL DEFAULT NULL, `supplier` VARCHAR(100) NULL DEFAULT NULL, `was_donated` TINYINT(1) NOT NULL DEFAULT 0, PRIMARY KEY (`acquisition_id`)) ENGINE InnoDB CHARACTER SET {CharacterSet} COLLATE {Collation}",
            $"CREATE TABLE `library_catalog`.`identifier_type` (`identifier_type_code` CHAR(2) NOT NULL, `identifier_name` VARCHAR(10) NOT NULL, `identifier_description` VARCHAR(100) NOT NULL, PRIMARY KEY (`identifier_type_code`), CONSTRAINT `uc_identifier_type` UNIQUE KEY (`identifier_name`)) ENGINE InnoDB CHARACTER SET {CharacterSet} COLLATE {Collation}",
            $"CREATE TABLE `library_catalog`.`identifier` (`identifier_id` INT NOT NULL AUTO_INCREMENT, `book_id` INT NOT NULL, `identifier_type_code` CHAR(2) NOT NULL, `identifier_value` VARCHAR(50) NOT NULL, PRIMARY KEY (`identifier_id`), KEY `idx_identifier_book` (`book_id`), KEY `idx_identifier_identifier_type_code` (`identifier_type_code`)) ENGINE InnoDB CHARACTER SET {CharacterSet} COLLATE {Collation}",
            $"CREATE TABLE `library_catalog`.`item_status` (`item_status_code` CHAR(2) NOT NULL, `item_status_name` VARCHAR(50) NOT NULL, `item_status_description` VARCHAR(200) NOT NULL, PRIMARY KEY (`item_status_code`), CONSTRAINT `uc_item_status` UNIQUE KEY (`item_status_name`)) ENGINE InnoDB CHARACTER SET {CharacterSet} COLLATE {Collation}",
            $"CREATE TABLE `library_catalog`.`item` (`item_id` INT NOT NULL AUTO_INCREMENT, `book_id` INT NOT NULL, `acquisition_id` INT NOT NULL, `item_status_code` CHAR(2) NOT NULL DEFAULT 'AV', `barcode` VARCHAR(50) NOT NULL, `location_code` VARCHAR(10) NOT NULL, `shelf_code` VARCHAR(10) NULL DEFAULT NULL, PRIMARY KEY (`item_id`), KEY `idx_item_book` (`book_id`), KEY `idx_item_acquisition` (`acquisition_id`), KEY `idx_item_item_status_code` (`item_status_code`)) ENGINE InnoDB CHARACTER SET {CharacterSet} COLLATE {Collation}",
            "ALTER TABLE `library_activity`.`active_rental` ADD CONSTRAINT `uc_active_rental_item_id` UNIQUE KEY (`item_id`, `really_active`)",
            "ALTER TABLE `library_activity`.`rental_log` ADD KEY `idx_rental_log_item_id` (`item_id`)",
            "ALTER TABLE `library_catalog`.`book` ADD CONSTRAINT `uc_book_expanded_title` UNIQUE KEY (`title`, `subtitle_gen`, `description_gen`(200))",
            $"ALTER TABLE `library_activity`.`active_rental` ADD CONSTRAINT `fk_active_rental_item` FOREIGN KEY (`item_id`) REFERENCES `library_catalog`.`item` (`item_id`){OnDeleteRestrict}",
            $"ALTER TABLE `library_activity`.`rental_log` ADD CONSTRAINT `fk_rental_log_item` FOREIGN KEY (`item_id`) REFERENCES `library_catalog`.`item` (`item_id`){OnDeleteRestrict}",
            "ALTER TABLE `library_catalog`.`identifier` ADD CONSTRAINT `fk_identifier_book` FOREIGN KEY (`book_id`) REFERENCES `library_catalog`.`book` (`book_id`) ON DELETE CASCADE ON UPDATE CASCADE",
            $"ALTER TABLE `library_catalog`.`identifier` ADD CONSTRAINT `fk_identifier_identifier_type_code` FOREIGN KEY (`identifier_type_code`) REFERENCES `library_catalog`.`identifier_type` (`identifier_type_code`){OnDeleteRestrict} ON UPDATE CASCADE",
            $"ALTER TABLE `library_catalog`.`item` ADD CONSTRAINT `fk_item_acquisition` FOREIGN KEY (`acquisition_id`) REFERENCES `library_catalog`.`acquisition` (`acquisition_id`){OnDeleteRestrict} ON UPDATE CASCADE",
            $"ALTER TABLE `library_catalog`.`item` ADD CONSTRAINT `fk_item_book` FOREIGN KEY (`book_id`) REFERENCES `library_catalog`.`book` (`book_id`){OnDeleteRestrict} ON UPDATE CASCADE",
            $"ALTER TABLE `library_catalog`.`item` ADD CONSTRAINT `fk_item_status` FOREIGN KEY (`item_status_code`) REFERENCES `library_catalog`.`item_status` (`item_status_code`){OnDeleteRestrict} ON UPDATE CASCADE",
            "CREATE ALGORITHM = UNDEFINED DEFINER = `admin`@`localhost` SQL SECURITY INVOKER VIEW `library_activity`.`available_books` AS SELECT `bc`.`book_id` AS `book_id`, `bc`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `bc` INNER JOIN `library_catalog`.`item` AS `itm` ON `bc`.`book_id` = `itm`.`book_id` WHERE NOT EXISTS (SELECT 1 FROM `library_activity`.`active_rental` AS `ar` WHERE `ar`.`item_id` = `itm`.`item_id` AND `ar`.`really_active` = 'Y')",
            $@"CREATE DEFINER = `admin`@`localhost` PROCEDURE `library_activity`.`end_loan` (IN `p_rental_id` INT{IntWidth} SIGNED, IN `p_barcode` VARCHAR(50) CHARACTER SET {CharacterSet} COLLATE {Collation}, IN `p_return_date` TIMESTAMP, IN `p_late_fee` DECIMAL(10,2) SIGNED, IN `p_fees_paid` DECIMAL(10,2) SIGNED, IN `p_notes` TEXT CHARACTER SET {CharacterSet} COLLATE {Collation}, OUT `p_active_rental_concluded` TINYINT(1) SIGNED) LANGUAGE SQL NOT DETERMINISTIC CONTAINS SQL SQL SECURITY DEFINER BEGIN DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; START TRANSACTION; IF p_rental_id IS NULL THEN SELECT ar.rental_id INTO p_rental_id FROM active_rental AS ar INNER JOIN library_catalog.item AS itm ON ar.item_id = itm.item_id WHERE itm.barcode = p_barcode AND ar.really_active = 'Y'; IF p_rental_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Active rental not found for given item.'; END IF; END IF; IF p_late_fee IS NULL THEN SET p_late_fee = 0.00; END IF; IF p_fees_paid IS NULL THEN SET p_fees_paid = 0.00; END IF; IF p_late_fee > 0.00 THEN UPDATE active_rental AS ar SET ar.fees_charged = p_late_fee, ar.fees_paid = ar.fees_paid + p_fees_paid, ar.return_date = p_return_date, ar.notes = CONCAT(IFNULL(ar.notes, ''), '\n', IFNULL(p_notes, '')) WHERE rental_id = p_rental_id; ELSE UPDATE active_rental AS ar SET ar.fees_paid = ar.fees_paid + p_fees_paid, ar.return_date = p_return_date, ar.notes = CONCAT(IFNULL(ar.notes, ''), '\n', IFNULL(p_notes, '')) WHERE rental_id = p_rental_id; END IF; UPDATE library_catalog.item AS itm INNER JOIN active_rental AS ar ON itm.item_id = ar.item_id SET itm.item_status_code = 'AV' WHERE ar.rental_id = p_rental_id; IF (SELECT ar.fees_charged - ar.fees_paid FROM active_rental AS ar WHERE ar.rental_id = p_rental_id) > 0.00 THEN SET p_active_rental_concluded = FALSE; ELSE SET p_active_rental_concluded = TRUE; DELETE FROM library_activity.active_rental WHERE rental_id = p_rental_id; END IF; COMMIT; END",
            $"CREATE DEFINER = `admin`@`localhost` PROCEDURE `library_activity`.`start_loan` (IN `p_item_id` INT{IntWidth} SIGNED, IN `p_barcode` VARCHAR(50) CHARACTER SET {CharacterSet} COLLATE {Collation}, IN `p_patron_id` INT{IntWidth} SIGNED, IN `p_due_date` TIMESTAMP, IN `p_notes` TEXT CHARACTER SET {CharacterSet} COLLATE {Collation}) LANGUAGE SQL NOT DETERMINISTIC CONTAINS SQL SQL SECURITY DEFINER BEGIN DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END; START TRANSACTION; IF p_item_id IS NULL THEN SELECT itm.item_id INTO p_item_id FROM library_catalog.item AS itm WHERE itm.barcode = p_barcode; END IF; IF p_item_id IS NULL THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Item not found for given barcode.'; END IF; INSERT INTO active_rental (item_id, patron_id, rental_date, due_date, notes) VALUES (p_item_id, p_patron_id, CURRENT_TIMESTAMP(), p_due_date, p_notes); UPDATE library_catalog.item SET item_status_code = 'LO' WHERE item_id = p_item_id; COMMIT; END",
            "CREATE DEFINER = `admin`@`localhost` TRIGGER `library_activity`.`tr_active_rental_after_insert` AFTER INSERT ON `library_activity`.`active_rental` FOR EACH ROW BEGIN INSERT INTO rental_log (rental_id, item_id, patron_id, rental_date, due_date, return_date, fees_charged, fees_paid, notes, log_date) VALUES (NEW.rental_id, NEW.item_id, NEW.patron_id, NEW.rental_date, NEW.due_date, NEW.return_date, NEW.fees_charged, NEW.fees_paid, NEW.notes, CURRENT_TIMESTAMP()); END",
            "CREATE DEFINER = `admin`@`localhost` TRIGGER `library_activity`.`tr_active_rental_after_update` AFTER UPDATE ON `library_activity`.`active_rental` FOR EACH ROW BEGIN INSERT INTO rental_log (rental_id, item_id, patron_id, rental_date, due_date, return_date, fees_charged, fees_paid, notes, log_date) VALUES (NEW.rental_id, NEW.item_id, NEW.patron_id, NEW.rental_date, NEW.due_date, NEW.return_date, NEW.fees_charged, NEW.fees_paid, NEW.notes, CURRENT_TIMESTAMP()); IF NEW.return_date IS NOT NULL AND NEW.fees_charged <= NEW.fees_paid THEN DELETE FROM active_rental WHERE rental_id = NEW.rental_id; END IF; END"
        };

        Assert.Equal(expectedStatements.Length, actualStatements.Length);

        for (var i = 0; i < expectedStatements.Length; i++)
        {
            var expectedSql = expectedStatements[i];
            var actualSql = actualStatements[i];
            Assert.Equal(expectedSql, actualSql);
        }
    }

    [Fact]
    public void Test_Set_2()
    {
        var actualStatements = Fixture.TestSets[2]
            .ActualChanges
            .OrderBy(c => c.Weight)
            .ThenBy(c => c.Statement.ToSql())
            .Select(c => c.Statement.ToSql())
            .ToArray();

        var expectedStatements = new[]
        {
            "DROP EVENT `library_identity`.`deactivate_stale_users`",
            "DROP FUNCTION `library_activity`.`get_late_charge`",
            "DROP TRIGGER `library_activity`.`tr_active_rental_after_insert`",
            "DROP TRIGGER `library_activity`.`tr_active_rental_after_update`",
            "DROP TRIGGER `library_activity`.`tr_active_rental_before_delete`",
            "DROP VIEW `library_activity`.`available_books`",
            "ALTER TABLE `library_catalog`.`accidentally_added_table` RENAME TO `library_catalog`.`differently_named_table`",
            "ALTER TABLE `library_catalog`.`contributor` RENAME COLUMN `disambiguation` TO `details`",
            "ALTER TABLE `library_activity`.`active_rental` RENAME TO `library_activity`.`loan`",
            "ALTER TABLE `library_activity`.`loan` RENAME COLUMN `rental_id` TO `loan_id`",
            "ALTER TABLE `library_activity`.`rental_log` RENAME COLUMN `rental_id` TO `loan_id`",
            $"INSERT INTO `library_activity`.`{StoredMetadataConstants.TableName}` (`entry_key`, `entry_type`) VALUES ('7a8b9c0d-e1f2-3g4h-5i6j-7k8l9m0n1o2p', '{StoredMetadataConstants.Refactor}'), ('2b3c4d5e-6f7g-8h9i-0j1k-l2m3n4o5p6q7', '{StoredMetadataConstants.Refactor}'), ('3c4d5e6f-7g8h-9i0j-1k2l-m3n4o5p6q7r8', '{StoredMetadataConstants.Refactor}')",
            $"INSERT INTO `library_catalog`.`{StoredMetadataConstants.TableName}` (`entry_key`, `entry_type`) VALUES ('f3e7e2b8-4e3e-4c8a-8b2e-6b2e9e3b7c1d', '{StoredMetadataConstants.Refactor}'), ('a1b2c3d4-5e6f-7g8h-9i0j-k1l2m3n4o5p6', '{StoredMetadataConstants.Refactor}')",
            "ALTER TABLE `library_activity`.`loan` DROP FOREIGN KEY `fk_active_rental_book`",
            "ALTER TABLE `library_activity`.`loan` DROP FOREIGN KEY `fk_active_rental_patron`",
            "ALTER TABLE `library_activity`.`loan` DROP CONSTRAINT `uc_active_rental_book_id`",
            "ALTER TABLE `library_activity`.`loan` DROP KEY `idx_active_rental_patron_id`",
            "ALTER TABLE `library_activity`.`rental_log` DROP KEY `idx_rental_log_rental_id`",
            "ALTER TABLE `library_activity`.`rental_log` MODIFY COLUMN `loan_id` INT NOT NULL COMMENT 'Fake foreign key to allow loan to be deleted'",
            "ALTER TABLE `library_activity`.`loan` ADD CONSTRAINT `uc_loan_book_id` UNIQUE KEY (`book_id`, `really_active`)",
            "ALTER TABLE `library_activity`.`loan` ADD KEY `idx_loan_patron_id` (`patron_id`)",
            "ALTER TABLE `library_activity`.`rental_log` ADD KEY `idx_rental_log_loan_id` (`loan_id`)",
            $"ALTER TABLE `library_activity`.`loan` ADD CONSTRAINT `fk_loan_book` FOREIGN KEY (`book_id`) REFERENCES `library_catalog`.`book` (`book_id`){OnDeleteRestrict}",
            $"ALTER TABLE `library_activity`.`loan` ADD CONSTRAINT `fk_loan_patron` FOREIGN KEY (`patron_id`) REFERENCES `library_identity`.`patron` (`patron_id`){OnDeleteRestrict}",
            "CREATE ALGORITHM = UNDEFINED DEFINER = `admin`@`localhost` SQL SECURITY INVOKER VIEW `library_activity`.`available_books` AS SELECT `bc`.`book_id` AS `book_id`, `bc`.`full_title` AS `full_title` FROM `library_catalog`.`book` AS `bc` WHERE NOT EXISTS (SELECT 1 FROM `library_activity`.`loan` AS `l` WHERE `l`.`book_id` = `bc`.`book_id` AND `l`.`really_active` = 'Y')",
            $"CREATE DEFINER = `admin`@`localhost` FUNCTION `library_activity`.`get_late_charge` ({FunctionParameterDirection}`loan_id` INT{IntWidth} SIGNED, {FunctionParameterDirection}`lateness_rate` DECIMAL(10,2) SIGNED) RETURNS DECIMAL(10,2) LANGUAGE SQL NOT DETERMINISTIC READS SQL DATA SQL SECURITY DEFINER BEGIN DECLARE late_charge DECIMAL(10,2) DEFAULT 0.00; IF CURRENT_DATE() <= (SELECT due_date FROM loan AS l WHERE l.loan_id = loan_id) THEN RETURN 0.00; END IF; SELECT (DATEDIFF(CURRENT_DATE(), l.due_date) * lateness_rate) INTO late_charge FROM loan AS l WHERE l.loan_id = loan_id; RETURN IFNULL(late_charge, 0); END",
            $"CREATE ALGORITHM = UNDEFINED DEFINER = `admin`@`localhost` SQL SECURITY DEFINER VIEW `library_catalog`.`full_genre` AS WITH RECURSIVE {CteDeclName} (`genre_id`, `genre`, `depth`) AS (SELECT `library_catalog`.`genre`.`genre_id` AS `genre_id`, CAST(`library_catalog`.`genre`.`short_name` AS CHAR(1000) CHARACTER SET utf8mb4) AS `genre`, 0 AS `depth` FROM `library_catalog`.`genre` WHERE `library_catalog`.`genre`.`parent_id` IS NULL UNION ALL SELECT `g`.`genre_id` AS `genre_id`, CONCAT(`my_cte`.`genre`, ' - ', `g`.`short_name`) AS `genre`, `my_cte`.`depth` + 1 AS `depth` FROM `library_catalog`.`genre` AS `g` INNER JOIN `my_cte` ON `g`.`parent_id` = `my_cte`.`genre_id` WHERE `my_cte`.`depth` < 10) SELECT `my_cte`.`genre_id` AS `genre_id`, `my_cte`.`genre` AS `genre` FROM `my_cte`",
            "CREATE DEFINER = `admin`@`localhost` TRIGGER `library_activity`.`tr_loan_after_insert` AFTER INSERT ON `library_activity`.`loan` FOR EACH ROW BEGIN INSERT INTO rental_log (loan_id, book_id, patron_id, rental_date, due_date, return_date, fees_charged, fees_paid, notes, log_date) VALUES (NEW.loan_id, NEW.book_id, NEW.patron_id, NEW.rental_date, NEW.due_date, NEW.return_date, NEW.fees_charged, NEW.fees_paid, NEW.notes, CURRENT_TIMESTAMP()); END",
            "CREATE DEFINER = `admin`@`localhost` TRIGGER `library_activity`.`tr_loan_after_update` AFTER UPDATE ON `library_activity`.`loan` FOR EACH ROW BEGIN INSERT INTO rental_log (loan_id, book_id, patron_id, rental_date, due_date, return_date, fees_charged, fees_paid, notes, log_date) VALUES (NEW.loan_id, NEW.book_id, NEW.patron_id, NEW.rental_date, NEW.due_date, NEW.return_date, NEW.fees_charged, NEW.fees_paid, NEW.notes, CURRENT_TIMESTAMP()); IF NEW.return_date IS NOT NULL AND NEW.fees_charged <= NEW.fees_paid THEN DELETE FROM loan WHERE loan_id = NEW.loan_id; END IF; END",
            "CREATE DEFINER = `admin`@`localhost` TRIGGER `library_activity`.`tr_loan_before_delete` BEFORE DELETE ON `library_activity`.`loan` FOR EACH ROW BEGIN IF OLD.fees_charged > OLD.fees_paid THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cannot delete active rental with outstanding fees.'; END IF; END",
            "CREATE DEFINER = `admin`@`localhost` EVENT `library_identity`.`deactivate_stale_users` ON SCHEDULE EVERY 1 DAY STARTS '2024-01-01 00:00:00' ON COMPLETION NOT PRESERVE ENABLE DO BEGIN UPDATE site_user SET is_active = FALSE WHERE is_active = TRUE AND last_login < NOW() - INTERVAL 6 MONTH; END",
        };

        Assert.Equal(expectedStatements.Length, actualStatements.Length);

        for (var i = 0; i < expectedStatements.Length; i++)
        {
            var expectedSql = expectedStatements[i];
            var actualSql = actualStatements[i];
            Assert.Equal(expectedSql, actualSql);
        }
    }

    // private void WriteExpectedActual(string testSetName, IEnumerable<string> expectedDefinition, IEnumerable<string> actualDefinition)
    // {
    //     var rootDir = "/tmp/TestRunErrors";
    //     Directory.CreateDirectory(rootDir);
    //     var outputDir = Path.Combine(rootDir, "DifferTestOutputs");
    //     Directory.CreateDirectory(outputDir);

    //     var expectedPath = Path.Combine(outputDir, $"{testSetName}_Expected.sql");
    //     File.WriteAllLines(expectedPath, expectedDefinition);

    //     var actualPath = Path.Combine(outputDir, $"{testSetName}_Actual.sql");
    //     File.WriteAllLines(actualPath, actualDefinition);
    // }
}
