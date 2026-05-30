#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
puml_state_to_drawio.py  ·  v2.0  (Traditional B&W UML 2.5 State Diagram)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Converts PlantUML State Diagrams (.puml) → draw.io XML (.drawio)

v2.0 improvements over v1.0:
  • Correct UML bullseye final state  (outer white ring + inner black dot)
  • Smart edge routing  :  forward  ↓ straight,  backward ↑ left-side U-curve,
                           same-level ← → horizontal,  self-loop ↺ right side
  • Bidirectional pair detection → offset left/right so both arrows are visible
  • BFS level tracking exposed to generator for direction classification
  • Larger spacing  (V_GAP 170, H_GAP 145) to reduce label overlap
  • Composite interior uses smaller gaps (COMP_V_GAP 85, COMP_H_GAP 80)
  • Transition labels get white background (readable over crossing edges)
  • Note connectors attach from note-edge to state-edge (not centre-to-centre)
  • Anti-overlap note shifting (stacks notes that would collide)

Supported PUML syntax:
  state "Label" as alias
  state "Label" as alias { ... }      ← composite/region
  [*] --> A : event [guard] / action  ← initial pseudostate
  A  --> [*] : label                  ← final pseudostate
  A  --> B   : label                  ← regular transition
  A  --> A   : label                  ← self-transition (loop)
  note right of X / left of X / bottom of X / on link ... end note
  skinparam { ... }  (ignored)
  title ...

Output style: strict Black & White UML 2.5
  • States      : white stadium (arcSize=50), 1.5pt black stroke
  • Composite   : white rounded rect, bold label top-left, 1.5pt stroke
  • Initial [*] : filled black circle  (28 × 28)
  • Final   [*] : bullseye = outer white ring (36 × 36, 3pt stroke)
                           + inner black dot (16 × 16), centred
  • Transitions : orthogonal, filled block arrowhead, 1.3pt black stroke
  • Self-loops  : right-side rectangular loop
  • Notes       : white dog-ear rectangle, black border

Usage:
  python puml_state_to_drawio.py  input.puml
  python puml_state_to_drawio.py  input.puml  output.drawio
  python puml_state_to_drawio.py  "E:\\...\\Diagrams"    # batch 05*.puml

Requirements: Python ≥ 3.9 (stdlib only)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"""

from __future__ import annotations

import re, sys, xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Set, Tuple
from pathlib import Path
from collections import defaultdict, deque

# ── Windows cp1252 console fix ────────────────────────────────────────────────
if sys.stdout.encoding and sys.stdout.encoding.lower() not in ("utf-8", "utf_8"):
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")


# ═══════════════════════════════════════════════════════════════════════════════
#  LAYOUT CONSTANTS
# ═══════════════════════════════════════════════════════════════════════════════

MARGIN        = 80      # canvas margin

# ── Initial / Final pseudostates ──────────────────────────────────────────────
INIT_D        = 28      # initial black circle diameter
FINAL_D       = 36      # final bullseye outer ring diameter
FINAL_DOT_D   = 16      # final bullseye inner dot diameter

# ── Regular states ────────────────────────────────────────────────────────────
STATE_W       = 260     # default state width
STATE_H_MIN   = 60      # minimum state height
STATE_LINE_H  = 18      # extra height per label line beyond first

# ── Composite states ──────────────────────────────────────────────────────────
COMP_PAD_X    = 45      # horizontal inner padding
COMP_PAD_TOP  = 68      # top padding  (leaves room for composite label)
COMP_PAD_BOT  = 36      # bottom padding

# ── Top-level spacing ─────────────────────────────────────────────────────────
H_GAP         = 145     # horizontal gap between sibling nodes
V_GAP         = 175     # vertical gap between diagram levels

# ── Composite interior spacing (more compact) ─────────────────────────────────
COMP_H_GAP    = 80      # horizontal gap inside composite
COMP_V_GAP    = 85      # vertical gap inside composite

# ── Notes ─────────────────────────────────────────────────────────────────────
NOTE_W        = 250     # note width
NOTE_MIN_H    = 55      # minimum note height
NOTE_LINE_H   = 14      # px per line of text
NOTE_GAP      = 36      # gap between note edge and state edge

# ── Self-transition loop ──────────────────────────────────────────────────────
SELF_W        = 70      # loop horizontal extent beyond state right edge

# ── Backward edge routing ─────────────────────────────────────────────────────
BACK_OFFSET   = 90      # distance left of leftmost state for backward routing


# ═══════════════════════════════════════════════════════════════════════════════
#  DRAW.IO STYLE STRINGS  (strict B&W UML 2.5)
# ═══════════════════════════════════════════════════════════════════════════════

_S_STATE = (
    "rounded=1;arcSize=50;whiteSpace=wrap;html=1;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1.5;"
    "fontSize=10;fontStyle=0;align=center;"
    "spacingTop=6;spacingLeft=6;spacingRight=6;"
)
_S_COMPOSITE = (
    "rounded=1;arcSize=8;whiteSpace=wrap;html=1;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1.5;"
    "fontSize=10;fontStyle=1;verticalAlign=top;align=left;"
    "spacingLeft=10;spacingTop=8;"
    "container=1;collapsible=0;expand=0;"
)
_S_INITIAL = (
    "ellipse;fillColor=#000000;strokeColor=#000000;"
    "strokeWidth=1;fontSize=1;aspect=fixed;pointerEvents=1;"
)
# Final state outer ring (white fill, thick black border)
_S_FINAL_RING = (
    "ellipse;fillColor=#ffffff;strokeColor=#000000;"
    "strokeWidth=3;fontSize=1;aspect=fixed;pointerEvents=1;"
)
# Final state inner dot (black filled, no border visible)
_S_FINAL_DOT = (
    "ellipse;fillColor=#000000;strokeColor=#000000;"
    "strokeWidth=1;fontSize=1;aspect=fixed;pointerEvents=0;"
)

# ── Base edge style shared by all transition types ────────────────────────────
_BASE_EDGE = (
    "endArrow=block;endFill=1;html=1;dashed=0;"
    "strokeColor=#000000;strokeWidth=1.3;"
    "fontSize=8;fontStyle=0;"
    "labelBackgroundColor=#ffffff;labelBorderColor=none;"
    "edgeStyle=orthogonalEdgeStyle;rounded=1;"
)
# Forward (down the diagram, centre exit/entry)
_S_FWD = _BASE_EDGE + "align=center;"
# Forward in a bidirectional pair — offset right so both arrows are visible
_S_FWD_BIDIR = _BASE_EDGE + (
    "align=center;"
    "exitX=0.65;exitY=1;exitDx=0;exitDy=0;"
    "entryX=0.65;entryY=0;entryDx=0;entryDy=0;"
)
# Backward (up the diagram, exits/enters left edge)
_S_BACK = _BASE_EDGE + (
    "align=left;"
    "exitX=0;exitY=0.5;exitDx=0;exitDy=0;"
    "entryX=0;entryY=0.5;entryDx=0;entryDy=0;"
)
# Same-level → right    (exits right, enters left)
_S_SIDE_R = _BASE_EDGE + (
    "align=center;"
    "exitX=1;exitY=0.5;exitDx=0;exitDy=0;"
    "entryX=0;entryY=0.5;entryDx=0;entryDy=0;"
)
# Same-level ← left     (exits left, enters right)
_S_SIDE_L = _BASE_EDGE + (
    "align=center;"
    "exitX=0;exitY=0.5;exitDx=0;exitDy=0;"
    "entryX=1;entryY=0.5;entryDx=0;entryDy=0;"
)
# Self-loop (right side)
_S_SELF = _BASE_EDGE + (
    "align=left;"
    "exitX=1;exitY=0.3;exitDx=0;exitDy=0;"
    "entryX=1;entryY=0.7;entryDx=0;entryDy=0;"
)

_S_NOTE = (
    "shape=note;whiteSpace=wrap;html=1;backgroundOutline=1;size=12;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
    "fontSize=8;align=left;"
    "spacingLeft=8;spacingTop=6;spacingRight=6;verticalAlign=top;"
)
_S_NOTE_LINK = (
    "endArrow=none;html=1;dashed=1;"
    "strokeColor=#888888;strokeWidth=1;"
)
_S_TITLE = (
    "text;html=1;align=center;strokeColor=none;fillColor=none;"
    "fontSize=13;fontStyle=1;"
)


# ═══════════════════════════════════════════════════════════════════════════════
#  DATA CLASSES
# ═══════════════════════════════════════════════════════════════════════════════

@dataclass
class StateNode:
    alias:      str
    label:      str
    parent:     Optional[str] = None
    sub_states: List[str]     = field(default_factory=list)
    x: float = 0.0
    y: float = 0.0
    w: float = STATE_W
    h: float = STATE_H_MIN


@dataclass
class PseudoNode:
    alias:  str
    kind:   str          # "initial" | "final"
    parent: Optional[str] = None
    x: float = 0.0
    y: float = 0.0
    w: float = INIT_D
    h: float = INIT_D


@dataclass
class TransitionDef:
    src:   str
    dst:   str
    label: str


@dataclass
class NoteDef:
    text:          str
    position:      str   # "right of" | "left of" | "bottom of" | "top of" | "on link"
    target:        str   # state alias or ""
    trans_idx:     int   = -1  # index into transitions list for "on link" notes


# ═══════════════════════════════════════════════════════════════════════════════
#  PUML PARSER
# ═══════════════════════════════════════════════════════════════════════════════

_R_TITLE       = re.compile(r'^title\s+(.+)$', re.I)
_R_STATE_COMP  = re.compile(r'^state\s+"([^"]+)"\s+as\s+(\w+)\s*\{', re.I)
_R_STATE_AS    = re.compile(r'^state\s+"([^"]+)"\s+as\s+(\w+)\s*$', re.I)
_R_TRANS       = re.compile(
    r'^(\[\*\]|\w+)\s*(?:-->|->)\s*(\[\*\]|\w+)\s*(?::\s*(.*?))?$'
)
_R_NOTE_INLINE = re.compile(
    r'^note\s+(right of|left of|bottom of|top of|right|left|bottom|top|on link)'
    r'\s+(\w+)\s*:\s*(.+)$', re.I
)
_R_NOTE_BLOCK  = re.compile(
    r'^note\s+(right of|left of|bottom of|top of|right|left|bottom|top|on link)'
    r'(?:\s+(\w+))?\s*$', re.I
)
_R_NOTE_END    = re.compile(r'^end\s+note$', re.I)
_R_SKIP        = re.compile(r"^(@startuml|@enduml|shadowing|//|')", re.I)
_R_SKINBLK     = re.compile(r'^skinparam\b', re.I)


class PumlParser:

    def __init__(self):
        self.title:         str                      = "State Diagram"
        self.states:        Dict[str, StateNode]     = {}
        self.pseudos:       Dict[str, PseudoNode]    = {}
        self.transitions:   List[TransitionDef]      = []
        self.notes:         List[NoteDef]            = []
        self._state_order:  List[str]                = []
        self._pseudo_order: List[str]                = []
        self._init_ctr  = 0
        self._final_ctr = 0

    # ── helpers ───────────────────────────────────────────────────────────────

    def _ensure_state(self, alias: str, label: str = "",
                      parent: Optional[str] = None) -> str:
        if alias not in self.states:
            h = self._calc_h(label or alias)
            self.states[alias] = StateNode(
                alias=alias, label=label or alias,
                parent=parent, w=STATE_W, h=h
            )
            self._state_order.append(alias)
        else:
            s = self.states[alias]
            if label and (not s.label or s.label == alias):
                s.label = label
                s.h = self._calc_h(label)
            if parent and s.parent is None:
                s.parent = parent
        if parent:
            comp = self.states.get(parent)
            if comp and alias not in comp.sub_states:
                comp.sub_states.append(alias)
        return alias

    def _make_init(self, parent: Optional[str] = None) -> str:
        alias = f"__init__{self._init_ctr}"
        self._init_ctr += 1
        ps = PseudoNode(alias=alias, kind="initial", parent=parent,
                        w=INIT_D, h=INIT_D)
        self.pseudos[alias] = ps
        self._pseudo_order.append(alias)
        return alias

    def _make_final(self, parent: Optional[str] = None) -> str:
        alias = f"__final__{self._final_ctr}"
        self._final_ctr += 1
        ps = PseudoNode(alias=alias, kind="final", parent=parent,
                        w=FINAL_D, h=FINAL_D)
        self.pseudos[alias] = ps
        self._pseudo_order.append(alias)
        return alias

    @staticmethod
    def _calc_h(label: str) -> float:
        n = label.replace("\\n", "\n").count("\n") + 1
        return STATE_H_MIN + max(0, n - 1) * STATE_LINE_H

    # ── parse entry ───────────────────────────────────────────────────────────

    def parse(self, text: str):
        lines = text.splitlines()
        self._parse_block(lines, 0, len(lines), parent=None)

    # ── recursive block parser ────────────────────────────────────────────────

    def _parse_block(self, lines: List[str], start: int, end: int,
                     parent: Optional[str]) -> int:
        i = start
        while i < end:
            raw = lines[i].strip()
            i += 1
            if not raw:
                continue

            # title
            m = _R_TITLE.match(raw)
            if m:
                self.title = m.group(1).strip()
                continue

            # skip comments / @startuml / @enduml
            if _R_SKIP.match(raw):
                continue

            # skinparam block  (may have braces or be one-liner)
            if _R_SKINBLK.match(raw):
                if "{" in raw:
                    depth = raw.count("{")
                    while i < end and depth > 0:
                        ch = lines[i].strip()
                        i += 1
                        depth += ch.count("{") - ch.count("}")
                continue

            # composite state: state "label" as alias {
            m = _R_STATE_COMP.match(raw)
            if m:
                label, alias = m.group(1), m.group(2)
                self._ensure_state(alias, label, parent)
                depth = 1
                block_start = i
                while i < end and depth > 0:
                    stripped = lines[i].strip()
                    depth += stripped.count("{") - stripped.count("}")
                    i += 1
                self._parse_block(lines, block_start, i - 1, parent=alias)
                continue

            # simple state: state "label" as alias
            m = _R_STATE_AS.match(raw)
            if m:
                self._ensure_state(m.group(2), m.group(1), parent)
                continue

            # closing brace
            if raw in ("}", "];"):
                continue

            # note inline
            m = _R_NOTE_INLINE.match(raw)
            if m:
                pos    = m.group(1).lower().strip()
                target = (m.group(2) or "").strip()
                # For "on link", record the index of the transition just added
                tidx = len(self.transitions) - 1 if (pos == "on link" or not target) else -1
                self.notes.append(NoteDef(text=m.group(3).strip(),
                                          position=pos, target=target,
                                          trans_idx=tidx))
                continue

            # note block
            m = _R_NOTE_BLOCK.match(raw)
            if m:
                pos    = m.group(1).lower().strip()
                target = (m.group(2) or "").strip()
                body: List[str] = []
                while i < end:
                    nl = lines[i].strip()
                    i += 1
                    if _R_NOTE_END.match(nl):
                        break
                    body.append(nl)
                # Record transition index BEFORE reading the block (i.e., transition just before note)
                tidx = len(self.transitions) - 1 if (pos == "on link" or not target) else -1
                self.notes.append(NoteDef(text="\n".join(body),
                                          position=pos, target=target,
                                          trans_idx=tidx))
                continue

            # transition
            m = _R_TRANS.match(raw)
            if m:
                src_raw, dst_raw = m.group(1), m.group(2)
                lbl = (m.group(3) or "").strip()
                src = (self._make_init(parent) if src_raw == "[*]"
                       else self._ensure_state(src_raw, parent=parent))
                dst = (self._make_final(parent) if dst_raw == "[*]"
                       else self._ensure_state(dst_raw, parent=parent))
                self.transitions.append(TransitionDef(src=src, dst=dst, label=lbl))
                continue

        return i


# ═══════════════════════════════════════════════════════════════════════════════
#  LAYOUTER
# ═══════════════════════════════════════════════════════════════════════════════

class Layouter:

    def __init__(self, parser: PumlParser):
        self.p = parser
        self.node_levels: Dict[str, int] = {}   # top-level BFS levels

    # ── PUBLIC entry ──────────────────────────────────────────────────────────

    def layout(self):
        """
        Phase 1: bottom-up sizing of composite states.
        Phase 2: BFS-level grid layout of top-level nodes (absolute coords).
        Phase 3: absolutize sub-state positions.
        """
        # Phase 1
        for alias in self.p._state_order:
            s = self.p.states[alias]
            if s.sub_states and s.parent is None:
                self._size_composite(alias)

        # Phase 2
        self._layout_scope(scope=None,
                           origin_x=MARGIN, origin_y=MARGIN + 40,
                           h_gap=H_GAP, v_gap=V_GAP,
                           store_levels=True)

        # Phase 3
        for alias in self.p._state_order:
            s = self.p.states[alias]
            if s.sub_states and s.parent is None:
                self._absolutize(alias,
                                  s.x + COMP_PAD_X,
                                  s.y + COMP_PAD_TOP)

    # ── composite sizing ──────────────────────────────────────────────────────

    def _size_composite(self, alias: str):
        comp = self.p.states[alias]
        for sub in comp.sub_states:
            s2 = self.p.states.get(sub)
            if s2 and s2.sub_states:
                self._size_composite(sub)
        cw, ch = self._layout_scope(scope=alias,
                                    origin_x=0.0, origin_y=0.0,
                                    h_gap=COMP_H_GAP, v_gap=COMP_V_GAP,
                                    store_levels=False)
        comp.w = cw + 2 * COMP_PAD_X
        comp.h = ch + COMP_PAD_TOP + COMP_PAD_BOT

    # ── generic scope layout ──────────────────────────────────────────────────

    def _layout_scope(self, scope: Optional[str],
                      origin_x: float, origin_y: float,
                      h_gap: float, v_gap: float,
                      store_levels: bool) -> Tuple[float, float]:
        p = self.p

        state_nodes  = [a for a in p._state_order  if p.states[a].parent == scope]
        pseudo_nodes = [a for a in p._pseudo_order if p.pseudos[a].parent == scope]
        all_nodes    = pseudo_nodes + state_nodes
        if not all_nodes:
            return STATE_W, STATE_H_MIN

        node_set = set(all_nodes)

        # Build adjacency (exclude self-loops)
        adj: Dict[str, List[str]] = defaultdict(list)
        for t in p.transitions:
            s = t.src if t.src in node_set else None
            d = t.dst if t.dst in node_set else None
            if s and d and s != d:
                adj[s].append(d)

        # BFS level assignment from initial pseudostates
        level: Dict[str, int] = {}
        queue: deque = deque()
        for a in pseudo_nodes:
            if p.pseudos[a].kind == "initial":
                level[a] = 0
                queue.append(a)
        while queue:
            curr = queue.popleft()
            for nxt in adj[curr]:
                if nxt not in level:
                    level[nxt] = level[curr] + 1
                    queue.append(nxt)

        # Assign unreached nodes
        max_lv = max(level.values(), default=0)
        for a in all_nodes:
            if a not in level:
                level[a] = max_lv + 1

        if store_levels:
            self.node_levels.update(level)

        # Group by level
        by_level: Dict[int, List[str]] = defaultdict(list)
        for a, lv in level.items():
            by_level[lv].append(a)

        # Sort within level by definition order
        s_ord  = {a: i for i, a in enumerate(p._state_order)}
        ps_ord = {a: i for i, a in enumerate(p._pseudo_order)}

        def _key(a: str) -> int:
            return s_ord.get(a, ps_ord.get(a, 999))

        for lv in by_level:
            by_level[lv].sort(key=_key)

        # Build row info
        num_levels = max(by_level.keys(), default=0) + 1
        rows: List[Tuple[List[str], float, float]] = []
        for lv in range(num_levels):
            ns = by_level.get(lv, [])
            if not ns:
                continue
            rw = sum(self._nw(a) for a in ns) + max(0, len(ns) - 1) * h_gap
            rh = max(self._nh(a) for a in ns)
            rows.append((ns, rw, rh))

        canvas_w = max((r[1] for r in rows), default=STATE_W)

        # Place nodes
        y = origin_y
        for ns, rw, rh in rows:
            x = origin_x + (canvas_w - rw) / 2   # centre-align this row
            for a in ns:
                nw = self._nw(a)
                nh = self._nh(a)
                self._set_pos(a, x, y + (rh - nh) / 2, nw, nh)
                x += nw + h_gap
            y += rh + v_gap

        total_h = (y - v_gap - origin_y) if rows else STATE_H_MIN
        return canvas_w, total_h

    # ── absolutize ────────────────────────────────────────────────────────────

    def _absolutize(self, comp_alias: str, off_x: float, off_y: float):
        p = self.p
        comp = p.states[comp_alias]
        for sub in comp.sub_states:
            s = p.states[sub]
            s.x += off_x
            s.y += off_y
            if s.sub_states:
                self._absolutize(sub, s.x + COMP_PAD_X, s.y + COMP_PAD_TOP)
        for ps in p.pseudos.values():
            if ps.parent == comp_alias:
                ps.x += off_x
                ps.y += off_y

    # ── helpers ───────────────────────────────────────────────────────────────

    def _nw(self, a: str) -> float:
        if a in self.p.states:  return self.p.states[a].w
        if a in self.p.pseudos: return self.p.pseudos[a].w
        return STATE_W

    def _nh(self, a: str) -> float:
        if a in self.p.states:  return self.p.states[a].h
        if a in self.p.pseudos: return self.p.pseudos[a].h
        return STATE_H_MIN

    def _set_pos(self, a: str, x: float, y: float, w: float, h: float):
        if a in self.p.states:
            s = self.p.states[a]
            s.x, s.y, s.w, s.h = x, y, w, h
        elif a in self.p.pseudos:
            ps = self.p.pseudos[a]
            ps.x, ps.y, ps.w, ps.h = x, y, w, h

    def total_bounds(self) -> Tuple[float, float]:
        xs, ys = [], []
        for s in self.p.states.values():
            if s.parent is None:
                xs.append(s.x + s.w)
                ys.append(s.y + s.h)
        for ps in self.p.pseudos.values():
            if ps.parent is None:
                xs.append(ps.x + ps.w)
                ys.append(ps.y + ps.h)
        return (
            (max(xs) + MARGIN) if xs else 800,
            (max(ys) + MARGIN) if ys else 600
        )


# ═══════════════════════════════════════════════════════════════════════════════
#  DRAW.IO XML GENERATOR
# ═══════════════════════════════════════════════════════════════════════════════

class DrawioGenerator:

    def __init__(self, parser: PumlParser, layout: Layouter):
        self.p = parser
        self.l = layout
        self._id    = 2
        self._cells: List[ET.Element] = []
        self._cid:   Dict[str, str]  = {}   # alias → draw.io cell ID

    # ── ID factory ────────────────────────────────────────────────────────────

    def _nid(self) -> str:
        v = self._id
        self._id += 1
        return str(v)

    # ── XML label escaping ────────────────────────────────────────────────────

    @staticmethod
    def _esc(text: str) -> str:
        text = (text.replace("&", "&amp;")
                    .replace("<", "&lt;")
                    .replace(">", "&gt;"))
        return text.replace("\\n", "<br/>").replace("\n", "<br/>")

    # ── Geometry helpers ──────────────────────────────────────────────────────

    def _centre(self, alias: str) -> Tuple[float, float]:
        """Absolute centre of a node."""
        if alias in self.p.states:
            s = self.p.states[alias]
            return s.x + s.w / 2, s.y + s.h / 2
        if alias in self.p.pseudos:
            ps = self.p.pseudos[alias]
            return ps.x + ps.w / 2, ps.y + ps.h / 2
        return 0.0, 0.0

    def _parent_cell_id(self, alias: str) -> str:
        """draw.io parent cell ID for this alias."""
        node = self.p.states.get(alias) or self.p.pseudos.get(alias)
        if node:
            par = getattr(node, "parent", None)
            if par and par in self._cid:
                return self._cid[par]
        return "1"

    def _rel_geo(self, alias: str) -> Tuple[float, float, float, float]:
        """(x, y, w, h) in parent-relative coords for draw.io."""
        if alias in self.p.states:
            s = self.p.states[alias]
            if s.parent and s.parent in self.p.states:
                par = self.p.states[s.parent]
                return s.x - par.x, s.y - par.y, s.w, s.h
            return s.x, s.y, s.w, s.h
        if alias in self.p.pseudos:
            ps = self.p.pseudos[alias]
            if ps.parent and ps.parent in self.p.states:
                par = self.p.states[ps.parent]
                return ps.x - par.x, ps.y - par.y, ps.w, ps.h
            return ps.x, ps.y, ps.w, ps.h
        return 0, 0, STATE_W, STATE_H_MIN

    def _back_x(self) -> float:
        """X coordinate for the backward-edge routing column (left of all states)."""
        xs = ([s.x for s in self.p.states.values() if s.parent is None] +
              [ps.x for ps in self.p.pseudos.values() if ps.parent is None])
        return (min(xs) - BACK_OFFSET) if xs else (MARGIN - BACK_OFFSET)

    # ── Cell factories ────────────────────────────────────────────────────────

    def _vtx(self, cid: str, val: str, style: str,
             x: float, y: float, w: float, h: float,
             parent: str = "1") -> ET.Element:
        c = ET.Element("mxCell", {
            "id": cid, "value": val, "style": style,
            "vertex": "1", "parent": parent
        })
        ET.SubElement(c, "mxGeometry", {
            "x": f"{x:.1f}", "y": f"{y:.1f}",
            "width": f"{w:.1f}", "height": f"{h:.1f}",
            "as": "geometry"
        })
        self._cells.append(c)
        return c

    def _edg(self, val: str, style: str,
             sx: float = 0, sy: float = 0,
             tx: float = 0, ty: float = 0,
             pts: Optional[List[Tuple[float, float]]] = None,
             src_id: str = "", dst_id: str = "",
             label_x: float = 0.0) -> ET.Element:
        """
        label_x: draw.io relative label position along edge.
          0   = midpoint (default)
          0.5 = 75% from source (near target)  — reduces fan-out overlap
         -0.5 = 25% from source (near source)
        """
        attrs: Dict[str, str] = {
            "id": self._nid(), "value": val, "style": style,
            "edge": "1", "parent": "1"
        }
        if src_id: attrs["source"] = src_id
        if dst_id: attrs["target"] = dst_id
        c = ET.Element("mxCell", attrs)
        geo_attrs: Dict[str, str] = {"relative": "1", "as": "geometry"}
        if label_x != 0.0:
            geo_attrs["x"] = f"{label_x:.2f}"
        geo = ET.SubElement(c, "mxGeometry", geo_attrs)
        ET.SubElement(geo, "mxPoint", {"x": f"{sx:.1f}", "y": f"{sy:.1f}", "as": "sourcePoint"})
        ET.SubElement(geo, "mxPoint", {"x": f"{tx:.1f}", "y": f"{ty:.1f}", "as": "targetPoint"})
        if pts:
            arr = ET.SubElement(geo, "Array", {"as": "points"})
            for px, py in pts:
                ET.SubElement(arr, "mxPoint", {"x": f"{px:.1f}", "y": f"{py:.1f}"})
        self._cells.append(c)
        return c

    # ══════════════════════════════════════════════════════════════════════════
    #  DRAW METHODS
    # ══════════════════════════════════════════════════════════════════════════

    # ── Title ─────────────────────────────────────────────────────────────────

    def _draw_title(self):
        tw, _ = self.l.total_bounds()
        self._vtx(self._nid(), self._esc(self.p.title), _S_TITLE,
                  MARGIN, 0, max(tw - 2 * MARGIN, 400), MARGIN + 20)

    # ── States ────────────────────────────────────────────────────────────────

    def _draw_states(self):
        # Composites first (they become draw.io parents)
        for alias in self.p._state_order:
            if self.p.states[alias].sub_states:
                self._draw_one_state(alias)
        for alias in self.p._state_order:
            if not self.p.states[alias].sub_states:
                self._draw_one_state(alias)

    def _draw_one_state(self, alias: str):
        s   = self.p.states[alias]
        cid = self._nid()
        self._cid[alias] = cid
        rx, ry, rw, rh = self._rel_geo(alias)
        style = _S_COMPOSITE if s.sub_states else _S_STATE
        self._vtx(cid, self._esc(s.label), style,
                  rx, ry, rw, rh, parent=self._parent_cell_id(alias))

    # ── Pseudostates (initial black circle + final bullseye) ──────────────────

    def _draw_pseudos(self):
        for alias, ps in self.p.pseudos.items():
            rx, ry, rw, rh = self._rel_geo(alias)
            par_cid = self._parent_cell_id(alias)

            if ps.kind == "initial":
                cid = self._nid()
                self._cid[alias] = cid
                self._vtx(cid, "", _S_INITIAL,
                          rx, ry, INIT_D, INIT_D, parent=par_cid)

            else:
                # ── Bullseye: outer white ring ─────────────────────────────
                ring_cid = self._nid()
                self._cid[alias] = ring_cid   # transitions connect to outer ring
                self._vtx(ring_cid, "", _S_FINAL_RING,
                          rx, ry, FINAL_D, FINAL_D, parent=par_cid)

                # ── Inner filled black dot ─────────────────────────────────
                dot_off = (FINAL_D - FINAL_DOT_D) / 2
                dot_cid = self._nid()
                self._vtx(dot_cid, "", _S_FINAL_DOT,
                          rx + dot_off, ry + dot_off,
                          FINAL_DOT_D, FINAL_DOT_D, parent=par_cid)

    # ── Transitions ───────────────────────────────────────────────────────────

    def _bidir_pairs(self) -> Set[Tuple[str, str]]:
        """All (a,b) pairs where transitions exist in BOTH directions."""
        pairs = {(t.src, t.dst) for t in self.p.transitions}
        return {(a, b) for (a, b) in pairs if (b, a) in pairs}

    def _fan_out_map(self) -> Dict[str, List]:
        """
        For each source, collect forward transitions leaving it.
        Used to distribute exitX so fan-out labels don't overlap.
        """
        fan: Dict[str, List] = defaultdict(list)
        for t in self.p.transitions:
            if t.src == t.dst:
                continue
            src_lv = self.l.node_levels.get(t.src, 0)
            dst_lv = self.l.node_levels.get(t.dst, 0)
            if dst_lv > src_lv:          # forward edge
                fan[t.src].append(t)
        return fan

    def _draw_transitions(self):
        bidir   = self._bidir_pairs()
        back_x  = self._back_x()
        fan_map = self._fan_out_map()

        # Track per-source draw index for fan-out exitX distribution
        fan_idx: Dict[str, int] = defaultdict(int)

        for t in self.p.transitions:
            lbl    = self._esc(t.label)
            src_id = self._cid.get(t.src, "")
            dst_id = self._cid.get(t.dst, "")
            scx, scy = self._centre(t.src)
            dcx, dcy = self._centre(t.dst)
            src_lv   = self.l.node_levels.get(t.src, 0)
            dst_lv   = self.l.node_levels.get(t.dst, 0)

            # ── Self-loop ──────────────────────────────────────────────────
            if t.src == t.dst:
                s = self.p.states.get(t.src)
                if s:
                    sx = s.x + s.w
                    sy1 = s.y + s.h * 0.3
                    sy2 = s.y + s.h * 0.7
                    lx  = sx + SELF_W
                    self._edg(lbl, _S_SELF,
                               sx=sx, sy=sy1, tx=sx, ty=sy2,
                               pts=[(lx, sy1), (lx, sy2)],
                               src_id=src_id, dst_id=src_id)
                continue

            is_bidir = (t.src, t.dst) in bidir

            # ── Forward (going down, dst level > src level) ────────────────
            if dst_lv > src_lv:
                fan_list = fan_map.get(t.src, [])
                n_fan    = len(fan_list)
                idx      = fan_idx[t.src]
                fan_idx[t.src] += 1

                # Distribute exitX across 0.25 … 0.75 when fan-out > 1
                if n_fan > 1:
                    # e.g. n=2 → [0.3, 0.7];  n=3 → [0.2, 0.5, 0.8]
                    span = 0.5
                    lo   = 0.5 - span / 2
                    ex   = lo + span * idx / (n_fan - 1)
                    # entryX mirrors the relative horizontal position
                    ey   = lo + span * idx / (n_fan - 1)
                    extra = (f"exitX={ex:.2f};exitY=1;exitDx=0;exitDy=0;"
                             f"entryX={ey:.2f};entryY=0;entryDx=0;entryDy=0;")
                    style = _BASE_EDGE + "align=center;" + extra
                elif is_bidir:
                    style = _S_FWD_BIDIR
                else:
                    style = _S_FWD

                # Place label near destination (x=0.5 → 75% from source)
                # so fan-out labels cluster near their respective targets
                lx = 0.4 if n_fan > 1 else 0.0
                self._edg(lbl, style,
                           sx=scx, sy=scy, tx=dcx, ty=dcy,
                           src_id=src_id, dst_id=dst_id,
                           label_x=lx)

            # ── Backward (going up, dst level < src level) ─────────────────
            elif dst_lv < src_lv:
                self._edg(lbl, _S_BACK,
                           sx=scx, sy=scy, tx=dcx, ty=dcy,
                           pts=[(back_x, scy), (back_x, dcy)],
                           src_id=src_id, dst_id=dst_id,
                           label_x=-0.2)   # place label near source to avoid mid clutter

            # ── Same level (sideways) ──────────────────────────────────────
            else:
                style = _S_SIDE_R if dcx >= scx else _S_SIDE_L
                self._edg(lbl, style,
                           sx=scx, sy=scy, tx=dcx, ty=dcy,
                           src_id=src_id, dst_id=dst_id)

    # ── Notes ─────────────────────────────────────────────────────────────────

    def _rightmost_x(self) -> float:
        """Absolute right edge of the rightmost top-level state."""
        xs = [s.x + s.w for s in self.p.states.values() if s.parent is None]
        return (max(xs) + NOTE_GAP) if xs else MARGIN

    def _overlaps_state(self, nx: float, ny: float, nw: float, nh: float,
                        margin: float = 10.0) -> bool:
        """True if rectangle (nx,ny,nw,nh) overlaps any top-level state."""
        for s in self.p.states.values():
            if s.parent is not None:
                continue
            if (nx < s.x + s.w + margin and nx + nw > s.x - margin and
                    ny < s.y + s.h + margin and ny + nh > s.y - margin):
                return True
        return False

    def _draw_notes(self):
        placed: List[Tuple[float, float, float, float]] = []
        right_col = self._rightmost_x()   # safe fallback column for notes

        def _no_overlap(nx: float, ny: float, nw: float, nh: float,
                        check_states: bool = True) -> Tuple[float, float]:
            """Shift note downward until clear of placed notes AND (optionally) states."""
            for _ in range(30):   # max iterations guard
                changed = False
                for (px, py, pw, ph) in placed:
                    if nx < px + pw and nx + nw > px and ny < py + ph and ny + nh > py:
                        ny = py + ph + 12
                        changed = True
                if check_states and self._overlaps_state(nx, ny, nw, nh):
                    ny += nh + 12
                    changed = True
                if not changed:
                    break
            return nx, ny

        for note in self.p.notes:
            txt = note.text.strip()
            if not txt:
                continue
            lbl     = self._esc(txt)
            n_lines = max(txt.count("\n") + 1, 1)
            nh      = max(NOTE_MIN_H, n_lines * NOTE_LINE_H + 22)
            target  = note.target.strip()
            pos     = note.position

            # ── note on link ──────────────────────────────────────────────
            if pos == "on link" or not target:
                # Use the transition recorded at parse time (the one just before
                # the note), not blindly the last transition in the list.
                tidx = note.trans_idx
                if 0 <= tidx < len(self.p.transitions):
                    t  = self.p.transitions[tidx]
                    scx2, scy2 = self._centre(t.src)
                    dcx2, dcy2 = self._centre(t.dst)
                    # Place note to the RIGHT of the transition midpoint
                    mx = max(scx2, dcx2) + NOTE_GAP
                    my = (scy2 + dcy2) / 2 - nh / 2
                elif self.p.transitions:
                    t  = self.p.transitions[-1]
                    mx = self._centre(t.dst)[0] + NOTE_GAP
                    my = self._centre(t.dst)[1]
                else:
                    mx, my = right_col, MARGIN
                # Ensure note does not sit on top of any state
                if self._overlaps_state(mx, my, NOTE_W, nh):
                    mx = right_col
                mx, my = _no_overlap(mx, my, NOTE_W, nh, check_states=True)
                cid = self._nid()
                self._vtx(cid, lbl, _S_NOTE, mx, my, NOTE_W, nh)
                placed.append((mx, my, NOTE_W, nh))
                continue

            s = self.p.states.get(target)
            if s is None:
                continue

            # ── Determine preferred position ───────────────────────────────
            if "right" in pos:
                nx, ny  = s.x + s.w + NOTE_GAP, s.y
                note_cx = nx;           note_cy = ny + nh / 2
                s_cx    = s.x + s.w;   s_cy    = s.y + s.h / 2
            elif "left" in pos:
                nx, ny  = s.x - NOTE_W - NOTE_GAP, s.y
                note_cx = nx + NOTE_W;  note_cy = ny + nh / 2
                s_cx    = s.x;          s_cy    = s.y + s.h / 2
            elif "bottom" in pos:
                nx, ny  = s.x, s.y + s.h + NOTE_GAP
                note_cx = nx + NOTE_W / 2; note_cy = ny
                s_cx    = s.x + s.w / 2;   s_cy    = s.y + s.h
            else:   # top
                nx, ny  = s.x, s.y - nh - NOTE_GAP
                note_cx = nx + NOTE_W / 2; note_cy = ny + nh
                s_cx    = s.x + s.w / 2;   s_cy    = s.y

            # ── Fallback: if preferred pos overlaps a state, move to right col ─
            if self._overlaps_state(nx, ny, NOTE_W, nh):
                nx = right_col
                ny = s.y            # align vertically with target state
                note_cx = nx
                note_cy = ny + nh / 2
                s_cx    = s.x + s.w
                s_cy    = s.y + s.h / 2

            nx, ny = _no_overlap(nx, ny, NOTE_W, nh, check_states=True)
            right_col = max(right_col, nx + NOTE_W + NOTE_GAP)  # push rightward

            # Recompute note edge connector anchor y after possible shift
            if "right" in pos or nx >= s.x + s.w:   # right or fallback
                note_cy = ny + nh / 2
                note_cx = nx
                s_cx    = s.x + s.w
                s_cy    = s.y + s.h / 2
            elif "left" in pos:
                note_cy = ny + nh / 2
            elif "bottom" in pos:
                note_cy = ny
            else:
                note_cy = ny + nh

            cid = self._nid()
            self._vtx(cid, lbl, _S_NOTE, nx, ny, NOTE_W, nh)
            placed.append((nx, ny, NOTE_W, nh))

            # Dashed connector (note edge → state edge)
            self._edg("", _S_NOTE_LINK,
                       sx=note_cx, sy=note_cy, tx=s_cx, ty=s_cy)

    # ── Master ────────────────────────────────────────────────────────────────

    def generate_xml(self) -> str:
        self._draw_states()
        self._draw_pseudos()
        self._draw_transitions()
        self._draw_notes()
        self._draw_title()

        pw, ph = self.l.total_bounds()
        pw, ph = int(pw) + 120, int(ph) + 120

        mxfile = ET.Element("mxfile", {
            "host": "app.diagrams.net",
            "version": "21.0", "type": "device"
        })
        diag  = ET.SubElement(mxfile, "diagram", {
            "id": "state-diagram", "name": self.p.title
        })
        model = ET.SubElement(diag, "mxGraphModel", {
            "dx": "1422", "dy": "762",
            "grid": "0", "gridSize": "10",
            "guides": "1", "tooltips": "1",
            "connect": "1", "arrows": "1",
            "fold": "1", "page": "0",
            "pageScale": "1",
            "pageWidth":  str(pw),
            "pageHeight": str(ph),
            "math": "0", "shadow": "0"
        })
        root = ET.SubElement(model, "root")
        ET.SubElement(root, "mxCell", {"id": "0"})
        ET.SubElement(root, "mxCell", {"id": "1", "parent": "0"})
        for cell in self._cells:
            root.append(cell)

        ET.indent(mxfile, space="  ")
        return ET.tostring(mxfile, encoding="unicode", xml_declaration=True)


# ═══════════════════════════════════════════════════════════════════════════════
#  PUBLIC API
# ═══════════════════════════════════════════════════════════════════════════════

def convert(puml_text: str) -> str:
    p = PumlParser()
    p.parse(puml_text)
    l = Layouter(p)
    l.layout()
    g = DrawioGenerator(p, l)
    return g.generate_xml()


def convert_file(src: Path, dst: Path) -> dict:
    text = src.read_text(encoding="utf-8")
    xml  = convert(text)
    dst.write_text(xml, encoding="utf-8")
    p = PumlParser()
    p.parse(text)
    return {
        "input":       src.name,
        "output":      dst.name,
        "title":       p.title,
        "states":      len(p.states),
        "pseudos":     len(p.pseudos),
        "transitions": len(p.transitions),
        "notes":       len(p.notes),
    }


# ═══════════════════════════════════════════════════════════════════════════════
#  CLI
# ═══════════════════════════════════════════════════════════════════════════════

def main():
    argv = sys.argv[1:]
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        sys.exit(0)

    target = Path(argv[0])

    # ── Batch mode ────────────────────────────────────────────────────────────
    if target.is_dir():
        files = sorted(target.glob("05*.puml")) or sorted(target.glob("*.puml"))
        if not files:
            print(f"No .puml files found in: {target}")
            sys.exit(1)
        print(f"\nBatch converting {len(files)} state diagram(s)  [{target}]\n")
        ok = fail = 0
        for f in files:
            out = f.with_suffix(".drawio")
            try:
                info = convert_file(f, out)
                print(f"  OK   {info['input']}")
                print(f"       -> {info['output']}")
                print(f"       Title: {info['title']}")
                print(f"       States:{info['states']}  Pseudos:{info['pseudos']}  "
                      f"Transitions:{info['transitions']}  Notes:{info['notes']}\n")
                ok += 1
            except Exception as e:
                print(f"  FAIL {f.name}: {e}")
                import traceback; traceback.print_exc()
                fail += 1
        print(f"Done: {ok} succeeded, {fail} failed.")
        return

    # ── Single file mode ──────────────────────────────────────────────────────
    if not target.is_file():
        print(f"Error: '{target}' is not a file or directory.")
        sys.exit(1)
    out = Path(argv[1]) if len(argv) >= 2 else target.with_suffix(".drawio")
    try:
        info = convert_file(target, out)
        print(f"\nConverted successfully!")
        print(f"  Input  : {info['input']}")
        print(f"  Output : {info['output']}")
        print(f"  Title  : {info['title']}")
        print(f"  States:{info['states']}  Pseudos:{info['pseudos']}  "
              f"Transitions:{info['transitions']}  Notes:{info['notes']}")
    except Exception as e:
        print(f"Conversion failed: {e}")
        import traceback; traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
