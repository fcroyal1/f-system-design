-- Reference data: spend categories, GL accounts, cost centers, delegation of authority tiers.

MERGE dbo.spend_category AS t
USING (VALUES
    ('MED-SUPPLIES', N'Medical supplies', 0),
    ('OFFICE',       N'Office supplies',  0),
    ('UTILITIES',    N'Utilities',        1),
    ('RENT',         N'Rent',             1),
    ('TAXES',        N'Taxes',            1),
    ('INSURANCE',    N'Insurance',        1),
    ('LEGAL',        N'Legal fees',       1),
    ('REFUNDS',      N'Refunds',          1)
) AS s (code, name, is_po_exempt)
ON t.code = s.code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (code, name, is_po_exempt) VALUES (s.code, s.name, s.is_po_exempt);

MERGE dbo.gl_account AS t
USING (VALUES
    ('6110', N'Medical supplies', 'Expense'),
    ('6200', N'Office supplies',  'Expense')
) AS s (number, name, type)
ON t.number = s.number
WHEN NOT MATCHED BY TARGET THEN
    INSERT (number, name, type) VALUES (s.number, s.name, s.type);

MERGE dbo.cost_center AS t
USING (VALUES
    ('CC-200', N'Clinic operations', N'Dana Ruiz')
) AS s (code, name, owner_name)
ON t.code = s.code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (code, name, owner_name) VALUES (s.code, s.name, s.owner_name);

MERGE dbo.doa_tier AS t
USING (VALUES
    (1,         5000.00, N'Cost center owner'),
    (2,        25000.00, N'Director'),
    (3,       100000.00, N'VP Finance'),
    (4, 999999999999.99, N'CFO')
) AS s (id, max_amount, approver_role)
ON t.id = s.id
WHEN NOT MATCHED BY TARGET THEN
    INSERT (id, max_amount, approver_role) VALUES (s.id, s.max_amount, s.approver_role);
