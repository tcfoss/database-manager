INSERT INTO `item_status` 
(
    `item_status_code`, 
    `item_status_name`, 
    `item_status_description`
) 
VALUES
('AV', 'Available', 'Item is available for loan'),
('LO', 'Loaned Out', 'Item is currently loaned out to a patron'),
('RS', 'Reserved', 'Item is reserved for a patron'),
('MT', 'Maintenance', 'Item is under maintenance or repair'),
('DO', 'Disposed', 'Item has been removed from the collection');
