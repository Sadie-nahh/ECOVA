#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
puml_to_drawio.py
=================
Chuyển file PlantUML ERD (dạng class/entity) sang file .drawio (XML)
để import vào draw.io (app.diagrams.net) và chỉnh sửa thủ công.

Cách dùng:
    python puml_to_drawio.py <input.puml> <output.drawio>

Ví dụ:
    python puml_to_drawio.py 01_ERD.puml 01_ERD.drawio

Sau đó mở draw.io → File → Import → Chọn file 01_ERD.drawio
"""

import re
import sys
import xml.etree.ElementTree as ET
from xml.dom import minidom
import math


# ─────────────────────────────────────────────────────────────
# CONFIG — vị trí layout tự động (grid-based)
# ─────────────────────────────────────────────────────────────
ENTITY_WIDTH     = 320   # px chiều rộng mỗi bảng
ENTITY_HEADER_H  = 32    # px chiều cao header (tên bảng)
ATTR_ROW_H       = 20    # px chiều cao mỗi dòng thuộc tính
SECTION_GAP_H    = 8     # px khoảng cách giữa các section "--"
COL_GAP          = 60    # px khoảng cách ngang giữa các bảng
ROW_GAP          = 80    # px khoảng cách dọc giữa các bảng
COLS_PER_ROW     = 4     # số bảng trên 1 hàng


# ─────────────────────────────────────────────────────────────
# STYLE CONSTANTS — draw.io mxCell styles
# ─────────────────────────────────────────────────────────────
STYLE_ENTITY = (
    "shape=table;startSize=32;container=1;collapsible=0;childLayout=tableLayout;"
    "fixedRows=1;rowLines=0;fontStyle=1;align=center;resizeLast=1;"
    "fillColor=#1A252F;fontColor=#ffffff;strokeColor=#1A252F;fontSize=11;"
)

STYLE_ATTR_PK = (
    "shape=tableRow;horizontal=0;startSize=0;swimlaneHead=0;swimlaneBody=0;"
    "fillColor=#EBF5FB;strokeColor=#1A252F;top=0;left=0;right=0;bottom=1;"
    "fontStyle=5;fontSize=10;align=left;"   # fontStyle=5 = bold+underline
)

STYLE_ATTR_FK = (
    "shape=tableRow;horizontal=0;startSize=0;swimlaneHead=0;swimlaneBody=0;"
    "fillColor=#FEF9E7;strokeColor=#1A252F;top=0;left=0;right=0;bottom=0;"
    "fontStyle=2;fontSize=10;align=left;"   # fontStyle=2 = italic
)

STYLE_ATTR_SECTION = (
    "shape=tableRow;horizontal=0;startSize=0;swimlaneHead=0;swimlaneBody=0;"
    "fillColor=#D5D8DC;strokeColor=#1A252F;top=0;left=0;right=0;bottom=0;"
    "fontSize=9;align=left;fontStyle=0;"
)

STYLE_ATTR_NORMAL = (
    "shape=tableRow;horizontal=0;startSize=0;swimlaneHead=0;swimlaneBody=0;"
    "fillColor=#FDFEFE;strokeColor=#1A252F;top=0;left=0;right=0;bottom=0;"
    "fontSize=10;align=left;"
)

STYLE_EDGE = (
    "edgeStyle=entityRelationEdgeStyle;endArrow=ERzeroToMany;"
    "startArrow=ERmandOne;exitX=1;exitY=0.5;entryX=0;entryY=0.5;"
    "strokeColor=#1A252F;fontColor=#555555;fontSize=9;"
)

STYLE_EDGE_OPTIONAL = (
    "edgeStyle=entityRelationEdgeStyle;endArrow=ERzeroToMany;"
    "startArrow=ERzeroToOne;exitX=1;exitY=0.5;entryX=0;entryY=0.5;"
    "strokeColor=#1A252F;fontColor=#555555;fontSize=9;dashed=1;"
)


# ─────────────────────────────────────────────────────────────
# PARSE PLANTUML
# ─────────────────────────────────────────────────────────────

def parse_puml(filepath: str):
    """
    Parse file .puml và trả về:
      entities  = [ {name, alias, rows: [ {kind, text} ] } ]
      relations = [ {from_alias, to_alias, from_mult, to_mult, label} ]
    """
    with open(filepath, encoding='utf-8') as f:
        content = f.read()

    entities  = []
    relations = []

    # ── Entity / Class blocks ──
    # Hỗ trợ cả: entity "Name" as Alias { ... }
    #             class  "Name" as Alias { ... }
    block_re = re.compile(
        r'(?:entity|class)\s+"([^"]+)"\s+as\s+(\w+)(?:\s+<<[^>]*>>)?\s*\{([^}]*)\}',
        re.DOTALL
    )
    for m in block_re.finditer(content):
        name, alias, body = m.group(1).strip(), m.group(2).strip(), m.group(3)
        rows = []
        for line in body.splitlines():
            line = line.strip()
            if not line:
                continue
            # section separator "--"
            if re.match(r'^--+$', line):
                rows.append({'kind': 'section', 'text': ''})
                continue
            # detect PK (bold+underline markers from PlantUML: <b><u>.....</u></b>)
            if re.search(r'<b><u>|<u><b>|\bPK\b', line, re.IGNORECASE):
                text = re.sub(r'<[^>]+>', '', line).strip()
                text = re.sub(r'\bPK\b\s*', '', text).strip()
                rows.append({'kind': 'pk', 'text': text})
            # detect FK (italic markers or FK keyword)
            elif re.search(r'<i>|</i>|\bFK\b', line, re.IGNORECASE):
                text = re.sub(r'<[^>]+>', '', line).strip()
                text = re.sub(r'\bFK\b\s*', '', text).strip()
                rows.append({'kind': 'fk', 'text': text})
            # comments / metadata lines starting with CK/IDX/TRIGGER/NOTE/NAMED
            elif re.match(r'^(?:CK|IDX|TRIGGER|NOTE|NAMED|<<|//)', line, re.IGNORECASE):
                text = re.sub(r'<[^>]+>', '', line).strip()
                rows.append({'kind': 'meta', 'text': text})
            else:
                text = re.sub(r'<[^>]+>', '', line).strip()
                if text:
                    rows.append({'kind': 'attr', 'text': text})

        entities.append({'name': name, 'alias': alias, 'rows': rows})

    # ── Relationships ──
    # Formats:
    #   A  "1"  --o{  "0..*"  B  : "label"
    #   A  ||--o{  B  :  "label"
    rel_re = re.compile(
        r'(\w+)\s+"?([^"\s]+)"?\s+([\|\-o]+)\s*-+\s*([\|\-o{]+)\s+"?([^"\s:]+)"?\s+(\w+)'
        r'\s*:\s*"([^"]*)"',
        re.MULTILINE
    )
    for m in rel_re.finditer(content):
        from_a   = m.group(1).strip()
        from_m   = m.group(2).strip()
        to_m     = m.group(5).strip()
        to_a     = m.group(6).strip()
        label    = m.group(7).strip()
        relations.append({
            'from': from_a, 'to': to_a,
            'from_mult': from_m, 'to_mult': to_m,
            'label': label,
            'optional': '0..1' in from_m or 'o|' in m.group(3)
        })

    # Fallback simpler pattern (PlantUML IE style: A ||--o{ B : "label")
    rel_simple_re = re.compile(
        r'^(\w+)\s+([\|o\-{]+)\s*-+\s*([\|o\-{]+)\s+(\w+)\s*:\s*"([^"]*)"',
        re.MULTILINE
    )
    existing = {(r['from'], r['to']) for r in relations}
    for m in rel_simple_re.finditer(content):
        fa, ra = m.group(1).strip(), m.group(4).strip()
        if (fa, ra) not in existing:
            lhs, rhs = m.group(2), m.group(3)
            optional = 'o|' in lhs or lhs.startswith('o')
            relations.append({
                'from': fa, 'to': ra,
                'from_mult': '0..1' if optional else '1',
                'to_mult': '0..*',
                'label': m.group(5).strip(),
                'optional': optional
            })
            existing.add((fa, ra))

    return entities, relations


# ─────────────────────────────────────────────────────────────
# COMPUTE LAYOUT
# ─────────────────────────────────────────────────────────────

def compute_layout(entities):
    """Gán (x, y, width, height) cho từng entity theo grid."""
    layout = {}
    for i, ent in enumerate(entities):
        col = i % COLS_PER_ROW
        row = i // COLS_PER_ROW

        # compute height based on number of rows
        n_attrs   = sum(1 for r in ent['rows'] if r['kind'] in ('pk','fk','attr'))
        n_meta    = sum(1 for r in ent['rows'] if r['kind'] == 'meta')
        n_section = sum(1 for r in ent['rows'] if r['kind'] == 'section')

        height = (ENTITY_HEADER_H
                  + n_attrs * ATTR_ROW_H
                  + n_meta  * (ATTR_ROW_H - 4)
                  + n_section * SECTION_GAP_H)

        # x, y  — coarse grid; each row uses max-height of that row
        x = col * (ENTITY_WIDTH + COL_GAP)
        layout[ent['alias']] = {
            'col': col, 'row': row,
            'x': x,
            'height': height,
        }

    # compute y offset per grid-row
    row_max_h = {}
    for alias, d in layout.items():
        r = d['row']
        row_max_h[r] = max(row_max_h.get(r, 0), d['height'])

    row_y = {}
    acc = 60   # top margin
    for r in sorted(row_max_h):
        row_y[r] = acc
        acc += row_max_h[r] + ROW_GAP

    for alias in layout:
        layout[alias]['y'] = row_y[layout[alias]['row']]
        layout[alias]['width'] = ENTITY_WIDTH

    return layout


# ─────────────────────────────────────────────────────────────
# BUILD draw.io XML
# ─────────────────────────────────────────────────────────────

def build_drawio(entities, relations, layout):
    """Tạo cây XML mxGraphModel cho draw.io."""
    root_el = ET.Element('mxGraphModel',
        dx='1422', dy='762', grid='1', gridSize='10',
        guides='1', tooltips='1', connect='1', arrows='1',
        fold='1', page='1', pageScale='1',
        pageWidth='1654', pageHeight='1169',   # A3 landscape
        math='0', shadow='0')
    root_elem = ET.SubElement(root_el, 'root')

    # required default cells
    ET.SubElement(root_elem, 'mxCell', id='0')
    ET.SubElement(root_elem, 'mxCell', id='1', parent='0')

    cell_id  = 2      # auto-increment ID counter
    alias_id = {}     # alias → cell_id of parent table

    # ── Entities ──
    for ent in entities:
        alias  = ent['alias']
        name   = ent['name']
        L      = layout[alias]

        # --- Table container ---
        table_id   = str(cell_id); cell_id += 1
        alias_id[alias] = table_id

        table_cell = ET.SubElement(root_elem, 'mxCell',
            id=table_id, value=f'<b>{name}</b>',
            style=STYLE_ENTITY, vertex='1', parent='1')
        ET.SubElement(table_cell, 'mxGeometry',
            x=str(L['x']), y=str(L['y']),
            width=str(L['width']), height=str(L['height']),
            **{'as': 'geometry'})

        # --- Attribute rows ---
        for rdata in ent['rows']:
            kind = rdata['kind']
            text = rdata['text']

            row_id = str(cell_id); cell_id += 1

            if kind == 'section':
                style = STYLE_ATTR_SECTION
                row_h = SECTION_GAP_H
                display_text = '<hr/>'
            elif kind == 'pk':
                style = STYLE_ATTR_PK
                row_h = ATTR_ROW_H
                display_text = f'<b><u>{text}</u></b>'
            elif kind == 'fk':
                style = STYLE_ATTR_FK
                row_h = ATTR_ROW_H
                display_text = f'<i>{text}</i>'
            elif kind == 'meta':
                style = STYLE_ATTR_SECTION
                row_h = ATTR_ROW_H - 4
                display_text = f'<font color="#555555" style="font-size:8px">{text}</font>'
            else:
                style = STYLE_ATTR_NORMAL
                row_h = ATTR_ROW_H
                display_text = text

            row_cell = ET.SubElement(root_elem, 'mxCell',
                id=row_id, value=display_text,
                style=style, vertex='1', parent=table_id)
            ET.SubElement(row_cell, 'mxGeometry',
                y='0', width=str(L['width']), height=str(row_h),
                **{'as': 'geometry'})

    # ── Relationships / Edges ──
    for rel in relations:
        fa, ta  = rel['from'], rel['to']
        if fa not in alias_id or ta not in alias_id:
            continue   # skip unresolved aliases

        edge_id = str(cell_id); cell_id += 1
        label   = rel['label'].replace('\n', ' ')
        style   = STYLE_EDGE_OPTIONAL if rel.get('optional') else STYLE_EDGE

        # Annotate multiplicity on label
        mult_label = f'{rel["from_mult"]} — {rel["to_mult"]}\n{label}'

        edge_cell = ET.SubElement(root_elem, 'mxCell',
            id=edge_id,
            value=mult_label,
            style=style,
            edge='1',
            source=alias_id[fa],
            target=alias_id[ta],
            parent='1')
        ET.SubElement(edge_cell, 'mxGeometry', relative='1', **{'as': 'geometry'})

    return root_el


# ─────────────────────────────────────────────────────────────
# PRETTY PRINT XML → .drawio
# ─────────────────────────────────────────────────────────────

def write_drawio(xml_root, output_path: str):
    raw = ET.tostring(xml_root, encoding='unicode')
    pretty = minidom.parseString(raw).toprettyxml(indent='  ', encoding='UTF-8')
    # minidom adds XML declaration — drawio needs it
    with open(output_path, 'wb') as f:
        f.write(pretty)
    print(f"[OK] Written: {output_path}")


# ─────────────────────────────────────────────────────────────
# MAIN
# ─────────────────────────────────────────────────────────────

def main():
    if len(sys.argv) < 3:
        print("Usage: python puml_to_drawio.py <input.puml> <output.drawio>")
        print("Example:")
        print("  python puml_to_drawio.py 01_ERD.puml 01_ERD.drawio")
        sys.exit(1)

    input_puml   = sys.argv[1]
    output_drawio = sys.argv[2]

    print(f"[1/4] Parsing PlantUML: {input_puml}")
    entities, relations = parse_puml(input_puml)
    print(f"      → {len(entities)} entities, {len(relations)} relations")

    if not entities:
        print("[WARN] No entities found. Check your .puml format.")
        print("       Supported: class/entity \"Name\" as Alias { ... }")
        sys.exit(1)

    print(f"[2/4] Computing layout ({COLS_PER_ROW} columns per row)...")
    layout = compute_layout(entities)

    print(f"[3/4] Building draw.io XML...")
    xml_tree = build_drawio(entities, relations, layout)

    print(f"[4/4] Writing output: {output_drawio}")
    write_drawio(xml_tree, output_drawio)

    print()
    print("=" * 55)
    print(" DONE! Import vao draw.io:")
    print("   1. Mo app.diagrams.net")
    print("   2. File -> Import -> Chon file .drawio")
    print("   3. Chinh sua thu cong theo y muon")
    print("=" * 55)


if __name__ == '__main__':
    main()
