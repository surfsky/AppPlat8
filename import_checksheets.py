import pandas as pd
import sqlite3

db_path = 'App/Db/sqlite.db'
excel_path = 'doc/data/250918-检查表单.xlsx'

df = pd.read_excel(excel_path, sheet_name=0)
conn = sqlite3.connect(db_path)
cursor = conn.cursor()
count = 0

for idx, row in df.iterrows():
    sheet_name = str(row['检查表名']).strip() if not pd.isna(row['检查表名']) else ''
    tag = str(row['标签属性']).strip() if not pd.isna(row['标签属性']) else ''
    scope = str(row['所属领域']).strip() if not pd.isna(row['所属领域']) else ''
    order_no = int(row['编号']) if not pd.isna(row['编号']) else None
    risk_level = str(row['隐患等级']).strip() if not pd.isna(row['隐患等级']) else ''
    content = str(row['检查项名称']).strip() if not pd.isna(row['检查项名称']) else ''
    if not content:
        continue

    cursor.execute("SELECT Id FROM CheckSheets WHERE Name=?", (sheet_name,))
    sheet = cursor.fetchone()
    sheet_id = sheet[0] if sheet else None
    if not sheet_id:
        cursor.execute("INSERT INTO CheckSheets (Name, Tag, Scope) VALUES (?, ?, ?)", (sheet_name, tag, scope))
        sheet_id = cursor.lastrowid

    cursor.execute(
        "INSERT INTO CheckSheetItems (SheetId, OrderNo, RiskLevel, Content) VALUES (?, ?, ?, ?)",
        (sheet_id, order_no, risk_level, content)
    )
    count += 1

conn.commit()
conn.close()
print(f'导入完成，共导入{count}条检查项。')