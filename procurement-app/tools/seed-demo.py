#!/usr/bin/env python3
"""Loads several months of demo purchasing activity through the API.

Everything goes through the real endpoints (create PO -> approve -> receive -> invoice), so the match engine,
approval routing and audit trail decide every outcome exactly as they would for a user. Nothing here writes
SQL. Runs once: if any invoice numbered DMO-* exists, it exits without changing anything.

Usage: python3 seed-demo.py [api-base-url]        (default http://localhost:8080)
Standard library only, so it runs in a plain python image.
"""

import datetime as dt
import json
import random
import sys
import urllib.error
import urllib.request

API = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:8080").rstrip("/")
PO_COUNT = 72
MONTHS_BACK = 8
rng = random.Random(2026)  # fixed seed: the same scenario mix on every fresh database

# Catalog per purchasable category: (item code, description, unit of measure, unit price, receipt required)
CATALOG = {
    "MED-SUPPLIES": ("6110", "CC-200", [
        ("GLV-NIT-L", "Nitrile exam gloves, large, box of 100", "BX", 10.40, True),
        ("MSK-SURG", "Surgical masks, level 2, box of 50", "BX", 14.50, True),
        ("GZE-4X4", "Gauze pads 4x4, pack of 200", "PK", 6.75, True),
        ("SYR-10ML", "Syringes 10 ml, box of 100", "BX", 22.00, True),
        ("TBL-PPR", "Exam table paper, roll", "RL", 18.40, True),
        ("SAN-CASE", "Hand sanitizer, case of 12", "CS", 48.00, True),
    ]),
    "LAB": ("6120", "CC-300", [
        ("SPC-CUP", "Specimen cups, case of 500", "CS", 36.00, True),
        ("PIP-TIP", "Pipette tips 200 ul, box of 1000", "BX", 54.00, True),
        ("RGT-KIT", "Chemistry reagent kit", "KT", 410.00, True),
        ("CTF-TUBE", "Centrifuge tubes 15 ml, case", "CS", 62.00, True),
        ("LAB-COAT", "Lab coat, fluid resistant", "EA", 28.00, True),
    ]),
    "OFFICE": ("6200", "CC-100", [
        ("PPR-A4-500", "Copy paper, A4, ream of 500", "RM", 6.50, True),
        ("TNR-HP58", "Toner cartridge, black", "EA", 89.00, True),
        ("PEN-BLK", "Ballpoint pens, box of 50", "BX", 12.00, True),
        ("FLD-MAN", "Manila folders, box of 100", "BX", 18.00, True),
        ("CHR-TASK", "Task chair, ergonomic", "EA", 249.00, True),
    ]),
    "IT": ("6300", "CC-100", [
        ("LPT-14", "Laptop, 14 inch, 32 GB", "EA", 1380.00, True),
        ("MON-27", "Monitor, 27 inch 4K", "EA", 329.00, True),
        ("DCK-USB4", "USB4 docking station", "EA", 219.00, True),
        ("NSW-48P", "Network switch, 48 port", "EA", 1190.00, True),
        ("LBL-PRN", "Label printer, thermal", "EA", 412.00, True),
    ]),
    "FACILITIES": ("6400", "CC-400", [
        ("HVAC-FLT", "HVAC filter set, MERV 13", "ST", 145.00, True),
        ("SVC-FLOOR", "Floor care service, monthly", "SV", 850.00, False),
        ("LED-PNL", "LED ceiling panel 2x4", "EA", 64.00, True),
        ("PNT-GAL", "Interior paint, gallon", "GL", 42.00, True),
        ("SVC-LAWN", "Grounds maintenance visit", "SV", 620.00, False),
    ]),
}

REQUESTERS = [
    "Harborview Clinics – Supply room", "Harborview Clinics – Front office", "Laboratory – Intake",
    "Laboratory – Chemistry", "IT – Service desk", "Facilities – East campus", "Administration – Finance",
]

# scenario: (weight, name). Outcomes come from the match engine, not from this script.
SCENARIOS = [
    (62, "clean"),         # approve, receive everything, invoice exactly            -> Matched
    (8, "price"),          # invoice unit price ~8% above the PO (outside tolerance)  -> Blocked: price
    (8, "short"),          # receive less than ordered, invoice the full quantity     -> Blocked: quantity
    (6, "retro"),          # invoice dated before the PO, otherwise clean             -> Matched, counts as retro PO
    (8, "open"),           # approved and received, not invoiced yet
    (4, "draft"),          # created, never approved
    (4, "unreceived"),     # approved, invoiced before anything arrived               -> Blocked: quantity
]


def call(method, path, body=None):
    data = None if body is None else json.dumps(body).encode()
    request = urllib.request.Request(API + path, data=data, method=method,
                                     headers={"Content-Type": "application/json", "Accept": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            text = response.read().decode()
            return response.status, (json.loads(text) if text else None)
    except urllib.error.HTTPError as error:
        return error.code, error.read().decode()


def ok(status, body, what):
    if status >= 300:
        raise SystemExit(f"seed-demo: {what} failed with HTTP {status}: {body}")
    return body


def pick_scenario():
    total = sum(w for w, _ in SCENARIOS)
    roll = rng.uniform(0, total)
    for weight, name in SCENARIOS:
        roll -= weight
        if roll <= 0:
            return name
    return SCENARIOS[0][1]


def main():
    status, invoices = call("GET", "/api/invoices")
    ok(status, invoices, "GET /api/invoices")
    if any(i["invoiceNumber"].startswith("DMO-") for i in invoices):
        print("seed-demo: demo activity already loaded, nothing to do.")
        return

    suppliers = ok(*call("GET", "/api/suppliers"), "GET /api/suppliers")
    by_category = {}
    for s in suppliers:
        if s["categoryCode"] in CATALOG:
            by_category.setdefault(s["categoryCode"], []).append(s)

    active = {c: [s for s in ss if not s["isBlocked"]] for c, ss in by_category.items()}
    blocked = [s for ss in by_category.values() for s in ss if s["isBlocked"]]

    today = dt.date.today()
    counts = {}
    invoice_no = 40000

    plans = [("blocked-supplier", s) for s in blocked]  # one invoice per blocked supplier: Blocked: supplier
    for _ in range(PO_COUNT):
        category = rng.choices(list(active), weights=[5 if c == "MED-SUPPLIES" else 3 for c in active])[0]
        plans.append((pick_scenario(), rng.choice(active[category])))

    for scenario, supplier in plans:
        gl, cost_center, items = CATALOG[supplier["categoryCode"]]
        # Skew PO dates toward recent months so the trend has a visible shape.
        days_ago = int(rng.triangular(4, MONTHS_BACK * 30, 20))
        po_date = today - dt.timedelta(days=days_ago)

        lines = []
        for item in rng.sample(items, k=rng.choice([1, 1, 2, 2, 3])):
            code, description, uom, price, receipt_required = item
            quantity = rng.choice([1, 2, 4, 6, 10, 12, 20, 25, 40]) if price < 500 else rng.choice([1, 2, 3, 5, 8, 12, 20])
            lines.append({"itemCode": code, "description": description, "uom": uom, "quantity": quantity,
                          "unitPrice": price, "glAccount": gl, "receiptRequired": receipt_required})

        po = ok(*call("POST", "/api/purchase-orders", {
            "supplierId": supplier["id"], "requesterName": rng.choice(REQUESTERS), "costCenterCode": cost_center,
            "currency": "USD", "poDate": po_date.isoformat(), "lines": lines,
        }), f"create PO for {supplier['code']}")
        counts[scenario] = counts.get(scenario, 0) + 1

        if scenario == "draft":
            continue
        ok(*call("POST", f"/api/purchase-orders/{po['id']}/approve"), f"approve PO {po['number']}")

        if scenario != "unreceived":
            received = []
            for n, line in enumerate(lines, start=1):
                if not line["receiptRequired"]:
                    continue
                quantity = line["quantity"]
                if scenario == "short" and n == 1:
                    quantity = max(quantity - max(1, quantity // 5), 0)
                if quantity > 0:
                    received.append({"poLineNo": n, "quantity": quantity})
            if received:
                ok(*call("POST", f"/api/purchase-orders/{po['id']}/receipts", {
                    "receivedBy": "Receiving dock", "packingSlip": f"PS-{rng.randint(10000, 99999)}", "lines": received,
                }), f"receive PO {po['number']}")

        if scenario == "open":
            continue

        invoice_date = po_date + dt.timedelta(days=rng.randint(3, 25))
        if scenario == "retro":
            invoice_date = po_date - dt.timedelta(days=rng.randint(2, 10))
        invoice_date = min(invoice_date, today)

        invoice_lines = []
        for n, line in enumerate(lines, start=1):
            price = line["unitPrice"]
            if scenario == "price" and n == 1:
                price = round(price * 1.08 + 5, 2)
            invoice_lines.append({"poLineNo": n, "itemCode": line["itemCode"], "uom": line["uom"],
                                  "description": line["description"], "quantity": line["quantity"], "unitPrice": price})

        invoice_no += rng.randint(1, 37)
        ok(*call("POST", "/api/invoices", {
            "supplierId": supplier["id"], "invoiceNumber": f"DMO-{invoice_no}", "invoiceDate": invoice_date.isoformat(),
            "currency": "USD", "poNumber": po["number"], "taxAmount": 0, "lines": invoice_lines,
        }), f"invoice PO {po['number']}")

    summary = ", ".join(f"{n} {k}" for k, n in sorted(counts.items()))
    print(f"seed-demo: created {sum(counts.values())} purchase orders ({summary}).")


if __name__ == "__main__":
    main()
