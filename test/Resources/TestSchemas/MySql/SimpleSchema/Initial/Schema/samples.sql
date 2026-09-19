CREATE TABLE samples
(
    unchanged VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
    inferred TEXT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL,
    changed_collation VARCHAR(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL,
    changed_charset CHAR(10) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL
) DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
