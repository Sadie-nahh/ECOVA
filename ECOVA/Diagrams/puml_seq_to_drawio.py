#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
puml_seq_to_drawio.py  ·  v2.0  (Traditional B&W UML Sequence Diagram)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Converts PlantUML Sequence Diagrams (.puml) → draw.io XML (.drawio)
following the classic, black-and-white UML Sequence Diagram convention:

  • All fills: white (#ffffff)
  • All strokes: black (#000000)
  • Participant boxes: white rectangle, black border, stereotype on top
  • Actors: UML stick-figure, name below
  • Lifelines: vertical dashed black lines
  • Activation bars: white narrow rectangle, black border ON the lifeline
  • Messages (A→B):      solid line + filled arrowhead  (▶)
  • Return  (A-->B):     dashed line + open arrowhead   (>)
  • Self-message (A→A):  orthogonal loop-right
  • Combined fragments:  rectangle, black border, fragment-type pentagon
  • Else separators:     dashed horizontal line inside fragment
  • Notes:  dog-ear rectangle, white fill, black border

Supported PUML syntax:
  actor / participant <<stereotype>>
  A -> B : label  |  A --> B : label  |  A -> A : self
  activate / deactivate
  alt / else / opt / loop / break
  note right of / left of / over / top … end note
  title / skinparam (title kept, skinparam ignored)
  Multi-line labels: \\n → <br/>

Usage:
  python puml_seq_to_drawio.py input.puml
  python puml_seq_to_drawio.py input.puml output.drawio
  python puml_seq_to_drawio.py "E:\\...\\Diagrams"   # batch all *.puml

Requirements: Python ≥ 3.9 (uses xml.etree.ElementTree.indent)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
"""

from __future__ import annotations

import re, sys, xml.etree.ElementTree as ET
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple
from pathlib import Path

# ── Windows cp1252 console fix ───────────────────────────────────────────
if sys.stdout.encoding and sys.stdout.encoding.lower() not in ("utf-8", "utf_8"):
    import io
    sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")
    sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding="utf-8", errors="replace")


# ═════════════════════════════════════════════════════════════════════════
#  LAYOUT CONSTANTS  (all sizes in pixels)
# ═════════════════════════════════════════════════════════════════════════

# ── Canvas margins ────────────────────────────────────────────────────────
MARGIN_LEFT   = 60       # X of first participant's left edge
MARGIN_TOP    = 20       # Y of participant headers

# ── Participant header boxes ──────────────────────────────────────────────
P_W           = 160      # participant box width
P_H           = 60       # participant box height  (2 text rows: stereotype + name)
P_COL         = 220      # center-to-center column spacing (must be ≥ P_W + some gap)
A_W           = 40       # actor figure width
A_H           = 70       # actor figure height

# ── Lifeline ──────────────────────────────────────────────────────────────
LL_GAP        = 0        # extra gap between participant bottom and lifeline start

# ── Vertical step height between consecutive logical events ───────────────
STEP_H        = 52       # pixels per step (message, activate, note, fragment boundary)

# ── Activation bars ───────────────────────────────────────────────────────
ACT_W         = 12       # bar width
ACT_NEST_OFF  = 6        # extra X offset per nesting level (keeps bars visible)

# ── Self-messages ─────────────────────────────────────────────────────────
SELF_LOOP_W   = 55       # how far right the loop extends from lifeline center
SELF_LOOP_H   = 30       # vertical drop of the self-loop (fraction of STEP_H)

# ── Combined fragments ────────────────────────────────────────────────────
FRAG_PAD_X    = 12       # horizontal padding outside outermost lifeline centre
FRAG_PAD_TOP  = 14       # space above first event inside fragment
FRAG_PAD_BOT  = 10       # space below last event inside fragment
FRAG_HDR_H    = 20       # height of the pentagon guard label
FRAG_HDR_W    = 70       # width of the pentagon guard label
FRAG_TYPE_FONT = 8       # font size for fragment-type keyword (ALT / OPT / LOOP)

# ── Notes ─────────────────────────────────────────────────────────────────
NOTE_W        = 190      # default note width
NOTE_MIN_H    = 42       # minimum note height
NOTE_LINE_H   = 14       # estimated px per text line
NOTE_GAP      = 8        # gap between note and lifeline

# ── First message Y (below participant header bottom) ─────────────────────
FIRST_STEP_Y_OFFSET = 30   # extra vertical space before step 1


# ═════════════════════════════════════════════════════════════════════════
#  DATA CLASSES  (parser output)
# ═════════════════════════════════════════════════════════════════════════

@dataclass
class Participant:
    alias:        str
    display_name: str   # cleaned (leading ':' removed)
    is_actor:     bool
    stereotype:   str   # lowercase, e.g. "boundary"
    col_index:    int = 0

@dataclass
class MsgEvent:
    src:       str
    dst:       str
    label:     str      # may contain '\n'
    is_return: bool
    step:      int

@dataclass
class ActivationEvent:
    alias:    str
    is_start: bool
    step:     int

@dataclass
class NoteEvent:
    text:     str       # may contain '\n'
    position: str       # "right of" | "left of" | "over" | "top"
    targets:  List[str] # aliases
    step:     int

@dataclass
class FragmentEvent:
    sub_kind: str       # "start" | "else" | "end"
    frag_kind: str      # "alt" | "opt" | "loop" | "break" | "group"
    guard:    str
    frag_id:  int
    step:     int

@dataclass
class RawElem:
    kind: str           # "msg" | "act" | "note" | "frag"
    data: object
    step: int


# ═════════════════════════════════════════════════════════════════════════
#  PUML PARSER
# ═════════════════════════════════════════════════════════════════════════

_R_ACTOR    = re.compile(r'^actor\s+"?([^"]+?)"?\s+as\s+(\w+)(?:\s+<<([^>]+)>>)?', re.I)
_R_PART     = re.compile(r'^participant\s+"?([^"]+?)"?\s+as\s+(\w+)(?:\s+<<([^>]+)>>)?', re.I)
_R_TITLE    = re.compile(r'^title\s+(.+)$', re.I)
_R_MSG      = re.compile(r'^(\w+)\s*(--?>|-->|->>|->)\s*(\w+)\s*:\s*(.*)')
_R_ACT      = re.compile(r'^activate\s+(\w+)',   re.I)
_R_DEACT    = re.compile(r'^deactivate\s+(\w+)', re.I)
_R_FSTART   = re.compile(r'^(alt|opt|loop|break|group|critical)\s*(.*)?$', re.I)
_R_FELSE    = re.compile(r'^else\s*(.*)?$',  re.I)
_R_FEND     = re.compile(r'^end\s*$',        re.I)
_R_NSTART   = re.compile(r'^note\s+(right of|left of|over|top|right|left)\s*(.*)?$', re.I)
_R_NINLINE  = re.compile(r'^note\s+(right of|left of|over|top|right|left)\s*(.*?)\s*:\s*(.+)$', re.I)
_R_NEND     = re.compile(r'^end\s+note$', re.I)
_R_SKIP     = re.compile(
    r'^(@startuml|@enduml|skinparam|hide|show|autonumber|!|\''
    r'|ref\s|box\s|end\s+box|boundary |control |entity |database '
    r'|collections |queue |create |destroy |footbox )', re.I
)


class PumlParser:

    def __init__(self):
        self.title:            str                     = "Sequence Diagram"
        self.participants:     Dict[str, Participant]  = {}
        self.participant_order: List[str]              = []
        self.elements:         List[RawElem]           = []
        self._step   = 0
        self._fid    = 0
        self._fstack: List[int] = []

    # ── helpers ───────────────────────────────────────────────────────────

    def _s(self) -> int:
        self._step += 1
        return self._step

    @staticmethod
    def _clean(raw: str) -> str:
        return raw.strip().lstrip(":").strip()

    def _aliases_in(self, text: str) -> List[str]:
        """Extract known aliases from a string like 'UC, DB' or 'right of X'."""
        candidates = re.split(r'[\s,]+', text.replace("of", " "))
        return [c for c in candidates if c.strip() and c.strip() in self.participants]

    def _add_p(self, alias: str, raw: str, is_actor: bool, stereo: str):
        if alias not in self.participants:
            p = Participant(
                alias=alias, display_name=self._clean(raw),
                is_actor=is_actor, stereotype=stereo.strip().lower(),
                col_index=len(self.participant_order)
            )
            self.participants[alias] = p
            self.participant_order.append(alias)

    # ── main parse loop ───────────────────────────────────────────────────

    def parse(self, text: str):
        lines = text.splitlines()
        i = 0
        while i < len(lines):
            raw = lines[i].strip()

            if not raw or _R_SKIP.match(raw):
                i += 1; continue

            # title
            m = _R_TITLE.match(raw)
            if m:
                self.title = m.group(1).strip(); i += 1; continue

            # actor
            m = _R_ACTOR.match(raw)
            if m:
                self._add_p(m.group(2), m.group(1), is_actor=True,  stereo=m.group(3) or "")
                i += 1; continue

            # participant
            m = _R_PART.match(raw)
            if m:
                self._add_p(m.group(2), m.group(1), is_actor=False, stereo=m.group(3) or "")
                i += 1; continue

            # note inline: "note right of X : text"
            m = _R_NINLINE.match(raw)
            if m:
                pos, rest, text_ = m.group(1).lower().strip(), m.group(2), m.group(3)
                targets = self._aliases_in(rest)
                self.elements.append(RawElem("note",
                    NoteEvent(text_.replace("\\n", "\n"), pos, targets, self._s()), self._step))
                i += 1; continue

            # note block: "note right of X … end note"
            m = _R_NSTART.match(raw)
            if m:
                pos, rest = m.group(1).lower().strip(), (m.group(2) or "").strip()
                targets = self._aliases_in(rest)
                body: List[str] = []
                i += 1
                while i < len(lines):
                    nl = lines[i].strip()
                    if _R_NEND.match(nl): i += 1; break
                    body.append(nl)
                    i += 1
                txt = "\n".join(body).replace("\\n", "\n")
                self.elements.append(RawElem("note",
                    NoteEvent(txt, pos, targets, self._s()), self._step))
                continue

            # activate / deactivate
            m = _R_ACT.match(raw)
            if m:
                self.elements.append(RawElem("act",
                    ActivationEvent(m.group(1), True,  self._s()), self._step))
                i += 1; continue

            m = _R_DEACT.match(raw)
            if m:
                self.elements.append(RawElem("act",
                    ActivationEvent(m.group(1), False, self._s()), self._step))
                i += 1; continue

            # fragment start
            m = _R_FSTART.match(raw)
            if m:
                fkind = m.group(1).lower()
                guard = (m.group(2) or "").strip().strip("[]")
                self._fid += 1; fid = self._fid
                self._fstack.append(fid)
                self.elements.append(RawElem("frag",
                    FragmentEvent("start", fkind, guard, fid, self._s()), self._step))
                i += 1; continue

            # fragment else
            m = _R_FELSE.match(raw)
            if m:
                guard = (m.group(1) or "").strip().strip("[]")
                fid   = self._fstack[-1] if self._fstack else 0
                self.elements.append(RawElem("frag",
                    FragmentEvent("else", "", guard, fid, self._s()), self._step))
                i += 1; continue

            # fragment end
            m = _R_FEND.match(raw)
            if m:
                fid = self._fstack.pop() if self._fstack else 0
                self.elements.append(RawElem("frag",
                    FragmentEvent("end", "", "", fid, self._s()), self._step))
                i += 1; continue

            # message
            m = _R_MSG.match(raw)
            if m:
                src, arrow, dst = m.group(1), m.group(2), m.group(3)
                lbl = m.group(4).strip().replace("\\n", "\n")
                is_ret = "-->" in arrow
                self.elements.append(RawElem("msg",
                    MsgEvent(src, dst, lbl, is_ret, self._s()), self._step))
                i += 1; continue

            i += 1


# ═════════════════════════════════════════════════════════════════════════
#  LAYOUT ENGINE
# ═════════════════════════════════════════════════════════════════════════

@dataclass
class LMsg:
    src: str; dst: str; label: str; is_return: bool; y: float

@dataclass
class LAct:
    alias: str; y_start: float; y_end: float; depth: int = 0

@dataclass
class LNote:
    text: str; position: str; targets: List[str]; y: float

@dataclass
class LFrag:
    fkind:     str
    guard:     str
    y_start:   float
    y_end:     float
    else_dividers: List[Tuple[str, float]]
    frag_id:   int


class LayoutEngine:

    def __init__(self, parser: PumlParser):
        self.parser  = parser
        self.px:     Dict[str, float] = {}   # alias → left-edge X
        self.cx:     Dict[str, float] = {}   # alias → center X
        self.msgs:   List[LMsg]   = []
        self.acts:   List[LAct]   = []
        self.notes:  List[LNote]  = []
        self.frags:  List[LFrag]  = []
        self._open_acts:  Dict[str, List[Tuple[float, int]]] = {}
        self._open_frags: Dict[int, dict] = {}
        self._max_step = 0

    def _y(self, step: int) -> float:
        """Convert a logical step number to an absolute Y pixel."""
        header_bottom = MARGIN_TOP + P_H   # actors use the same row
        return header_bottom + FIRST_STEP_Y_OFFSET + step * STEP_H

    def _calc_x(self):
        for alias in self.parser.participant_order:
            p = self.parser.participants[alias]
            left = MARGIN_LEFT + p.col_index * P_COL
            self.px[alias] = left
            self.cx[alias] = left + (A_W / 2 if p.is_actor else P_W / 2)

    def layout(self):
        self._calc_x()
        for elem in self.parser.elements:
            y = self._y(elem.step)
            self._max_step = max(self._max_step, elem.step)

            if elem.kind == "msg":
                d: MsgEvent = elem.data
                self.msgs.append(LMsg(d.src, d.dst, d.label, d.is_return, y))

            elif elem.kind == "act":
                d: ActivationEvent = elem.data
                alias = d.alias
                if d.is_start:
                    depth = len(self._open_acts.get(alias, []))
                    self._open_acts.setdefault(alias, []).append((y, depth))
                else:
                    stack = self._open_acts.get(alias, [])
                    if stack:
                        sy, depth = stack.pop()
                        self.acts.append(LAct(alias, sy, y, depth))

            elif elem.kind == "note":
                d: NoteEvent = elem.data
                self.notes.append(LNote(d.text, d.position, d.targets, y))

            elif elem.kind == "frag":
                d: FragmentEvent = elem.data
                fid = d.frag_id
                if d.sub_kind == "start":
                    self._open_frags[fid] = {
                        "fkind": d.frag_kind, "guard": d.guard,
                        "y_start": y - FRAG_PAD_TOP,
                        "else_dividers": []
                    }
                elif d.sub_kind == "else":
                    if fid in self._open_frags:
                        self._open_frags[fid]["else_dividers"].append((d.guard, y))
                elif d.sub_kind == "end":
                    info = self._open_frags.pop(fid, None)
                    if info:
                        self.frags.append(LFrag(
                            fkind=info["fkind"], guard=info["guard"],
                            y_start=info["y_start"],
                            y_end=y + FRAG_PAD_BOT,
                            else_dividers=info["else_dividers"],
                            frag_id=fid
                        ))

        # Close any unclosed activation bars
        tail_y = self._y(self._max_step + 2)
        for alias, stack in self._open_acts.items():
            for sy, depth in stack:
                self.acts.append(LAct(alias, sy, tail_y, depth))

    def total_height(self) -> float:
        return self._y(self._max_step + 3)

    def total_width(self) -> float:
        n = len(self.parser.participant_order)
        return MARGIN_LEFT * 2 + n * P_COL


# ═════════════════════════════════════════════════════════════════════════
#  DRAW.IO XML GENERATOR  —  Traditional Black & White UML
# ═════════════════════════════════════════════════════════════════════════

# ── STYLE CONSTANTS ───────────────────────────────────────────────────────

# White fill, solid black stroke → standard UML participant box
_S_PARTICIPANT = (
    "shape=mxgraph.uml.entity2;"             # plain rectangle in draw.io UML lib
    "whiteSpace=wrap;html=1;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
    "fontSize=10;fontStyle=0;"
    "verticalAlign=middle;align=center;"
)

# Same box but override shape for plain rectangle (works in all draw.io versions)
_S_PART_PLAIN = (
    "rounded=0;whiteSpace=wrap;html=1;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
    "fontSize=10;fontStyle=0;"
    "verticalAlign=middle;align=center;"
)

_S_ACTOR = (
    "shape=mxgraph.uml.actor2;whiteSpace=wrap;html=1;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
    "labelPosition=center;verticalLabelPosition=bottom;"
    "verticalAlign=top;align=center;fontSize=10;fontStyle=0;"
)

_S_LIFELINE = (
    "endArrow=none;html=1;dashed=1;"
    "strokeColor=#000000;strokeWidth=1;dashPattern=8 4;"
)

_S_ACTIVATION = (
    "rounded=0;whiteSpace=wrap;html=1;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
)

# Sync message: solid line + solid filled triangle arrowhead
_S_MSG_SYNC = (
    "endArrow=block;endFill=1;html=1;dashed=0;exitX=0.5;exitY=0;"
    "strokeColor=#000000;strokeWidth=1.5;fontSize=9;"
    "align=center;labelBackgroundColor=none;labelBorderColor=none;"
    "edgeStyle=none;"
)

# Return message: dashed line + open arrowhead
_S_MSG_RETURN = (
    "endArrow=open;endFill=0;html=1;dashed=1;dashPattern=6 3;"
    "strokeColor=#000000;strokeWidth=1;fontSize=9;"
    "align=center;labelBackgroundColor=none;labelBorderColor=none;"
    "edgeStyle=none;"
)

# Self-message (sync)
_S_SELF_SYNC = (
    "endArrow=block;endFill=1;html=1;dashed=0;"
    "strokeColor=#000000;strokeWidth=1.5;fontSize=9;"
    "align=left;labelBackgroundColor=none;labelBorderColor=none;"
    "edgeStyle=orthogonalEdgeStyle;"
)

# Self-message (return)
_S_SELF_RET = (
    "endArrow=open;endFill=0;html=1;dashed=1;dashPattern=6 3;"
    "strokeColor=#000000;strokeWidth=1;fontSize=9;"
    "align=left;labelBackgroundColor=none;labelBorderColor=none;"
    "edgeStyle=orthogonalEdgeStyle;"
)

# Combined fragment outer frame
_S_FRAG_FRAME = (
    "rounded=0;whiteSpace=wrap;html=1;"
    "fillColor=none;strokeColor=#000000;strokeWidth=1.5;"
    "fontSize=9;fontStyle=1;"
    "verticalAlign=top;align=left;spacingLeft=4;spacingTop=2;"
)

# Guard label (pentagon / small box in top-left of fragment)
_S_FRAG_GUARD = (
    "shape=mxgraph.uml.message2;html=1;whiteSpace=wrap;"      # pentagon shape
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
    "fontSize=9;fontStyle=0;align=center;verticalAlign=middle;"
)

# Else divider (dashed horizontal line inside fragment)
_S_ELSE_LINE = (
    "endArrow=none;html=1;dashed=1;dashPattern=6 3;"
    "strokeColor=#000000;strokeWidth=1;"
)

# Else guard text
_S_ELSE_LABEL = (
    "text;html=1;align=left;fontSize=9;fontStyle=2;"
    "labelBackgroundColor=none;"
)

# Note (dog-ear rectangle)
_S_NOTE = (
    "shape=note;whiteSpace=wrap;html=1;backgroundOutline=1;size=10;"
    "fillColor=#ffffff;strokeColor=#000000;strokeWidth=1;"
    "fontSize=8;align=left;spacingLeft=8;spacingTop=4;verticalAlign=top;"
)

# Title text block
_S_TITLE = (
    "text;html=1;align=center;strokeColor=none;fillColor=none;"
    "fontSize=12;fontStyle=1;"
)


class DrawioGenerator:

    def __init__(self, parser: PumlParser, layout: LayoutEngine):
        self.p  = parser
        self.l  = layout
        self._id = 2
        self._cells: List[ET.Element] = []

    # ── ID factory ────────────────────────────────────────────────────────
    def _nid(self) -> str:
        c = self._id; self._id += 1; return str(c)

    # ── XML cell factories ────────────────────────────────────────────────

    def _vtx(self, val: str, style: str,
              x: float, y: float, w: float, h: float) -> ET.Element:
        """Add a vertex (shape) cell."""
        cell = ET.Element("mxCell", {
            "id": self._nid(), "value": val, "style": style,
            "vertex": "1", "parent": "1"
        })
        ET.SubElement(cell, "mxGeometry", {
            "x": f"{x:.1f}", "y": f"{y:.1f}",
            "width": f"{w:.1f}", "height": f"{h:.1f}",
            "as": "geometry"
        })
        self._cells.append(cell)
        return cell

    def _edg(self, val: str, style: str,
              sx: float, sy: float, tx: float, ty: float,
              pts: Optional[List[Tuple[float,float]]] = None) -> ET.Element:
        """Add an edge (line/arrow) cell with absolute source/target points."""
        cell = ET.Element("mxCell", {
            "id": self._nid(), "value": val, "style": style,
            "edge": "1", "parent": "1"
        })
        geo = ET.SubElement(cell, "mxGeometry", {"relative": "1", "as": "geometry"})
        ET.SubElement(geo, "mxPoint", {"x": f"{sx:.1f}", "y": f"{sy:.1f}", "as": "sourcePoint"})
        ET.SubElement(geo, "mxPoint", {"x": f"{tx:.1f}", "y": f"{ty:.1f}", "as": "targetPoint"})
        if pts:
            arr = ET.SubElement(geo, "Array", {"as": "points"})
            for px, py in pts:
                ET.SubElement(arr, "mxPoint", {"x": f"{px:.1f}", "y": f"{py:.1f}"})
        self._cells.append(cell)
        return cell

    # ── Text helpers ──────────────────────────────────────────────────────

    @staticmethod
    def _esc(text: str) -> str:
        """HTML-escape and convert newlines → <br/> for draw.io labels."""
        text = (text.replace("&", "&amp;")
                    .replace("<", "&lt;")
                    .replace(">", "&gt;"))
        text = text.replace("\n", "<br/>")
        return text

    def _cx(self, alias: str) -> float:
        return self.l.cx.get(alias, MARGIN_LEFT + P_W / 2)

    def _px_left(self, alias: str) -> float:
        return self.l.px.get(alias, MARGIN_LEFT)

    def _p_right(self, alias: str) -> float:
        p = self.p.participants.get(alias)
        w = A_W if (p and p.is_actor) else P_W
        return self._px_left(alias) + w

    # ═══════════════════════════════════════════════════════════════════════
    #  DRAW METHODS
    # ═══════════════════════════════════════════════════════════════════════

    # ── 1. Title ──────────────────────────────────────────────────────────
    def _draw_title(self):
        tw = self.l.total_width()
        self._vtx(
            val=self._esc(self.p.title),
            style=_S_TITLE,
            x=MARGIN_LEFT, y=0,
            w=max(tw - MARGIN_LEFT * 2, 400), h=MARGIN_TOP
        )

    # ── 2. Participant headers ─────────────────────────────────────────────
    def _part_label(self, participant: Participant) -> str:
        """
        Traditional UML box label:
          «stereotype»      ← 8pt, centred
          ─────────────     (implied by the box top line)
          ClassName         ← 10pt bold, centred
        """
        name = self._esc(participant.display_name)
        if participant.stereotype:
            stereo = self._esc(f"«{participant.stereotype}»")
            # two-line label: stereotype (small italic) + name (bold)
            return (f'<font style="font-size:8px"><i>{stereo}</i></font>'
                    f'<br/><b>{name}</b>')
        return f'<b>{name}</b>'

    def _draw_participants(self):
        for alias in self.p.participant_order:
            part = self.p.participants[alias]
            left = self.l.px[alias]

            if part.is_actor:
                # ── Stick figure (UML actor) ──────────────────────────
                # Centre actor horizontally within its column
                actor_left = left + (P_W - A_W) / 2   # keep same column width
                self._vtx(
                    val=self._esc(part.display_name),
                    style=_S_ACTOR,
                    x=actor_left,
                    y=MARGIN_TOP + (P_H - A_H) / 2,  # vertically align with boxes
                    w=A_W, h=A_H
                )
                # Update centre X to reflect actor offset
                self.l.cx[alias] = actor_left + A_W / 2
            else:
                # ── White rectangle participant box ────────────────────
                self._vtx(
                    val=self._part_label(part),
                    style=_S_PART_PLAIN,
                    x=left, y=MARGIN_TOP,
                    w=P_W, h=P_H
                )

    # ── 3. Lifelines ──────────────────────────────────────────────────────
    def _draw_lifelines(self):
        total_h = self.l.total_height()
        for alias in self.p.participant_order:
            part = self.p.participants[alias]
            cx   = self._cx(alias)

            if part.is_actor:
                y_top = MARGIN_TOP + P_H   # bottom of actor row
            else:
                y_top = MARGIN_TOP + P_H + LL_GAP

            self._edg(
                val="", style=_S_LIFELINE,
                sx=cx, sy=y_top,
                tx=cx, ty=total_h
            )

    # ── 4. Activation bars ────────────────────────────────────────────────
    def _draw_activations(self):
        for act in self.l.acts:
            alias = act.alias
            if alias not in self.l.cx:
                continue
            cx     = self._cx(alias)
            offset = act.depth * ACT_NEST_OFF
            bx     = cx - ACT_W / 2 + offset
            bh     = max(act.y_end - act.y_start, STEP_H * 0.5)
            self._vtx(
                val="", style=_S_ACTIVATION,
                x=bx, y=act.y_start,
                w=ACT_W, h=bh
            )

    # ── 5. Messages ───────────────────────────────────────────────────────
    def _draw_messages(self):
        for msg in self.l.msgs:
            sa = msg.src if msg.src in self.l.cx else (
                self.p.participant_order[0] if self.p.participant_order else msg.src)
            da = msg.dst if msg.dst in self.l.cx else (
                self.p.participant_order[-1] if self.p.participant_order else msg.dst)

            scx = self._cx(sa)
            dcx = self._cx(da)
            lbl = self._esc(msg.label)
            y   = msg.y

            if sa == da or msg.src == msg.dst:
                # ── Self-message ──────────────────────────────────────
                sty = _S_SELF_RET if msg.is_return else _S_SELF_SYNC
                loop_x = scx + SELF_LOOP_W
                y2     = y + SELF_LOOP_H
                self._edg(
                    val=lbl, style=sty,
                    sx=scx, sy=y,
                    tx=scx, ty=y2,
                    pts=[(loop_x, y), (loop_x, y2)]
                )
            else:
                # ── Cross-participant message ──────────────────────────
                sty = _S_MSG_RETURN if msg.is_return else _S_MSG_SYNC
                self._edg(
                    val=lbl, style=sty,
                    sx=scx, sy=y,
                    tx=dcx, ty=y
                )

    # ── 6. Combined fragments ─────────────────────────────────────────────
    def _draw_fragments(self):
        if not self.p.participant_order:
            return

        # Global fragment X span (covers all participants)
        all_lefts  = [self.l.px[a] for a in self.p.participant_order]
        last_alias = self.p.participant_order[-1]
        last_p     = self.p.participants[last_alias]
        last_right = self.l.px[last_alias] + (A_W if last_p.is_actor else P_W)

        gx = min(all_lefts) - FRAG_PAD_X
        gr = last_right     + FRAG_PAD_X
        gw = gr - gx

        for frag in self.l.frags:
            fy = frag.y_start
            fh = frag.y_end - fy

            # ── Outer frame (no fill so lifelines show through) ────────
            self._vtx(
                val="",
                style=_S_FRAG_FRAME,
                x=gx, y=fy, w=gw, h=fh
            )

            # ── Fragment type keyword (top-left, bold) ─────────────────
            # In classic UML, the type + guard share a pentagon in top-left.
            # We render: type keyword (bold) inside a small box, then
            # guard text immediately after in brackets.
            type_label = f'<b>{frag.fkind.upper()}</b>'
            guard_txt  = f" [{self._esc(frag.guard)}]" if frag.guard else ""
            self._vtx(
                val=type_label + guard_txt,
                style=_S_FRAG_GUARD,
                x=gx, y=fy,
                w=max(FRAG_HDR_W + len(frag.guard) * 5, FRAG_HDR_W),
                h=FRAG_HDR_H
            )

            # ── Else dividers ──────────────────────────────────────────
            for else_guard, else_y in frag.else_dividers:
                # Dashed horizontal separator
                self._edg(
                    val="", style=_S_ELSE_LINE,
                    sx=gx, sy=else_y,
                    tx=gx + gw, ty=else_y
                )
                # "else" guard label
                if else_guard:
                    self._vtx(
                        val=self._esc(f"[{else_guard}]"),
                        style=_S_ELSE_LABEL,
                        x=gx + 4, y=else_y + 2,
                        w=min(gw - 8, 300), h=16
                    )

    # ── 7. Notes ──────────────────────────────────────────────────────────
    def _draw_notes(self):
        for note in self.l.notes:
            txt = note.text.strip()
            if not txt:
                continue
            lbl = self._esc(txt)
            # Estimate height
            n_lines = max(txt.count("\n") + 1, 1)
            nh = max(NOTE_MIN_H, n_lines * NOTE_LINE_H + 16)

            pos     = note.position
            targets = note.targets

            # ── Determine X position ──────────────────────────────────
            if pos == "top":
                nx = MARGIN_LEFT
                ny = note.y

            elif pos in ("right of", "right"):
                alias = targets[0] if targets else self.p.participant_order[-1]
                nx    = self._p_right(alias) + NOTE_GAP
                ny    = note.y

            elif pos in ("left of", "left"):
                alias = targets[0] if targets else self.p.participant_order[0]
                nx    = self._px_left(alias) - NOTE_W - NOTE_GAP
                ny    = note.y

            elif pos == "over":
                if len(targets) >= 2:
                    x1 = self._px_left(targets[0])
                    x2 = self._p_right(targets[-1])
                    nx = x1; nw_tmp = x2 - x1
                else:
                    alias = targets[0] if targets else self.p.participant_order[0]
                    nx    = self._px_left(alias)
                    nw_tmp = NOTE_W
                self._vtx(val=lbl, style=_S_NOTE, x=nx, y=note.y,
                           w=max(nw_tmp, NOTE_W), h=nh)
                continue
            else:
                nx = MARGIN_LEFT; ny = note.y

            self._vtx(val=lbl, style=_S_NOTE, x=nx, y=ny, w=NOTE_W, h=nh)

    # ── Master generator ──────────────────────────────────────────────────

    def generate_xml(self) -> str:
        # Draw order (bottom → top layer):
        # 1. Fragments  2. Lifelines  3. Activation bars
        # 4. Messages   5. Notes      6. Participant headers  7. Title
        self._draw_fragments()
        self._draw_lifelines()
        self._draw_activations()
        self._draw_messages()
        self._draw_notes()
        self._draw_participants()
        self._draw_title()

        pw = int(self.l.total_width()) + 60
        ph = int(self.l.total_height()) + 60

        mxfile = ET.Element("mxfile", {
            "host":     "app.diagrams.net",
            "modified": "2026-04-13",
            "agent":    "puml_seq_to_drawio.py v2.0",
            "version":  "21.0",
            "type":     "device"
        })
        diag = ET.SubElement(mxfile, "diagram", {
            "id": "seq", "name": self.p.title
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


# ═════════════════════════════════════════════════════════════════════════
#  PUBLIC API
# ═════════════════════════════════════════════════════════════════════════

def convert(puml_text: str) -> str:
    """Convert PlantUML sequence text → draw.io XML string."""
    parser = PumlParser()
    parser.parse(puml_text)
    engine = LayoutEngine(parser)
    engine.layout()
    gen = DrawioGenerator(parser, engine)
    return gen.generate_xml()


def convert_file(src: Path, dst: Path) -> dict:
    text = src.read_text(encoding="utf-8")
    xml  = convert(text)
    dst.write_text(xml, encoding="utf-8")
    # Stats
    p = PumlParser(); p.parse(text)
    return {
        "input":        src.name,
        "output":       dst.name,
        "title":        p.title,
        "participants": len(p.participants),
        "elements":     len(p.elements),
    }


# ═════════════════════════════════════════════════════════════════════════
#  CLI
# ═════════════════════════════════════════════════════════════════════════

def main():
    argv = sys.argv[1:]
    if not argv or argv[0] in ("-h", "--help"):
        print(__doc__)
        sys.exit(0)

    target = Path(argv[0])

    # ── Batch mode ────────────────────────────────────────────────────────
    if target.is_dir():
        files = sorted(target.glob("04*.puml")) or sorted(target.glob("*.puml"))
        if not files:
            print(f"No .puml files found in: {target}")
            sys.exit(1)

        print(f"\nBatch converting {len(files)} file(s)  [{target}]\n")
        ok = fail = 0
        for f in files:
            out = f.with_suffix(".drawio")
            try:
                info = convert_file(f, out)
                print(f"  OK  {info['input']}")
                print(f"      -> {info['output']}")
                print(f"      Title       : {info['title']}")
                print(f"      Participants: {info['participants']}  "
                      f"Elements: {info['elements']}\n")
                ok += 1
            except Exception as e:
                print(f"  FAIL  {f.name}: {e}")
                import traceback; traceback.print_exc()
                fail += 1
        print(f"Done: {ok} succeeded, {fail} failed.")
        return

    # ── Single file mode ──────────────────────────────────────────────────
    if not target.is_file():
        print(f"Error: '{target}' is not a file or directory.")
        sys.exit(1)

    out = Path(argv[1]) if len(argv) >= 2 else target.with_suffix(".drawio")
    try:
        info = convert_file(target, out)
        print(f"\nConverted successfully!")
        print(f"  Input       : {info['input']}")
        print(f"  Output      : {info['output']}")
        print(f"  Title       : {info['title']}")
        print(f"  Participants: {info['participants']}  Elements: {info['elements']}")
    except Exception as e:
        print(f"Conversion failed: {e}")
        import traceback; traceback.print_exc()
        sys.exit(1)


if __name__ == "__main__":
    main()
