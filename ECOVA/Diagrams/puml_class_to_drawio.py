#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
puml_class_to_drawio.py  — ECOVA PlantUML Class Diagram → draw.io XML
=======================================================================
Layout strategy (per diagram):
  02a DTO  → Grid 5 cột, nhóm có-attr trước rồi không-attr
  02b DAL  → Row-based: interface hàng trên, impl hàng dưới (5 cột x N hàng)
  02c BLL  → Row-based: interface hàng trên, service hàng dưới
  02d GUI  → Column-based: mỗi nhóm chức năng 1 cột dọc
"""

import os, sys, re, xml.sax.saxutils
from typing import List, Tuple, Optional, Dict

# ── Kích thước cơ bản ─────────────────────────────────────────────────────────
CLASS_W   = 340
HEADER_H  = 40
ATTR_H    = 18
MIN_H     = 60
ROW_GAP   = 35    # khoảng cách dọc giữa 2 hàng trong cùng section
SECT_GAP  = 90    # khoảng cách dọc giữa 2 section
INTRA_GAP = 25    # khoảng cách ngang giữa 2 class trong cùng hàng/cột
COL_GAP   = 30    # khoảng cách ngang giữa 2 cột (layout cột)

# ═════════════════════════════════════════════════════════════════════════════
# DATA CLASSES
# ═════════════════════════════════════════════════════════════════════════════

class ClassNode:
    def __init__(self, name:str, alias:str, kind:str="class", stereotype:str=""):
        self.name       = name
        self.alias      = alias
        self.kind       = kind
        self.stereotype = stereotype.lower()
        self.attributes : List[str] = []
        self.methods    : List[str] = []
        self.x = self.y = 0
        self.w = CLASS_W
        self.h = MIN_H
        self.cell_id = ""

    def compute_height(self):
        total = len(self.attributes) + len(self.methods)
        body_h = max(MIN_H - HEADER_H, total * ATTR_H + 8)
        if self.attributes and self.methods:
            body_h += 4
        self.h = HEADER_H + body_h

class Relationship:
    def __init__(self, src:str, tgt:str, rel_type:str, label:str="",
                 src_card:str="", tgt_card:str=""):
        self.src      = src
        self.tgt      = tgt
        self.rel_type = rel_type
        self.label    = label
        self.src_card = src_card
        self.tgt_card = tgt_card

# ═════════════════════════════════════════════════════════════════════════════
# PARSER
# ═════════════════════════════════════════════════════════════════════════════

def parse_puml(text: str) -> Tuple[List[ClassNode], List[Relationship]]:
    nodes: dict = {}
    rels: List[Relationship] = []
    name_to_alias: dict = {}
    current: Optional[ClassNode] = None
    in_body = False
    in_methods = False

    for raw in text.splitlines():
        line = raw.strip()
        if line.startswith("'") or line.startswith("@") or not line:
            continue

        m = re.match(
            r'^(abstract\s+class|class|interface|enum)\s+"([^"]+)"\s+as\s+(\w+)'
            r'(?:\s+<<([^>]+)>>)?\s*\{?', line, re.IGNORECASE)
        if m:
            raw_kind = m.group(1).lower().strip()
            kind  = "abstract" if "abstract" in raw_kind else raw_kind
            name  = m.group(2)
            alias = m.group(3)
            stereo = (m.group(4) or "").strip().lower()
            node = ClassNode(name, alias, kind, stereo)
            nodes[alias] = node
            name_to_alias[name] = alias
            in_body = "{" in line
            in_methods = False
            current = node if in_body else None
            continue

        if current and in_body:
            if line == "}":
                current.compute_height(); current = None; in_body = False; in_methods = False; continue
            if line in ("--", "..", "=="): in_methods = True; continue
            if line.startswith("'"):  current.attributes.append(line); continue
            if line.startswith("..") and line.endswith(".."): in_methods = True; continue
            cleaned = line.rstrip("{}")
            if in_methods: current.methods.append(cleaned)
            else:          current.attributes.append(cleaned)
            continue

        rp = re.match(
            r'^([\w"]+)\s*("[^"]*")?\s*([\|o\*\-<>\.]+)\s*("[^"]*")?\s*([\w"]+)'
            r'(?:\s*:\s*"?([^"]+)"?)?$', line)
        if rp:
            rs, sc, conn, tc, rt, lbl = (
                rp.group(1).strip('"'), (rp.group(2) or "").strip('"'),
                rp.group(3), (rp.group(4) or "").strip('"'),
                rp.group(5).strip('"'), (rp.group(6) or "").strip())
            src = rs if rs in nodes else name_to_alias.get(rs, rs)
            tgt = rt if rt in nodes else name_to_alias.get(rt, rt)
            rels.append(Relationship(src, tgt, _cls_conn(conn), lbl, sc, tc))

    return list(nodes.values()), rels

def _cls_conn(c: str) -> str:
    c = c.replace(" ", "")
    if "<|.." in c or "..|>" in c: return "REALIZATION"
    if "<|--" in c or "--|>" in c: return "GENERALIZATION"
    if "o--"  in c or "--o"  in c: return "AGGREGATION"
    if "*--"  in c or "--*"  in c: return "COMPOSITION"
    if "..>"  in c or "<.."  in c: return "DEPENDENCY"
    return "ASSOCIATION"

# ═════════════════════════════════════════════════════════════════════════════
# LAYOUT DEFINITIONS
# ═════════════════════════════════════════════════════════════════════════════

# ── 02b DAL: row-based ──────────────────────────────────────────────────────
# None = section break (thêm khoảng trắng dọc)
DAL_ROWS: List = [
    # Cụm 1: User · Contract · Customer · Employee · Order
    ["IUserRepository",  "IContractRepository",  "ICustomerRepository",
     "IEmployeeRepository", "IOrderRepository"],
    ["UserRepository",   "ContractRepository",   "CustomerRepository",
     "EmployeeRepository",  "OrderRepository"],
    None,
    # Cụm 2: Sample · TestResult · AuditLog · SamplingPlan · StandardParameter
    ["ISampleRepository", "ITestResultRepository", "IAuditLogRepository",
     "ISamplingPlanRepository", "IStandardParameterRepository"],
    ["SampleRepository",  "TestResultRepository",  "AuditLogRepository",
     "SamplingPlanRepository",  "StandardParameterRepository"],
    None,
    # Infrastructure + no-interface
    ["TestingResultRepository", "ResultAuditLogRepository",
     "SystemAuditHelper", "SqlHelper"],
    ["DbConnectionFactory"],
]

# ── 02c BLL: row-based ──────────────────────────────────────────────────────
BLL_ROWS: List = [
    # Cụm 1: UserBLL · Auth · Contract · Customer · Employee
    ["IUserBLL",         "IAuthService",    "IContractService",
     "ICustomerService", "IEmployeeService"],
    ["UserBLL",          "AuthService",     "ContractService",
     "CustomerService",  "EmployeeService"],
    None,
    # Cụm 2: Planning · Testing · Export · Approval · Notification
    ["IPlanningService", "ITestingService", "IExportService",
     "IApprovalService", "INotificationService"],
    ["PlanningService",  "TestingService",  "ExportService",
     "ApprovalService",  "NotificationService"],
    None,
    # Validators + Singleton
    ["AbstractValidator", "ContractValidator", "ResultValidator",
     "AiIntegrationService"],
]

# ── 02d GUI: column-based ────────────────────────────────────────────────────
GUI_COLS: List[List[str]] = [
    # Col 1: Enums
    ["AppLanguage", "ContractStatus", "OrderStatus",
     "SampleStatus", "EmailStatus", "ApprovalStatus"],
    # Col 2: Singletons
    ["AppState", "LanguageManager"],
    # Col 3: Static Helpers
    ["SecurityHelper", "AesHelper", "PdfExportHelper", "EmailSmtpHelper"],
    # Col 4: GUI Services
    ["LoginThrottleService", "SessionTimeoutService",
     "FaceIdManager", "VoiceSearchService"],
    # Col 5: Forms
    ["Login", "MainForm", "ContractCreateForm", "ContractEditForm",
     "CustomerEditForm", "EmployeeAddEditForm", "EditProfileForm",
     "SendMailForm", "SmtpSetupForm", "AddAreaForm",
     "AddParameterForm", "PrintBarcodeForm", "VoiceDownloadForm"],
    # Col 6: UserControls
    ["DashboardUC", "CustomerManagementUC", "ContractListUC",
     "SampleConfigUC", "ParameterConfigUC", "EnterResultUC",
     "LabResultUC", "DirectorApprovalUC",
     "EmployeeManagementUC", "NotificationUC"],
]

# ═════════════════════════════════════════════════════════════════════════════
# LAYOUT ENGINE
# ═════════════════════════════════════════════════════════════════════════════

def auto_layout(nodes: List[ClassNode], diagram_key: str = ""):
    alias_map = {n.alias: n for n in nodes}
    name_map  = {n.name:  n for n in nodes}

    def resolve(s: str):
        return alias_map.get(s) or name_map.get(s)

    placed: set = set()

    if diagram_key == "02b":
        _layout_rows(DAL_ROWS, resolve, placed, canvas_center=950)

    elif diagram_key == "02c":
        _layout_rows(BLL_ROWS, resolve, placed, canvas_center=950)

    elif diagram_key == "02d":
        _layout_cols(GUI_COLS, resolve, placed, start_x=60)

    else:
        # 02a DTO — simple 5-column grid
        _layout_grid_dto(nodes, cols=5, canvas_center=950)
        return

    # Orphans (không khai báo trong layout) → xếp dưới cùng
    orphans = sorted([n for n in nodes if n.alias not in placed],
                     key=lambda n: n.name)
    if orphans:
        max_y = max((n.y + n.h for n in nodes if n.alias in placed), default=60)
        ox = 60
        for n in orphans:
            n.x, n.y = ox, max_y + SECT_GAP
            ox += n.w + INTRA_GAP


def _layout_rows(rows: List, resolve, placed: set, canvas_center: int):
    """Xếp theo hàng ngang, căn giữa theo canvas_center."""
    y = 60
    for row_item in rows:
        if row_item is None:
            y += SECT_GAP
            continue
        row_nodes = [n for a in row_item if (n := resolve(a)) is not None]
        if not row_nodes:
            continue
        total_w = len(row_nodes) * CLASS_W + (len(row_nodes) - 1) * INTRA_GAP
        x = max(60, canvas_center - total_w // 2)
        max_h = MIN_H
        for node in row_nodes:
            node.x, node.y = x, y
            x += node.w + INTRA_GAP
            max_h = max(max_h, node.h)
            placed.add(node.alias)
        y += max_h + ROW_GAP


def _layout_cols(cols: List[List[str]], resolve, placed: set, start_x: int):
    """Xếp theo cột dọc."""
    x = start_x
    for col_aliases in cols:
        col_nodes = [n for a in col_aliases if (n := resolve(a)) is not None]
        if not col_nodes:
            continue
        max_w = max(n.w for n in col_nodes)
        y = 60
        for node in col_nodes:
            node.x, node.y = x, y
            y += node.h + ROW_GAP
            placed.add(node.alias)
        x += max_w + COL_GAP


def _layout_grid_dto(nodes: List[ClassNode], cols: int, canvas_center: int):
    """Grid layout cho DTO: nhóm có-attrs trước, không-attrs sau."""
    def dto_key(n):
        st = n.stereotype
        nm = n.name.lower()
        if "request" in st: return (0, nm)
        if "response" in st: return (1, nm)
        return (2, nm)

    with_attrs    = sorted([n for n in nodes if n.attributes or n.methods], key=dto_key)
    without_attrs = sorted([n for n in nodes if not n.attributes and not n.methods],
                           key=lambda n: n.name)
    ordered = with_attrs + without_attrs

    col_w = CLASS_W + INTRA_GAP
    total_row_w = cols * CLASS_W + (cols - 1) * INTRA_GAP
    start_x = max(60, canvas_center - total_row_w // 2)

    col_heights = [60] * cols
    for idx, node in enumerate(ordered):
        col = idx % cols
        node.x = start_x + col * col_w
        node.y = col_heights[col]
        col_heights[col] += node.h + ROW_GAP

# ═════════════════════════════════════════════════════════════════════════════
# XML BUILDER
# ═════════════════════════════════════════════════════════════════════════════

def escape(text: str) -> str:
    return xml.sax.saxutils.escape(str(text))

def build_drawio_xml(nodes: List[ClassNode], rels: List[Relationship],
                     title: str = "") -> str:
    cid = [10]
    a2c: dict = {}  # alias/name → cell_id

    def new_id():
        v = str(cid[0]); cid[0] += 1; return v

    out = []
    out.append('<?xml version="1.0" encoding="UTF-8"?>')
    out.append('<mxGraphModel dx="1400" dy="900" grid="1" gridSize="10" '
               'guides="1" tooltips="1" connect="1" arrows="1" fold="1" '
               'page="1" pageScale="1" pageWidth="2200" pageHeight="2800" '
               'math="0" shadow="0">')
    out.append('  <root>')
    out.append('    <mxCell id="0"/>')
    out.append('    <mxCell id="1" parent="0"/>')

    for node in nodes:
        stereo_line = ""
        if   node.kind == "interface": stereo_line = "«interface»\n"
        elif node.kind == "abstract":  stereo_line = "«abstract»\n"
        elif node.kind == "enum":      stereo_line = "«enumeration»\n"
        elif node.stereotype:          stereo_line = f"«{node.stereotype}»\n"

        header = escape(f"{stereo_line}{node.name}")
        cid_n  = new_id()
        a2c[node.alias] = cid_n
        a2c[node.name]  = cid_n

        sw_style = (
            f"swimlane;fontStyle=1;align=center;startSize={HEADER_H};"
            "fillColor=#FFFFFF;strokeColor=#000000;fontColor=#000000;"
            "fontSize=11;whiteSpace=wrap;html=1;rounded=0;"
        )
        out.append(f'    <mxCell id="{cid_n}" value="{header}" '
                   f'style="{sw_style}" vertex="1" parent="1">')
        out.append(f'      <mxGeometry x="{node.x}" y="{node.y}" '
                   f'width="{node.w}" height="{node.h}" as="geometry"/>')
        out.append(f'    </mxCell>')

        y_i = HEADER_H
        attr_stl = ("text;strokeColor=none;fillColor=none;align=left;"
                    "verticalAlign=top;spacingLeft=5;overflow=hidden;"
                    f"rotatable=0;fontSize=10;fontColor=#000000;")
        for attr in node.attributes:
            aid = new_id()
            out.append(f'    <mxCell id="{aid}" value="{escape(attr)}" '
                       f'style="{attr_stl}" vertex="1" parent="{cid_n}">')
            out.append(f'      <mxGeometry y="{y_i}" width="{node.w}" '
                       f'height="{ATTR_H}" as="geometry"/>')
            out.append(f'    </mxCell>')
            y_i += ATTR_H

        if node.attributes and node.methods:
            sid = new_id()
            out.append(f'    <mxCell id="{sid}" value="" '
                       f'style="line;strokeColor=#000000;fillColor=none;rotatable=0;fontSize=0;" '
                       f'vertex="1" parent="{cid_n}">')
            out.append(f'      <mxGeometry y="{y_i}" width="{node.w}" height="4" as="geometry"/>')
            out.append(f'    </mxCell>')
            y_i += 4

        mth_stl = ("text;strokeColor=none;fillColor=none;align=left;"
                   "verticalAlign=top;spacingLeft=5;overflow=hidden;"
                   f"rotatable=0;fontSize=10;fontColor=#000000;fontStyle=2;")
        for meth in node.methods:
            mid = new_id()
            out.append(f'    <mxCell id="{mid}" value="{escape(meth)}" '
                       f'style="{mth_stl}" vertex="1" parent="{cid_n}">')
            out.append(f'      <mxGeometry y="{y_i}" width="{node.w}" '
                       f'height="{ATTR_H}" as="geometry"/>')
            out.append(f'    </mxCell>')
            y_i += ATTR_H

    # ── Edges ────────────────────────────────────────────────────────────────
    REL = {
        "REALIZATION"   : "endArrow=block;endFill=0;dashed=1;endSize=10;strokeColor=#000000;strokeWidth=1.5;html=1;",
        "GENERALIZATION": "endArrow=block;endFill=0;dashed=0;endSize=10;strokeColor=#000000;strokeWidth=1.5;html=1;",
        "ASSOCIATION"   : "endArrow=open;endFill=0;dashed=0;strokeColor=#000000;strokeWidth=1.5;html=1;",
        "DEPENDENCY"    : "endArrow=open;endFill=0;dashed=1;strokeColor=#000000;strokeWidth=1.5;html=1;",
        "AGGREGATION"   : "endArrow=open;startArrow=diamondThin;startFill=0;dashed=0;strokeColor=#000000;strokeWidth=1.5;html=1;",
        "COMPOSITION"   : "endArrow=open;startArrow=diamondThin;startFill=1;dashed=0;strokeColor=#000000;strokeWidth=1.5;html=1;",
    }
    LBL_STL = ("resizable=0;html=1;align=center;verticalAlign=middle;"
               "strokeColor=none;fillColor=none;fontSize=9;fontColor=#333333;fontStyle=1;")

    for rel in rels:
        sid = a2c.get(rel.src, "")
        tid = a2c.get(rel.tgt, "")
        if not sid or not tid: continue
        eid   = new_id()
        style = REL.get(rel.rel_type, REL["ASSOCIATION"])
        out.append(f'    <mxCell id="{eid}" value="" style="{style}" '
                   f'edge="1" source="{sid}" target="{tid}" parent="1">')
        out.append(f'      <mxGeometry relative="1" as="geometry"/>')
        out.append(f'    </mxCell>')
        if rel.label:
            lid = new_id()
            out.append(f'    <mxCell id="{lid}" value="{escape(rel.label)}" '
                       f'style="{LBL_STL}" connectable="0" vertex="1" parent="{eid}">')
            out.append(f'      <mxGeometry x="0" y="-10" relative="1" as="geometry"/>')
            out.append(f'    </mxCell>')

    out.append('  </root>')
    out.append('</mxGraphModel>')
    return "\n".join(out)

# ═════════════════════════════════════════════════════════════════════════════
# MAIN
# ═════════════════════════════════════════════════════════════════════════════

def main():
    sys.stdout.reconfigure(encoding="utf-8")
    script_dir = os.path.dirname(os.path.abspath(__file__))

    files = [
        ("02a_ClassDiagram_DTO.puml", "02a"),
        ("02b_ClassDiagram_DAL.puml", "02b"),
        ("02c_ClassDiagram_BLL.puml", "02c"),
        ("02d_ClassDiagram_GUI.puml", "02d"),
    ]

    for fname, key in files:
        src = os.path.join(script_dir, fname)
        if not os.path.exists(src):
            print(f"[SKIP] {fname}")
            continue
        with open(src, encoding="utf-8") as f:
            text = f.read()

        nodes, rels = parse_puml(text)
        auto_layout(nodes, diagram_key=key)
        xml = build_drawio_xml(nodes, rels, title=fname)

        out = os.path.join(script_dir, fname.replace(".puml", ".drawio"))
        with open(out, "w", encoding="utf-8") as f:
            f.write(xml)
        print(f"[OK] {fname}  →  {os.path.basename(out)}")
        print(f"     Classes: {len(nodes)}  |  Relations: {len(rels)}")

    print("\n✅ Xong. Import từng file .drawio vào app.diagrams.net")

if __name__ == "__main__":
    main()
