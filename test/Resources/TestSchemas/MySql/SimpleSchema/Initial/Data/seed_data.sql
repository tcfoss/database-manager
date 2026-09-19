INSERT INTO samples
(unchanged, inferred, changed_collation, changed_charset)
VALUES
('Alpha', 'Alpha description', 'Alpha', 'AL'),
('Beta', 'Beta description', 'Beta', 'BE'),
('Gamma', NULL, 'Gamma', 'GA');

INSERT INTO widgets
(name)
VALUES
('Widget One'),
('Widget Two'),
('Widget Three');
