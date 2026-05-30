#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
generate_chen_erd.py  — ECOVA Chen ERD (Black & White, Tối ưu Bố cục Chặt chẽ)
==========================================================================
Đã chỉnh sửa thu hẹp đáng kể khoảng cách để vừa vặn màn hình mà vẫn không đè nhau.
Bán kính elip cũng được thu nhỏ lại thành cụm ôm sát thực thể.
"""

import os, sys, math, xml.sax.saxutils

# ── Sizes ──────────────────────────────────────────────────────────────────
EW,  EH  = 140,  50    # Kích thước Entity
DW,  DH  = 100,  60    # Kích thước Mối quan hệ (Thoi)
AW,  AH  =  90,  30    # Kích thước Thuộc tính (Elip)

# ── Styles (BLACK & WHITE) ─────────────────────────────────────────────────
def sty_entity(weak=False):
    double = "shape=mxgraph.er.entity;double=1;" if weak else ""
    return (f"rounded=0;whiteSpace=wrap;html=1;{double}"
            f"fillColor=#ffffff;strokeColor=#000000;strokeWidth=2;"
            f"fontColor=#000000;fontStyle=1;fontSize=12;verticalAlign=middle;")

def sty_diamond(nullable=False):
    dash = "dashed=1;" if nullable else ""
    return (f"rhombus;whiteSpace=wrap;html=1;"
            f"fillColor=#ffffff;strokeColor=#000000;strokeWidth=2;{dash}"
            f"fontColor=#000000;fontStyle=0;fontSize=10;verticalAlign=middle;align=center;")

def sty_attr(kind="normal"):
    if kind == "pk":
        return (f"ellipse;whiteSpace=wrap;html=1;"
                f"fillColor=#ffffff;strokeColor=#000000;strokeWidth=1.5;"
                f"fontColor=#000000;fontSize=9;fontStyle=4;")
    elif kind == "fk":
        return (f"ellipse;whiteSpace=wrap;html=1;"
                f"fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
                f"fontColor=#000000;fontSize=9;fontStyle=2;")        
    else:
        return (f"ellipse;whiteSpace=wrap;html=1;"
                f"fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
                f"fontColor=#000000;fontSize=9;fontStyle=0;")

STYLE_EDGE      = "endArrow=none;html=1;strokeColor=#000000;strokeWidth=1.5;endFill=0;"
STYLE_EDGE_DASH = "endArrow=none;html=1;strokeColor=#000000;strokeWidth=1.5;dashed=1;endFill=0;"
STYLE_CARD      = (f"resizable=0;html=1;align=center;verticalAlign=middle;"
                   f"strokeColor=none;fillColor=#ffffff;fontSize=12;fontStyle=1;fontColor=#000000;")

# ══════════════════════════════════════════════════════════════════════════════
# ENTITIES (Khoảng cách hợp lý hơn: X cách ~650, Y cách ~450)
# ══════════════════════════════════════════════════════════════════════════════
ENTITIES = {
    # Row 1 (Y=200)
    "Roles":              {"pos": ( 200, 200), "weak": False, "attrs": [
        ("RoleID", "pk"), ("RoleCode", "normal"), ("RoleName", "normal"), ("Description", "normal")]},
    "Users":              {"pos": ( 850, 200), "weak": False, "attrs": [
        ("UserID", "pk"), ("RoleID", "fk"), ("EmployeeCode", "normal"), ("Username", "normal"), 
        ("PasswordHash", "normal"), ("FullName", "normal"), ("Email", "normal"), ("Phone", "normal"), 
        ("DateOfBirth", "normal"), ("Department", "normal"), ("AvatarData", "normal"), 
        ("FaceIDData", "normal"), ("IsFaceIDRegistered", "normal"), ("IsActive", "normal"), 
        ("CreatedDate", "normal"), ("UpdatedAt", "normal")]},
    "Customers":          {"pos": (1500, 200), "weak": False, "attrs": [
        ("CustomerID", "pk"), ("TaxCode", "normal"), ("CompanyName", "normal"), 
        ("Address", "normal"), ("Representative", "normal"), ("ContactEmail", "normal"), ("PhoneNumber", "normal")]},
    "CustomerFeedbacks":  {"pos": (2150, 200), "weak": True, "attrs": [
        ("FeedbackID", "pk"), ("CustomerID", "fk"), ("ResponseSpeed", "normal"), 
        ("ResponseTime", "normal"), ("PreviousViolations", "normal"), ("Frequency", "normal"), ("CreatedDate", "normal")]},

    # Row 2 (Y=650)
    "AuditLogs":          {"pos": ( 200, 650), "weak": True, "attrs": [
        ("LogID", "pk"), ("UserID", "fk"), ("Action", "normal"), ("EntityType", "normal"), 
        ("EntityID", "normal"), ("Detail", "normal"), ("LoggedAt", "normal")]},
    "Contracts":          {"pos": ( 850, 650), "weak": False, "attrs": [
        ("ContractID", "pk"), ("CustomerID", "fk"), ("CreatedBy", "fk"), ("SignedDate", "normal"), 
        ("ValidFrom", "normal"), ("ValidTo", "normal"), ("ContractFilePath", "normal"), 
        ("Status", "normal"), ("TotalContractValue", "normal"), ("IndustryType", "normal"), 
        ("RenewalLabel", "normal"), ("UpdatedAt", "normal")]},
    "Orders":             {"pos": (1500, 650), "weak": False, "attrs": [
        ("OrderID", "pk"), ("ContractID", "fk"), ("OrderName", "normal"), ("EnvironmentType", "normal"), 
        ("OrderDate", "normal"), ("Deadline", "normal"), ("FinalReportPath", "normal"), 
        ("IsApproved", "normal"), ("Status", "normal"), ("CreatedBy", "normal"), ("UpdatedAt", "normal")]},

    # Row 3 (Y=1100)
    "EmailQueue":         {"pos": ( 200, 1100), "weak": False, "attrs": [
        ("EmailID", "pk"), ("Recipient", "normal"), ("Subject", "normal"), ("Body", "normal"), 
        ("AttachmentPath", "normal"), ("Status", "normal"), ("CreatedTime", "normal"), ("Type", "normal")]},
    "Samples":            {"pos": ( 850, 1100), "weak": False, "attrs": [
        ("SampleID", "pk"), ("OrderID", "fk"), ("RegulationID", "fk"), ("SamplerID", "fk"), 
        ("Barcode", "normal"), ("SamplingLocation", "normal"), ("SamplingTime", "normal"), 
        ("FieldTemperature", "normal"), ("FieldHumidity", "normal"), ("WeatherCondition", "normal"), 
        ("FieldImage", "normal"), ("IsWarning", "normal"), ("Status", "normal"), ("UpdatedAt", "normal")]},
    "SamplingPlanItems":  {"pos": (1500, 1100), "weak": True, "attrs": [
        ("PlanItemID", "pk"), ("OrderID", "fk"), ("ParamID", "fk"), ("RegulationID", "fk"), 
        ("Department", "normal"), ("QcvnLimit", "normal")]},
    "Regulations":        {"pos": (2150, 1100), "weak": False, "attrs": [
        ("RegulationID", "pk"), ("Code", "normal"), ("Name", "normal"), ("EnvironmentType", "normal")]},

    # Row 4 (Y=1550)
    "TestResults":        {"pos": ( 850, 1550), "weak": False, "attrs": [
        ("ResultID", "pk"), ("SampleID", "fk"), ("ParamID", "fk"), ("TesterID", "fk"), 
        ("ResultValue", "normal"), ("IsWarning", "normal"), ("EnteredAt", "normal")]},
    "TestParameters":     {"pos": (1500, 1550), "weak": False, "attrs": [
        ("ParamID", "pk"), ("ParamName", "normal"), ("Unit", "normal"), ("TestMethod", "normal"), 
        ("IsField", "normal"), ("Price", "normal")]},
    "RegulationLimits":   {"pos": (2150, 1550), "weak": True, "attrs": [
        ("LimitID", "pk"), ("RegulationID", "fk"), ("ParamID", "fk"), ("MinValue", "normal"), ("MaxValue", "normal")]},

    # Row 5 (Y=2000)
    "ResultHistory":      {"pos": ( 850, 2000), "weak": True, "attrs": [
        ("HistoryID", "pk"), ("ResultID", "fk"), ("ChangedBy", "fk"), ("OldValue", "normal"), 
        ("NewValue", "normal"), ("ChangedAt", "normal")]},
}

# ══════════════════════════════════════════════════════════════════════════════
# RELATIONSHIPS
# ══════════════════════════════════════════════════════════════════════════════
RELATIONS = {
    "R_has_role":        {"pos": ( 525,  200), "label": "has\nrole",        "nullable": False},
    "R_logs":            {"pos": ( 525,  425), "label": "logged\nby",       "nullable": True },
    "R_gives_fb":        {"pos": (1825,  200), "label": "gives\nfeedback",  "nullable": False},
    "R_signs":           {"pos": (1175,  425), "label": "signed\nby",       "nullable": False},
    "R_creates":         {"pos": ( 850,  425), "label": "creates",          "nullable": False},
    "R_contains":        {"pos": (1175,  650), "label": "contains",         "nullable": False},
    "R_plans":           {"pos": (1500,  875), "label": "plans",            "nullable": False},
    "R_takes":           {"pos": (1175,  875), "label": "takes\nsample",    "nullable": False},
    "R_applies_reg":     {"pos": (1500,  980), "label": "applies\nregulation","nullable": False}, 
    "R_ref_plan":        {"pos": (1825, 1100), "label": "references\n(optional)","nullable": True }, 
    "R_defines_limit":   {"pos": (2150, 1325), "label": "defines\nlimit",   "nullable": False}, 
    "R_param_limit":     {"pos": (1825, 1550), "label": "has\nlimit for",   "nullable": False},
    "R_param_plan":      {"pos": (1500, 1325), "label": "planned\nwith",    "nullable": False},
    "R_param_result":    {"pos": (1175, 1550), "label": "measured\nby",     "nullable": False},
    "R_sampler":         {"pos": ( 650,  650), "label": "collects\n(sampler)","nullable": False},
    "R_tester":          {"pos": ( 550,  875), "label": "tested\nby",       "nullable": False},
    "R_changed_by":      {"pos": ( 450, 1100), "label": "changed\nby",      "nullable": False}, 
    "R_has_result":      {"pos": ( 850, 1325), "label": "has\nresult",      "nullable": False},
    "R_entered_by":      {"pos": ( 850, 1775), "label": "history\nof",      "nullable": False},
}

# ══════════════════════════════════════════════════════════════════════════════
# EDGES 
# ══════════════════════════════════════════════════════════════════════════════
EDGES = [
    ("Roles",            "R_has_role",      "1",    "",   False),
    ("R_has_role",       "Users",           "",     "N",  False),
    ("Users",            "R_logs",          "0..1", "",   True ),
    ("R_logs",           "AuditLogs",       "",     "N",  True ),
    ("Customers",        "R_gives_fb",      "1",    "",   False),
    ("R_gives_fb",       "CustomerFeedbacks","",    "N",  False),
    ("Customers",        "R_signs",         "1",    "",   False),
    ("R_signs",          "Contracts",       "",     "N",  False),
    ("Users",            "R_creates",       "1",    "",   False),
    ("R_creates",        "Contracts",       "",     "N",  False),
    ("Contracts",        "R_contains",      "1",    "",   False),
    ("R_contains",       "Orders",          "",     "N",  False),
    ("Orders",           "R_plans",         "1",    "",   False),
    ("R_plans",          "SamplingPlanItems","",    "N",  False),
    ("Orders",           "R_takes",         "1",    "",   False),
    ("R_takes",          "Samples",         "",     "N",  False),
    ("Regulations",      "R_applies_reg",   "1",    "",   False),
    ("R_applies_reg",    "Samples",         "",     "N",  False),
    ("Regulations",      "R_defines_limit", "1",    "",   False),
    ("R_defines_limit",  "RegulationLimits","",     "N",  False),
    ("Regulations",      "R_ref_plan",      "0..1", "",   True ),
    ("R_ref_plan",       "SamplingPlanItems","",    "N",  True ),
    ("TestParameters",   "R_param_limit",   "1",    "",   False),
    ("R_param_limit",    "RegulationLimits","",     "N",  False),
    ("TestParameters",   "R_param_plan",    "1",    "",   False),
    ("R_param_plan",     "SamplingPlanItems","",    "N",  False),
    ("TestParameters",   "R_param_result",  "1",    "",   False),
    ("R_param_result",   "TestResults",     "",     "N",  False),
    ("Users",            "R_sampler",       "1",    "",   False),
    ("R_sampler",        "Samples",         "",     "N",  False),
    ("Users",            "R_tester",        "1",    "",   False),
    ("R_tester",         "TestResults",     "",     "N",  False),
    ("Samples",          "R_has_result",    "1",    "",   False),
    ("R_has_result",     "TestResults",     "",     "N",  False),
    ("TestResults",      "R_entered_by",    "1",    "",   False),
    ("R_entered_by",     "ResultHistory",   "",     "N",  False),
    ("Users",            "R_changed_by",    "1",    "",   False),
    ("R_changed_by",     "ResultHistory",   "",     "N",  False),
]

# ══════════════════════════════════════════════════════════════════════════════
# XML builder
# ══════════════════════════════════════════════════════════════════════════════
def escape_xml(text):
    return xml.sax.saxutils.escape(text)

def attr_positions(entity):
    ex, ey = entity["pos"]
    cx, cy = ex + EW // 2, ey + EH // 2
    n = len(entity["attrs"])
    
    # Bán kính thu hẹp lại (chỉ 160-190px) để vừa ôm khít Entity
    RX, RY = 175, 125
    
    results = []
    start_angle = -math.pi / 4
    
    for i in range(n):
        angle = start_angle + i * (2 * math.pi / n)
        ax_center = cx + RX * math.cos(angle)
        ay_center = cy + RY * math.sin(angle)
        
        ax = ax_center - AW // 2
        ay = ay_center - AH // 2
        results.append((ax, ay, AW, AH))

    return results

def build_xml():
    lines = []
    cid = [2]
    id_map = {}

    def new_id(key=None):
        i = str(cid[0]); cid[0] += 1
        if key: id_map[key] = i
        return i

    lines.append('<?xml version="1.0" encoding="UTF-8"?>')
    lines.append('<mxGraphModel dx="1200" dy="800" grid="1" gridSize="10" '
                 'guides="1" tooltips="1" connect="1" arrows="1" fold="1" '
                 'page="1" pageScale="1" pageWidth="3000" pageHeight="2500" '
                 'math="0" shadow="0">')
    lines.append("  <root>")
    lines.append('    <mxCell id="0"/>')
    lines.append('    <mxCell id="1" parent="0"/>')

    # ── Entities ────────────────────────────────────────────────────────────
    for ename, edata in ENTITIES.items():
        eid = new_id(ename)
        ex, ey = edata["pos"]
        weak   = edata.get("weak", False)
        style  = sty_entity(weak)
        val = escape_xml(ename)
        lines.append(f'    <mxCell id="{eid}" value="{val}" style="{style}" vertex="1" parent="1">')
        lines.append(f'      <mxGeometry x="{ex}" y="{ey}" width="{EW}" height="{EH}" as="geometry"/>')
        lines.append( '    </mxCell>')

        # ── Attributes vòng cung ────────────────────────────────────────────
        positions = attr_positions(edata)
        for i, (aname, akind) in enumerate(edata["attrs"]):
            ax, ay, aw, ah = positions[i]
            aid = new_id()
            id_map[f"_ATTR_{ename}_{aname}"] = aid
            style_a = sty_attr(akind)
            val_attr = escape_xml(aname)
            
            lines.append(f'    <mxCell id="{aid}" value="{val_attr}" style="{style_a}" vertex="1" parent="1">')
            lines.append(f'      <mxGeometry x="{ax}" y="{ay}" width="{aw}" height="{ah}" as="geometry"/>')
            lines.append( '    </mxCell>')

            eid_edge = new_id()
            lines.append(f'    <mxCell id="{eid_edge}" value="" style="{STYLE_EDGE}" edge="1" source="{aid}" target="{eid}" parent="1">')
            lines.append( '      <mxGeometry relative="1" as="geometry"/>')
            lines.append( '    </mxCell>')

    # ── Mối quan hệ hình thoi ───────────────────────────────────────────────
    for rname, rdata in RELATIONS.items():
        rid   = new_id(rname)
        rx, ry = rdata["pos"]
        label  = escape_xml(rdata["label"].replace("\n", " "))
        null   = rdata.get("nullable", False)
        style  = sty_diamond(null)
        lines.append(f'    <mxCell id="{rid}" value="{label}" style="{style}" vertex="1" parent="1">')
        lines.append(f'      <mxGeometry x="{rx}" y="{ry}" width="{DW}" height="{DH}" as="geometry"/>')
        lines.append( '    </mxCell>')

    # ── Edges Các đường nối ──────────────────────────────────────────
    seen = set()
    for (src, tgt, crd_s, crd_t, dash) in EDGES:
        pair = (src, tgt)
        if pair in seen: continue
        seen.add(pair)
        
        if src not in id_map or tgt not in id_map: continue

        eid_e  = new_id()
        style  = STYLE_EDGE_DASH if dash else STYLE_EDGE

        lines.append(f'    <mxCell id="{eid_e}" value="" style="{style}" edge="1" source="{id_map[src]}" target="{id_map[tgt]}" parent="1">')
        lines.append( '      <mxGeometry relative="1" as="geometry"/>')
        lines.append( '    </mxCell>')

        for (crd, rel_pos) in [(crd_s, "-0.8"), (crd_t, "0.8")]:
            if not crd: continue
            cid_label = new_id()
            lines.append(f'    <mxCell id="{cid_label}" value="{escape_xml(crd)}" style="{STYLE_CARD}" connectable="0" vertex="1" parent="{eid_e}">')
            lines.append(f'      <mxGeometry x="{rel_pos}" y="0" relative="1" as="geometry"/>')
            lines.append( '    </mxCell>')

    lines.append("  </root>")
    lines.append("</mxGraphModel>")
    return "\n".join(lines)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    script_dir = os.path.dirname(os.path.abspath(__file__))
    out_path   = os.path.join(script_dir, "ECOVA_Chen_ERD.drawio")

    print("=== Generating Compact Layout BLACK & WHITE Chen ERD ===")
    xml = build_xml()
    with open(out_path, "w", encoding="utf-8") as f:
        f.write(xml)
    print(f"[OK] Saved: {out_path}")
