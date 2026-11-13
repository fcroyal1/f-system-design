-- Demo data: the guide's worked example (Bluepeak PO 4500123 / GR 5000987 / INV-77812, blocked on
-- quantity) and a fully matched Northline PO 4500118 / GR 5000980 / NL-2231 for the dashboard.
--
-- Parents match on their fixed id OR their natural key, so a row the user re-created is not
-- duplicated. Children join to their parent's fixed id, so they are skipped if that parent is not
-- the seeded row. The match_check rows must equal MatchEngine's output for this data (Task 5's
-- drift test re-matches the seeded invoices and compares).

DECLARE @bluepeak  uniqueidentifier = 'a1b2c3d4-0001-4000-8000-000000010442';
DECLARE @northline uniqueidentifier = 'a1b2c3d4-0001-4000-8000-000000010518';
DECLARE @po123     uniqueidentifier = 'a1b2c3d4-0002-4000-8000-000004500123';
DECLARE @po118     uniqueidentifier = 'a1b2c3d4-0002-4000-8000-000004500118';
DECLARE @gr987     uniqueidentifier = 'a1b2c3d4-0003-4000-8000-000005000987';
DECLARE @gr980     uniqueidentifier = 'a1b2c3d4-0003-4000-8000-000005000980';
DECLARE @inv77812  uniqueidentifier = 'a1b2c3d4-0004-4000-8000-000000077812';
DECLARE @invNl2231 uniqueidentifier = 'a1b2c3d4-0004-4000-8000-000000002231';

MERGE dbo.supplier AS t
USING (VALUES
    (@bluepeak,  'V-10442', N'Bluepeak Medical Supply', 'MED-SUPPLIES', 30, '4421', '6630'),
    (@northline, 'V-10518', N'Northline Office Co',     'OFFICE',       45, NULL,   NULL)
) AS s (id, code, legal_name, category_code, payment_terms_days, tax_id_last4, bank_last4)
ON t.id = s.id OR t.code = s.code
WHEN NOT MATCHED BY TARGET THEN
    INSERT (id, code, legal_name, category_code, payment_terms_days, tax_id_last4, bank_last4, is_blocked)
    VALUES (s.id, s.code, s.legal_name, s.category_code, s.payment_terms_days, s.tax_id_last4, s.bank_last4, 0);

MERGE dbo.purchase_order AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@po123, '4500123', @bluepeak,  N'Harborview Clinics – Supply room',  CAST('2026-10-01' AS date), CAST('2026-10-01T09:30:00' AS datetime2)),
        (@po118, '4500118', @northline, N'Harborview Clinics – Front office', CAST('2026-09-20' AS date), CAST('2026-09-20T09:30:00' AS datetime2))
    ) AS s (id, po_number, supplier_id, requester_name, po_date, approved_at)
    JOIN dbo.supplier sup ON sup.id = s.supplier_id
) AS s
ON t.id = s.id OR t.po_number = s.po_number
WHEN NOT MATCHED BY TARGET THEN
    INSERT (id, po_number, supplier_id, requester_name, cost_center_code, currency, po_date, status, approver_role, approved_by, approved_at)
    VALUES (s.id, s.po_number, s.supplier_id, s.requester_name, 'CC-200', 'USD', s.po_date, 'Approved', N'Cost center owner', N'Dana Ruiz', s.approved_at);

MERGE dbo.po_line AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@po123, 1, 'GLV-NIT-M',  N'Nitrile exam gloves, medium, box of 100', 'BX', CAST(100 AS decimal(18,3)), CAST(10.0000 AS decimal(18,4)), '6110'),
        (@po118, 1, 'PPR-A4-500', N'Copy paper, A4, ream of 500',             'RM', CAST(40 AS decimal(18,3)),  CAST(6.5000 AS decimal(18,4)),  '6200')
    ) AS s (po_id, line_no, item_code, description, uom, quantity, unit_price, gl_account)
    JOIN dbo.purchase_order po ON po.id = s.po_id
) AS s
ON t.po_id = s.po_id AND t.line_no = s.line_no
WHEN NOT MATCHED BY TARGET THEN
    INSERT (po_id, line_no, item_code, description, uom, quantity, unit_price, gl_account, receipt_required)
    VALUES (s.po_id, s.line_no, s.item_code, s.description, s.uom, s.quantity, s.unit_price, s.gl_account, 1);

MERGE dbo.goods_receipt AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@gr987, '5000987', @po123, CAST('2026-10-06T10:15:00' AS datetime2), N'Harborview Clinics – Supply room',  N'PS-BLU-31877'),
        (@gr980, '5000980', @po118, CAST('2026-09-25T11:00:00' AS datetime2), N'Harborview Clinics – Front office', N'PS-NOR-2207')
    ) AS s (id, gr_number, po_id, received_at, received_by, packing_slip)
    JOIN dbo.purchase_order po ON po.id = s.po_id
) AS s
ON t.id = s.id OR t.gr_number = s.gr_number
WHEN NOT MATCHED BY TARGET THEN
    INSERT (id, gr_number, po_id, received_at, received_by, packing_slip)
    VALUES (s.id, s.gr_number, s.po_id, s.received_at, s.received_by, s.packing_slip);

MERGE dbo.gr_line AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@gr987, 1, CAST(98 AS decimal(18,3))),
        (@gr980, 1, CAST(40 AS decimal(18,3)))
    ) AS s (gr_id, po_line_no, quantity_received)
    JOIN dbo.goods_receipt gr ON gr.id = s.gr_id
) AS s
ON t.gr_id = s.gr_id AND t.po_line_no = s.po_line_no
WHEN NOT MATCHED BY TARGET THEN
    INSERT (gr_id, po_line_no, quantity_received) VALUES (s.gr_id, s.po_line_no, s.quantity_received);

MERGE dbo.invoice AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@inv77812,  @bluepeak,  N'INV-77812', N'77812', CAST('2026-10-08' AS date), CAST('2026-11-07' AS date), @po123,
         'Blocked', N'Quantity: billed 2 more than received (line 1)', CAST(0 AS bit), CAST('2026-10-08T14:00:00' AS datetime2)),
        (@invNl2231, @northline, N'NL-2231',   N'2231',  CAST('2026-09-28' AS date), CAST('2026-11-12' AS date), @po118,
         'Matched', NULL,                                              CAST(1 AS bit), CAST('2026-09-28T14:00:00' AS datetime2))
    ) AS s (id, supplier_id, invoice_number, normalized_number, invoice_date, due_date, po_id, status, block_reason, first_match_passed, created_at)
    JOIN dbo.supplier sup ON sup.id = s.supplier_id
    JOIN dbo.purchase_order po ON po.id = s.po_id
) AS s
ON t.id = s.id OR (t.supplier_id = s.supplier_id AND t.normalized_number = s.normalized_number)
WHEN NOT MATCHED BY TARGET THEN
    INSERT (id, supplier_id, invoice_number, normalized_number, invoice_date, due_date, currency, po_id, tax_amount,
            status, block_reason, first_match_passed, is_retro_po, created_at)
    VALUES (s.id, s.supplier_id, s.invoice_number, s.normalized_number, s.invoice_date, s.due_date, 'USD', s.po_id, 0,
            s.status, s.block_reason, s.first_match_passed, 0, s.created_at);

MERGE dbo.invoice_line AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@inv77812,  1, 1, 'GLV-NIT-M',  'BX', N'Nitrile exam gloves, medium, box of 100', CAST(100 AS decimal(18,3)), CAST(10.2000 AS decimal(18,4))),
        (@invNl2231, 1, 1, 'PPR-A4-500', 'RM', N'Copy paper, A4, ream of 500',             CAST(40 AS decimal(18,3)),  CAST(6.5000 AS decimal(18,4)))
    ) AS s (invoice_id, line_no, po_line_no, item_code, uom, description, quantity, unit_price)
    JOIN dbo.invoice i ON i.id = s.invoice_id
) AS s
ON t.invoice_id = s.invoice_id AND t.line_no = s.line_no
WHEN NOT MATCHED BY TARGET THEN
    INSERT (invoice_id, line_no, po_line_no, item_code, uom, description, quantity, unit_price)
    VALUES (s.invoice_id, s.line_no, s.po_line_no, s.item_code, s.uom, s.description, s.quantity, s.unit_price);

-- Matched on invoice_id only: once an invoice has any check rows (seeded, or written by a re-match),
-- none are added, so a re-match result is never mixed with seeded rows.
MERGE dbo.match_check AS t
USING (
    SELECT s.*
    FROM (VALUES
        (@inv77812, 1, N'Supplier',   NULL, N'—',          N'—',      N'—',          'Pass', N'supplier matches the PO'),
        (@inv77812, 2, N'Currency',   NULL, N'USD',        N'—',      N'USD',        'Pass', N'invoice and PO both in USD'),
        (@inv77812, 3, N'PO status',  NULL, N'Approved',   N'—',      N'—',          'Pass', N'PO is approved'),
        (@inv77812, 4, N'PO line',    1,    N'GLV-NIT-M/BX', N'—',    N'GLV-NIT-M/BX', 'Pass', N'invoice line 1 matches PO line 1'),
        (@inv77812, 5, N'Quantity',   1,    N'100',        N'98',     N'100',        'Fail', N'billed 2 more than received (line 1)'),
        (@inv77812, 6, N'Unit price', 1,    N'10.00',      N'—',      N'10.20',      'Pass', N'+2.0%, inside the ±3% / $50 price tolerance'),
        (@inv77812, 7, N'Amount',     1,    N'1000.00',    N'980.00', N'1020.00',    'Fail', N'$40.00 more than the value received'),

        (@invNl2231, 1, N'Supplier',   NULL, N'—',          N'—',      N'—',          'Pass', N'supplier matches the PO'),
        (@invNl2231, 2, N'Currency',   NULL, N'USD',        N'—',      N'USD',        'Pass', N'invoice and PO both in USD'),
        (@invNl2231, 3, N'PO status',  NULL, N'Approved',   N'—',      N'—',          'Pass', N'PO is approved'),
        (@invNl2231, 4, N'PO line',    1,    N'PPR-A4-500/RM', N'—',   N'PPR-A4-500/RM', 'Pass', N'invoice line 1 matches PO line 1'),
        (@invNl2231, 5, N'Quantity',   1,    N'40',         N'40',     N'40',         'Pass', N'within the quantity received (line 1)'),
        (@invNl2231, 6, N'Unit price', 1,    N'6.50',       N'—',      N'6.50',       'Pass', N'+0.0%, inside the ±3% / $50 price tolerance'),
        (@invNl2231, 7, N'Amount',     1,    N'260.00',     N'260.00', N'260.00',     'Pass', N'matches the value received')
    ) AS s (invoice_id, seq, name, line_no, po_value, receipt_value, invoice_value, outcome, message)
    JOIN dbo.invoice i ON i.id = s.invoice_id
) AS s
ON t.invoice_id = s.invoice_id
WHEN NOT MATCHED BY TARGET THEN
    INSERT (invoice_id, seq, name, line_no, po_value, receipt_value, invoice_value, outcome, message)
    VALUES (s.invoice_id, s.seq, s.name, s.line_no, s.po_value, s.receipt_value, s.invoice_value, s.outcome, s.message);
