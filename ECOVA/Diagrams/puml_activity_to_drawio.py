#!/usr/bin/env python3
# -*- coding: utf-8 -*-
import sys, io
if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
else:
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
"""
puml_activity_to_drawio.py  (v3 — B&W + Fix double-escape)
===========================================================
Chuyển PlantUML Activity Diagram (swimlane) → Draw.io XML

Fix v3:
  - Xóa double-escaping: &lt; &gt; &amp; không còn xuất hiện as literal text
  - Chuyển <<stereotype>> → «stereotype» (chuẩn UML guillemets)
  - Notes nhúng trong action box bằng HTML — không bao giờ tràn lane
  - Toàn bộ B&W (trắng-đen): fillColor=#FFFFFF, strokeColor=#000000
  - Backward arc (back to): dashed đen, exit/entry bên phải node
  - Decision guard: italic label trên edge
  - Shapes 100% chuẩn UML 2.x Activity Diagram

Cách dùng:
    python puml_activity_to_drawio.py --all
    python puml_activity_to_drawio.py <input.puml> <output.drawio>

Import vào draw.io → Ctrl+Shift+F → "Directed Graphs" layout
"""

import re, os
import xml.etree.ElementTree as ET
from xml.dom import minidom
from dataclasses import dataclass, field
from typing import List, Dict, Optional

# ══════════════════════════════════════════════════════════════════════════════
# LAYOUT CONSTANTS
# ══════════════════════════════════════════════════════════════════════════════
LANE_W     = 360   # swimlane column width (px)
ACT_W      = 300   # action box width
ACT_H_MIN  = 44    # minimum action height
ACT_CPL    = 28    # chars-per-line for label text estimation
NOTE_CPL   = 35    # chars-per-line for note text
NOTE_LH    = 12    # px per note line
DEC_W      = 160   # decision diamond width
DEC_H      = 80    # decision diamond height
BAR_H      = 10    # fork/join bar height
CIRC_D     = 22    # start Initial Node diameter  (solid black)
STOP_OUT   = 30    # stop Final Node — OUTER ring diameter
STOP_IN    = 18    # stop Final Node — INNER dot diameter
VGAP       = 28    # vertical gap between nodes
HDR_H      = 32    # swimlane header strip height
TOP_PAD    = 20    # padding below header to first node
PAGE_W     = 1654
PAGE_H     = 1169

# ══════════════════════════════════════════════════════════════════════════════
# BLACK & WHITE STYLES  (all draw.io mxCell style strings)
# ══════════════════════════════════════════════════════════════════════════════

# Swimlane: white, bold header, black border
# html=1 → supports «stereotype» in lane header via HTML if needed
_S_LANE = (
    "swimlane;startSize={hdr};"
    "fillColor=#FFFFFF;strokeColor=#000000;"
    "fontStyle=1;fontSize=11;fontColor=#000000;"
    "horizontal=1;swimlaneHead=0;align=center;html=1;"
)
# Start node: solid black circle
_S_START = (
    "ellipse;fillColor=#000000;strokeColor=#000000;"
    "fontColor=#FFFFFF;aspect=fixed;"
)
# Stop node — UML Activity Final Node = outer hollow ring + inner solid dot
# Rendered as TWO nested cells (stop_ring + stop_dot) so the white gap is visible.
_S_STOP_RING = (
    "ellipse;fillColor=#FFFFFF;strokeColor=#000000;strokeWidth=2;aspect=fixed;"
)
_S_STOP_DOT = (
    "ellipse;fillColor=#000000;strokeColor=#000000;aspect=fixed;"
)
# Action: rounded rectangle, white, thin black border, text wraps, HTML label
_S_ACTION = (
    "rounded=1;whiteSpace=wrap;html=1;arcSize=8;"
    "fillColor=#FFFFFF;strokeColor=#000000;strokeWidth=1;"
    "fontSize=10;fontColor=#000000;"
    "align=left;verticalAlign=top;"
    "spacingLeft=6;spacingRight=4;spacingTop=4;spacingBottom=4;"
)
# Decision: diamond (rhombus), white
_S_DECISION = (
    "rhombus;whiteSpace=wrap;html=1;"
    "fillColor=#FFFFFF;strokeColor=#000000;strokeWidth=1;"
    "fontSize=10;fontColor=#000000;align=center;verticalAlign=middle;"
)
# Fork / Join bar: solid black rectangle
_S_BAR = "fillColor=#000000;strokeColor=#000000;rounded=0;"

# Merge point: invisible (just a connector anchor)
_S_MERGE = "ellipse;fillColor=none;strokeColor=none;opacity=0;"

# ── Edge styles ───────────────────────────────────────────────────────────────
# Normal control flow: solid black, open arrowhead
_S_EDGE_FLOW = (
    "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;"
    "jettySize=auto;strokeColor=#000000;strokeWidth=1;"
    "startArrow=none;endArrow=open;endFill=1;"
    "fontSize=9;fontColor=#000000;"
)
# Guard-labeled edge (decision branch): same but italic label
_S_EDGE_GUARD = (
    "edgeStyle=orthogonalEdgeStyle;rounded=0;orthogonalLoop=1;"
    "jettySize=auto;strokeColor=#000000;strokeWidth=1;"
    "startArrow=none;endArrow=open;endFill=1;"
    "fontSize=9;fontColor=#000000;fontStyle=2;"  # italic
)
# Backward arc (back to / repeat loop): dashed black, exits right side
_S_EDGE_BACK = (
    "edgeStyle=orthogonalEdgeStyle;rounded=1;orthogonalLoop=1;"
    "jettySize=auto;strokeColor=#000000;strokeWidth=1;"
    "dashed=1;dashPattern=6 3;"
    "startArrow=none;endArrow=open;endFill=1;"
    "exitX=1;exitY=0.5;exitDx=0;exitDy=0;"
    "entryX=1;entryY=0.5;entryDx=0;entryDy=0;"
    "fontSize=9;fontColor=#444444;fontStyle=2;"
)


# ══════════════════════════════════════════════════════════════════════════════
# ENCODING HELPERS
# CRITICAL: values passed to ET.SubElement(value=...) must NOT be pre-XML-
# escaped, because ET will XML-escape them itself.  Only HTML-encode content
# that will be rendered with html=1 (so that < > show as literal text).
# ══════════════════════════════════════════════════════════════════════════════

def _h(s: str) -> str:
    """HTML-encode content characters for use inside an html=1 label.
    Result is a Python string that, after ET's XML-attribute escaping, will
    decode to proper HTML entities that a browser / draw.io renders as the
    original characters.

    Chain for e.g. '<':
      Python: '&lt;'              ← this function converts < → &lt;
      ET XML-attr: '&amp;lt;'    ← ET escapes & → &amp;
      draw.io XML-parse: '&lt;'  ← un-escapes &amp; → &
      draw.io HTML-render: '<'   ← HTML entity &lt; → < ✓
    """
    return (s.replace("&", "&amp;")
             .replace("<", "&lt;")
             .replace(">", "&gt;")
             .replace('"', "&quot;"))


def _stereo(s: str) -> str:
    """Convert PlantUML <<stereotype>> notation to UML guillemets «stereotype»."""
    return s.replace("<<", "\u00AB").replace(">>", "\u00BB")


def _estimate_h(label: str, note: str = "") -> float:
    """Estimate action box height from text lengths."""
    def lines(text, cpl):
        return sum(max(1, (len(ln) + cpl - 1) // cpl)
                   for ln in text.split("\n")) if text else 0

    lbl_h  = lines(label, ACT_CPL) * 16 + 14
    note_h = (lines(note, NOTE_CPL) * NOTE_LH + 16) if note else 0
    return max(ACT_H_MIN, lbl_h + note_h)


def _action_html(label: str, note: str = "") -> str:
    """Build HTML label string for an action box.
    - label + note are HTML-encoded so special chars render correctly.
    - Returns a raw Python string; ET will XML-escape it (one level).
    """
    body = _h(label).replace("\n", "<br/>")

    if not note:
        return body

    note_body = _h(note).replace("\n", "<br/>")
    # Separator using box-drawing char (safe Unicode)
    sep = "&#x2500;" * 10   # ──────────── (HTML entity, safe after ET escaping)
    return (
        f"{body}"
        f"<br/>"
        f"<font color=\"#888888\" size=\"1\">"
        f"{sep}<br/>"
        f"{note_body}"
        f"</font>"
    )


# ══════════════════════════════════════════════════════════════════════════════
# AST
# ══════════════════════════════════════════════════════════════════════════════

class _N: pass   # base

@dataclass
class ALane(_N):   name: str; color: str = ""
@dataclass
class AStart(_N):  pass
@dataclass
class AStop(_N):   pass
@dataclass
class AAction(_N): label: str; note: str = ""
@dataclass
class ABackTo(_N): target: str
@dataclass
class ANote(_N):   text: str

@dataclass
class ADecision(_N):
    cond: str; then_lbl: str; else_lbl: str
    then_body: List; else_body: List

@dataclass
class AFork(_N):
    branches: List

@dataclass
class ARepeat(_N):
    body: List; cond: str; loop_lbl: str; exit_lbl: str


# ══════════════════════════════════════════════════════════════════════════════
# PUML PARSER
# ══════════════════════════════════════════════════════════════════════════════

_RE_LANE = re.compile(r'^\|(#[0-9A-Fa-f]{6})?\s*(.+?)\s*\|$')
_RE_ACT  = re.compile(r'^:(.+?);$')
_RE_IF   = re.compile(r'^if\s*\((.+?)\)\s*then\s*\((\[?.+?\]?)\)\s*$', re.I)
_RE_ELSE = re.compile(r'^else\s*(?:\((\[?.+?\]?)\))?\s*$', re.I)
_RE_RW   = re.compile(r'^repeat\s+while\s*\((.+?)\)\s*is\s*\((\[?.+?\]?)\)\s*$', re.I)
_RE_BT   = re.compile(r'^back\s+to\s+:(.+?);\s*$', re.I)
_RE_ARR  = re.compile(r'^->\s*\[(.+?)\];\s*$', re.I)

def _skip(l: str) -> bool:
    return (not l or l.startswith("@") or l.startswith("skinparam")
            or l.startswith("title") or l.startswith("'"))

def _clean(s: str) -> str:
    return ' '.join(s.strip().split())


class Parser:
    def __init__(self, lines):
        self.lines = [l.rstrip() for l in lines]
        self.i = 0

    def _peek(self):
        while self.i < len(self.lines):
            l = self.lines[self.i].strip()
            if not _skip(l): return l
            self.i += 1
        return None

    def _adv(self): self.i += 1

    def _note(self):
        """Consume note block, return text."""
        self._adv()  # skip 'note ...' line
        parts = []
        while self.i < len(self.lines):
            r = self.lines[self.i].strip(); self.i += 1
            if r.lower() == "end note": break
            if not r.startswith("'"): parts.append(r)
        return "\n".join(parts).strip()

    def parse(self): return self._seq()

    def _sub(self, lines): return Parser(lines).parse()

    def _seq(self):
        res = []
        while True:
            l = self._peek()
            if l is None: break
            n = self._one(l)
            if n is not None: res.append(n)
        return res

    def _one(self, l):
        # Lane
        m = _RE_LANE.match(l)
        if m:
            color = (m.group(1) or "").strip()
            name  = _stereo(_clean(m.group(2)))
            self._adv(); return ALane(name, color)

        if l == "start":    self._adv(); return AStart()
        if l == "stop":     self._adv(); return AStop()

        # Note block
        if l.lower().startswith("note"):
            text = self._note(); return ANote(text)

        # Action (absorb following note)
        m = _RE_ACT.match(l)
        if m:
            lbl = _clean(m.group(1)); self._adv()
            note = ""
            nxt = self._peek()
            if nxt and nxt.lower().startswith("note"):
                note = self._note()
            return AAction(lbl, note)

        # back to
        m = _RE_BT.match(l)
        if m: self._adv(); return ABackTo(_clean(m.group(1)))

        # -> [label] — skip (exit arc of repeat)
        if _RE_ARR.match(l): self._adv(); return None

        # if / then / else / endif
        m = _RE_IF.match(l)
        if m:
            cond = _clean(m.group(1)); tl = m.group(2).strip()
            self._adv(); return self._if(cond, tl)

        if re.match(r'^(else|endif)\b', l, re.I): self._adv(); return None

        # fork
        if l.lower() == "fork": self._adv(); return self._fork()
        if re.match(r'^(fork again|end fork)$', l, re.I): self._adv(); return None

        # repeat
        if l.lower() == "repeat": self._adv(); return self._repeat()
        if _RE_RW.match(l): self._adv(); return None
        if l.lower() == "end note": self._adv(); return None

        self._adv(); return None  # unknown – skip

    def _if(self, cond, tl):
        then_ls, else_ls = [], []
        el = "[No]"; in_else = False; depth = 1
        while self.i < len(self.lines):
            r = self.lines[self.i].strip(); self.i += 1
            if _skip(r): continue
            if _RE_IF.match(r):
                depth += 1
                (else_ls if in_else else then_ls).append(r); continue
            if re.match(r'^endif$', r, re.I):
                if depth > 1:
                    depth -= 1
                    (else_ls if in_else else then_ls).append(r)
                else: break
                continue
            if re.match(r'^else\b', r, re.I) and depth == 1:
                em = _RE_ELSE.match(r)
                if em and em.group(1): el = em.group(1).strip()
                in_else = True; continue
            (else_ls if in_else else then_ls).append(r)
        return ADecision(cond, tl, el, self._sub(then_ls), self._sub(else_ls))

    def _fork(self):
        brs = [[]]
        depth = 1
        while self.i < len(self.lines):
            r = self.lines[self.i].strip(); self.i += 1
            if _skip(r): continue
            if r.lower() == "fork":      depth += 1; brs[-1].append(r); continue
            if r.lower() == "end fork" and depth > 1: depth -= 1; brs[-1].append(r); continue
            if r.lower() == "end fork":  break
            if r.lower() == "fork again" and depth == 1: brs.append([]); continue
            brs[-1].append(r)
        return AFork([self._sub(b) for b in brs])

    def _repeat(self):
        body = []; cond = ""; ll = "[retry]"; xl = "[continue]"
        while self.i < len(self.lines):
            r = self.lines[self.i].strip()
            m = _RE_RW.match(r)
            if m:
                cond = _clean(m.group(1)); ll = m.group(2).strip()
                self.i += 1
                nxt = self.lines[self.i].strip() if self.i < len(self.lines) else ""
                am = _RE_ARR.match(nxt)
                if am: xl = f"[{_clean(am.group(1))}]"; self.i += 1
                break
            body.append(r); self.i += 1
        return ARepeat(self._sub(body), cond, ll, xl)


# ══════════════════════════════════════════════════════════════════════════════
# GRAPH  (nodes + edges with absolute pixel positions)
# ══════════════════════════════════════════════════════════════════════════════

@dataclass
class GNode:
    nid: str
    shape: str       # start|stop_ring|stop_dot|action|decision|bar|merge
    label: str       # HTML string for action/decision; plain text for others
    lane: str
    x: float = 0.0   # absolute x (or relative to parent_id if set)
    y: float = 0.0   # absolute y (or relative to parent_id if set)
    w: float = ACT_W
    h: float = ACT_H_MIN
    parent_id: Optional[str] = None   # if set → child of this cell in draw.io

@dataclass
class GEdge:
    eid: str
    src: str; tgt: str
    label: str = ""
    style: str = "flow"   # flow | guard | back

_ctr = [4000]
def _id(p="n"): _ctr[0] += 1; return f"{p}{_ctr[0]}"


# ══════════════════════════════════════════════════════════════════════════════
# BUILD CONTEXT
# ══════════════════════════════════════════════════════════════════════════════

@dataclass
class Ctx:
    y: float
    lane: str
    lanes: List[str]
    nodes: List[GNode]
    edges: List[GEdge]
    lmap: Dict[str, str]   # action label → nid (for back-to targeting)
    prev: Optional[str] = None
    repeat_xl: str = ""

    def ensure(self, name):
        if name not in self.lanes: self.lanes.append(name)

    def idx(self, lane=None):
        l = lane or self.lane
        try: return self.lanes.index(l)
        except ValueError: return 0

    def cx(self, lane=None): return self.idx(lane) * LANE_W + LANE_W / 2
    def nx(self, w, lane=None): return self.cx(lane) - w / 2

    def place(self, shape, label, w, h, lane=None) -> GNode:
        l = lane or self.lane
        gn = GNode(nid=_id(), shape=shape, label=label,
                   lane=l, x=self.nx(w, l), y=self.y, w=w, h=h)
        self.nodes.append(gn)
        return gn

    def edge(self, src, tgt, label="", style="flow") -> GEdge:
        e = GEdge(eid=_id("e"), src=src, tgt=tgt, label=label, style=style)
        self.edges.append(e); return e


# ══════════════════════════════════════════════════════════════════════════════
# AST → GRAPH BUILDER
# ══════════════════════════════════════════════════════════════════════════════

def _has_bt(body):    return any(isinstance(n, ABackTo) for n in body)
def _has_stop(body):  return any(isinstance(n, AStop)   for n in body)
def _is_empty(body):  return len(body) == 0

def _terminal(body):  return _has_bt(body) or _has_stop(body) or _is_empty(body)


def build(ast: List, ctx: Ctx) -> Optional[str]:
    """Traverse AST, place nodes+edges. Returns last node nid (None if terminal)."""
    for node in ast:

        if isinstance(node, ANote):
            # Attach to last action node by re-labeling it
            for gn in reversed(ctx.nodes):
                if gn.shape == "action":
                    # Re-estimate height with note
                    raw_lbl = gn.label  # already HTML; extract plain back is complex
                    # Simpler: store note separately and rebuild
                    gn.label = _action_html(gn.label, node.text)  # embed
                    new_h = max(gn.h, _estimate_h("", node.text) + gn.h)
                    gn.h = new_h
                    break
            continue

        if isinstance(node, ALane):
            ctx.ensure(node.name); ctx.lane = node.name; continue

        if isinstance(node, AStart):
            gn = ctx.place("start", "", CIRC_D, CIRC_D)
            if ctx.prev: ctx.edge(ctx.prev, gn.nid)
            ctx.prev = gn.nid; ctx.y += CIRC_D + VGAP; continue

        if isinstance(node, AStop):
            # UML Activity Final Node ◉: outer hollow ring + inner solid dot
            ring_nid = _id()
            dot_off  = (STOP_OUT - STOP_IN) / 2   # offset of inner dot within ring
            ring = GNode(nid=ring_nid, shape="stop_ring", label="",
                         lane=ctx.lane,
                         x=ctx.nx(STOP_OUT), y=ctx.y,
                         w=STOP_OUT, h=STOP_OUT)
            dot  = GNode(nid=_id(), shape="stop_dot", label="",
                         lane=ctx.lane,
                         x=dot_off, y=dot_off,   # relative to ring cell
                         w=STOP_IN, h=STOP_IN,
                         parent_id=ring_nid)
            ctx.nodes.append(ring)
            ctx.nodes.append(dot)
            if ctx.prev: ctx.edge(ctx.prev, ring_nid)
            ctx.prev = None               # terminal — no outgoing edges
            ctx.y += STOP_OUT + VGAP
            continue

        if isinstance(node, AAction):
            html  = _action_html(node.label, node.note)
            h     = _estimate_h(node.label, node.note)
            gn    = ctx.place("action", html, ACT_W, h)
            ctx.lmap[node.label.strip()] = gn.nid
            if ctx.prev: ctx.edge(ctx.prev, gn.nid)
            ctx.prev = gn.nid; ctx.y += h + VGAP; continue

        if isinstance(node, ABackTo):
            tgt = ctx.lmap.get(node.target, "")
            if tgt and ctx.prev:
                ctx.edge(ctx.prev, tgt, label=node.target, style="back")
            ctx.prev = None; continue

        if isinstance(node, ADecision): _dec(node, ctx); continue
        if isinstance(node, AFork):     _fork(node, ctx); continue
        if isinstance(node, ARepeat):   _rpt(node, ctx); continue

    return ctx.prev


def _patch_first_edge(ctx: Ctx, src: str, label: str, style: str = "guard"):
    """Label the first edge created FROM src that has no label yet."""
    for e in reversed(ctx.edges):
        if e.src == src and not e.label:
            e.label = label; e.style = style; return


def _dec(node: ADecision, ctx: Ctx):
    dec = ctx.place("decision", _h(node.cond), DEC_W, DEC_H)
    if ctx.prev: ctx.edge(ctx.prev, dec.nid)
    ctx.y += DEC_H + VGAP
    sy = ctx.y; sl = ctx.lane

    t_term = _terminal(node.then_body)
    e_term = _terminal(node.else_body)

    # Case A: THEN is terminal (error/back-to), ELSE is happy path
    if t_term and not e_term:
        ctx.prev = dec.nid
        build(node.then_body, ctx)
        _patch_first_edge(ctx, dec.nid, node.then_lbl, "back")
        ctx.y = sy; ctx.lane = sl; ctx.prev = dec.nid
        build(node.else_body, ctx)
        _patch_first_edge(ctx, dec.nid, node.else_lbl, "guard")

    # Case B: ELSE is terminal, THEN is happy path
    elif e_term and not t_term:
        ctx.prev = dec.nid
        build(node.else_body, ctx)
        _patch_first_edge(ctx, dec.nid, node.else_lbl, "back")
        ctx.y = sy; ctx.lane = sl; ctx.prev = dec.nid
        build(node.then_body, ctx)
        _patch_first_edge(ctx, dec.nid, node.then_lbl, "guard")

    # Case C: Both branches have content — lay then first, else after
    else:
        ctx.prev = dec.nid
        build(node.then_body, ctx)
        _patch_first_edge(ctx, dec.nid, node.then_lbl, "guard")
        t_end = ctx.prev; ty = ctx.y

        ctx.y = sy; ctx.lane = sl; ctx.prev = dec.nid
        build(node.else_body, ctx)
        _patch_first_edge(ctx, dec.nid, node.else_lbl, "guard")
        e_end = ctx.prev; ey = ctx.y

        ctx.y = max(ty, ey)
        mg = ctx.place("merge", "", 10, 10)
        if t_end: ctx.edge(t_end, mg.nid)
        if e_end: ctx.edge(e_end, mg.nid)
        ctx.prev = mg.nid; ctx.y += VGAP

    ctx.lane = sl


def _fork(node: AFork, ctx: Ctx):
    # Collect branch lanes
    b_lanes = []
    for br in node.branches:
        found = next((n.name for n in br if isinstance(n, ALane)),
                     ctx.lane)
        b_lanes.append(found); ctx.ensure(found)

    idxs  = [ctx.idx(l) for l in b_lanes]
    mi, ma = min(idxs), max(idxs)
    bx = mi * LANE_W + 20
    bw = (ma - mi + 1) * LANE_W - 40

    # Fork bar (absolute coords, parent=1)
    fb = GNode(nid=_id(), shape="bar", label="",
               lane=ctx.lane, x=bx, y=ctx.y, w=bw, h=BAR_H)
    ctx.nodes.append(fb)
    if ctx.prev: ctx.edge(ctx.prev, fb.nid)
    ctx.y += BAR_H + VGAP; fy = ctx.y

    ends = []; eys = []
    for br_ast, bl in zip(node.branches, b_lanes):
        ctx.y = fy; ctx.lane = bl; ctx.prev = fb.nid
        build(br_ast, ctx)
        ends.append(ctx.prev); eys.append(ctx.y)

    ctx.y = max(eys) if eys else fy
    jb = GNode(nid=_id(), shape="bar", label="",
               lane=ctx.lane, x=bx, y=ctx.y, w=bw, h=BAR_H)
    ctx.nodes.append(jb)
    for ep in ends:
        if ep: ctx.edge(ep, jb.nid)
    ctx.y += BAR_H + VGAP; ctx.prev = jb.nid


def _rpt(node: ARepeat, ctx: Ctx):
    nc = len(ctx.nodes)
    build(node.body, ctx)
    bf = ctx.nodes[nc].nid if len(ctx.nodes) > nc else None

    rw = ctx.place("decision", _h(node.cond), DEC_W, DEC_H)
    if ctx.prev: ctx.edge(ctx.prev, rw.nid)
    ctx.y += DEC_H + VGAP
    if bf: ctx.edge(rw.nid, bf, label=node.loop_lbl, style="back")
    ctx.prev = rw.nid; ctx.repeat_xl = node.exit_lbl


# ══════════════════════════════════════════════════════════════════════════════
# DRAW.IO XML GENERATOR
# ══════════════════════════════════════════════════════════════════════════════

def _xml(nodes: List[GNode], edges: List[GEdge], lanes: List[str]) -> str:

    total_h = max((n.y + n.h for n in nodes), default=400) + 80
    total_h = max(total_h, PAGE_H)

    root_el   = ET.Element("mxGraphModel",
        dx="1422", dy="762", grid="1", gridSize="10",
        guides="1", tooltips="1", connect="1", arrows="1",
        fold="1", page="1", pageScale="1",
        pageWidth=str(PAGE_W), pageHeight=str(PAGE_H),
        math="0", shadow="0")
    root_e = ET.SubElement(root_el, "root")
    ET.SubElement(root_e, "mxCell", id="0")
    ET.SubElement(root_e, "mxCell", id="1", parent="0")

    # ── Swimlane containers ───────────────────────────────────────────────────
    lane_ids: Dict[str, str] = {}
    for idx, lane in enumerate(lanes):
        lid = f"lane_{idx}"; lane_ids[lane] = lid
        style = _S_LANE.format(hdr=HDR_H)
        # value passed raw — ET will XML-escape any special chars
        c = ET.SubElement(root_e, "mxCell",
            id=lid, value=lane,          # <── raw, no pre-escape
            style=style, vertex="1", parent="1")
        ET.SubElement(c, "mxGeometry",
            x=str(idx * LANE_W), y="0",
            width=str(LANE_W), height=str(int(total_h)),
            **{"as": "geometry"})

    # ── Nodes ─────────────────────────────────────────────────────────────────
    for gn in nodes:
        parent = lane_ids.get(gn.lane, "1")
        lx = lanes.index(gn.lane) * LANE_W if gn.lane in lanes else 0
        rx = gn.x - lx   # relative x inside lane

        if gn.shape == "start":
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value="", style=_S_START,
                vertex="1", parent=parent)
            ET.SubElement(c, "mxGeometry",
                x=str(rx), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

        elif gn.shape == "stop_ring":
            # Outer hollow ring of the UML Final Node
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value="", style=_S_STOP_RING,
                vertex="1", parent=parent)
            ET.SubElement(c, "mxGeometry",
                x=str(rx), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

        elif gn.shape == "stop_dot":
            # Inner solid black dot — child of the ring cell so they move together
            # gn.x and gn.y are relative offsets inside the ring's bounding box
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value="", style=_S_STOP_DOT,
                vertex="1", parent=gn.parent_id)   # parent = ring nid
            ET.SubElement(c, "mxGeometry",
                x=str(gn.x), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

        elif gn.shape == "decision":
            # label is HTML-encoded (single level via _h()); ET will xml-escape
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value=gn.label,   # <── already _h() encoded; ET will xml-escape
                style=_S_DECISION, vertex="1", parent=parent)
            ET.SubElement(c, "mxGeometry",
                x=str(rx), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

        elif gn.shape == "bar":
            # Fork/join bars: absolute coords, placed at root level
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value="", style=_S_BAR,
                vertex="1", parent="1")
            ET.SubElement(c, "mxGeometry",
                x=str(gn.x), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

        elif gn.shape == "merge":
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value="", style=_S_MERGE,
                vertex="1", parent=parent)
            ET.SubElement(c, "mxGeometry",
                x=str(rx), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

        else:   # action
            # label is full HTML from _action_html(); ET will xml-escape
            c = ET.SubElement(root_e, "mxCell",
                id=gn.nid, value=gn.label,   # <── HTML string; ET will xml-escape
                style=_S_ACTION, vertex="1", parent=parent)
            ET.SubElement(c, "mxGeometry",
                x=str(rx), y=str(gn.y),
                width=str(gn.w), height=str(gn.h),
                **{"as": "geometry"})

    # ── Edges ─────────────────────────────────────────────────────────────────
    for ge in edges:
        if ge.style == "back":
            st = _S_EDGE_BACK
        elif ge.style == "guard":
            st = _S_EDGE_GUARD
        else:
            st = _S_EDGE_FLOW

        # Edge label: pass raw string — ET will xml-escape
        c = ET.SubElement(root_e, "mxCell",
            id=ge.eid, value=ge.label,   # <── raw, no pre-escape
            style=st, edge="1",
            source=ge.src, target=ge.tgt,
            parent="1")
        ET.SubElement(c, "mxGeometry", relative="1",
                      **{"as": "geometry"})

    raw    = ET.tostring(root_el, encoding="unicode")
    pretty = minidom.parseString(raw).toprettyxml(indent="  ", encoding="UTF-8")
    return pretty.decode("utf-8")


# ══════════════════════════════════════════════════════════════════════════════
# MAIN CONVERTER
# ══════════════════════════════════════════════════════════════════════════════

def convert(inp: str, out: str) -> None:
    print(f"[1/4] Reading   : {inp}")
    with open(inp, encoding="utf-8") as f:
        lines = f.readlines()

    title = os.path.splitext(os.path.basename(inp))[0]
    for ln in lines:
        m = re.match(r'^title\s+(.+)', ln.strip(), re.I)
        if m: title = m.group(1).strip(); break

    print(f"[2/4] Parsing   : {title}")
    ast = Parser(lines).parse()

    print(f"[3/4] Building  ...")
    ctx = Ctx(y=HDR_H + TOP_PAD, lane="",
              lanes=[], nodes=[], edges=[],
              lmap={}, prev=None)
    build(ast, ctx)

    if not ctx.lanes:
        ctx.lanes = ["(default)"]
        for gn in ctx.nodes:
            if not gn.lane: gn.lane = "(default)"

    print(f"         → {len(ctx.lanes)} lanes | "
          f"{len(ctx.nodes)} nodes | {len(ctx.edges)} edges")

    print(f"[4/4] Writing   : {out}")
    xml_str = _xml(ctx.nodes, ctx.edges, ctx.lanes)
    with open(out, "w", encoding="utf-8") as f:
        f.write(xml_str)
    print("      [OK]\n")


def main():
    this = os.path.dirname(os.path.abspath(__file__))

    if "--all" in sys.argv or len(sys.argv) == 1:
        for pf, df in [
            ("03a_ActivityDiagram_MainFlow.puml",
             "03a_ActivityDiagram_MainFlow.drawio"),
            ("03b_ActivityDiagram_Login.puml",
             "03b_ActivityDiagram_Login.drawio"),
            ("03c_ActivityDiagram_ForgotPassword.puml",
             "03c_ActivityDiagram_ForgotPassword.drawio"),
        ]:
            inp = os.path.join(this, pf)
            out = os.path.join(this, df)
            if not os.path.exists(inp):
                print(f"[SKIP] {pf}")
                continue
            convert(inp, out)

        print("=" * 58)
        print("  DONE!  Sau khi import vao draw.io:")
        print("    Ctrl+A  →  Arrange  →  'Directed Graphs'")
        print("=" * 58)

    elif len(sys.argv) >= 3:
        convert(sys.argv[1], sys.argv[2])
    else:
        print("Usage:")
        print("  python puml_activity_to_drawio.py --all")
        print("  python puml_activity_to_drawio.py <in.puml> <out.drawio>")
        sys.exit(1)


if __name__ == "__main__":
    main()
