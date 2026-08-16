"""Render the architecture diagrams used by the README and ARCHITECTURE.md.

Why this exists
---------------
The diagrams are also written as Mermaid inside Documentation/ARCHITECTURE.md, which is the
better format for editing them. GitHub renders Mermaid, but plenty of other markdown viewers do
not, so the README embeds PNGs instead and this script is what produces them.

Matplotlib rather than Graphviz or mermaid-cli on purpose: both of those need a system binary
(dot, or a headless Chromium), and requiring one turns "regenerate the docs" into "install a
toolchain first". Matplotlib draws these with rectangles and arrows and nothing else.

Usage
-----
    py -3.13 -m venv Doc/.venv
    Doc/.venv/Scripts/python -m pip install -r Tools/diagrams/requirements.txt
    Doc/.venv/Scripts/python Tools/diagrams/generate_diagrams.py

Output lands in Documentation/diagrams/ and is committed; the venv under Doc/ is not.
"""

from __future__ import annotations

import pathlib

import matplotlib

matplotlib.use("Agg")

import matplotlib.pyplot as plt
from matplotlib.patches import FancyArrowPatch, FancyBboxPatch

# The game's palette, so the docs look like the thing they document.
BACKDROP = "#0e1018"
PANEL = "#1d2430"
INK = "#efece3"
MUTED = "#9aa0ad"
LIGHT = "#ffc65c"
WARD = "#75b8ff"
DANGER = "#e6574d"
GOOD = "#74cc80"
EDGE = "#3a4356"

OUT = pathlib.Path(__file__).resolve().parents[2] / "Documentation" / "diagrams"


def canvas(width: float, height: float):
    fig, ax = plt.subplots(figsize=(width, height))
    fig.patch.set_facecolor(BACKDROP)
    ax.set_facecolor(BACKDROP)
    ax.set_xlim(0, 100)
    ax.set_ylim(0, 100)
    ax.axis("off")
    return fig, ax


def box(ax, x, y, w, h, text, *, fill=PANEL, edge=EDGE, fg=INK, size=10, weight="normal"):
    ax.add_patch(
        FancyBboxPatch(
            (x, y),
            w,
            h,
            boxstyle="round,pad=0.6,rounding_size=1.6",
            linewidth=1.6,
            facecolor=fill,
            edgecolor=edge,
        )
    )
    ax.text(
        x + w / 2,
        y + h / 2,
        text,
        ha="center",
        va="center",
        color=fg,
        fontsize=size,
        fontweight=weight,
        linespacing=1.45,
    )
    return x + w / 2, y + h / 2


def group(ax, x, y, w, h, title, *, edge=EDGE):
    ax.add_patch(
        FancyBboxPatch(
            (x, y),
            w,
            h,
            boxstyle="round,pad=0.8,rounding_size=2",
            linewidth=1.8,
            linestyle=(0, (6, 4)),
            facecolor="none",
            edgecolor=edge,
        )
    )
    # Clear of the dashed border, which otherwise runs straight through the text.
    ax.text(x + 1.0, y + h + 4.2, title, ha="left", va="center", color=edge, fontsize=9.5,
            fontweight="bold")


def arrow(ax, start, end, *, colour=MUTED, style="-|>", dashed=False, rad=0.0, label=None,
          label_dx=0.0, label_dy=1.8):
    ax.add_patch(
        FancyArrowPatch(
            start,
            end,
            arrowstyle=style,
            mutation_scale=14,
            linewidth=1.5,
            color=colour,
            linestyle=(0, (4, 3)) if dashed else "solid",
            connectionstyle=f"arc3,rad={rad}",
            shrinkA=6,
            shrinkB=6,
        )
    )
    if label:
        mx = (start[0] + end[0]) / 2 + label_dx
        my = (start[1] + end[1]) / 2 + label_dy
        ax.text(mx, my, label, ha="center", va="center", color=colour, fontsize=8.5)


def save(fig, name: str) -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"{name}.png"
    fig.savefig(path, dpi=200, facecolor=BACKDROP, bbox_inches="tight", pad_inches=0.25)
    plt.close(fig)
    print(f"  wrote {path.relative_to(path.parents[2])}")


# --------------------------------------------------------------------------- 1. layers


def diagram_layers() -> None:
    fig, ax = canvas(11, 6.6)
    ax.text(50, 96, "Assembly layers", ha="center", color=INK, fontsize=14, fontweight="bold")
    ax.text(50, 90.5, "the arrow direction is enforced by assembly definitions, not convention",
            ha="center", color=MUTED, fontsize=9.5)

    group(ax, 4, 56, 92, 24, "LastLight.Presentation   (MonoBehaviours)", edge="#a86ba8")
    a = box(ax, 8, 61, 24, 12, "GameSession\nowns the run", fill="#2a2233", edge="#a86ba8")
    b = box(ax, 38, 61, 24, 12, "ScreenRouter\none screen at a time", fill="#2a2233", edge="#a86ba8")
    c = box(ax, 68, 61, 24, 12, "Views\ncard · actor · screens", fill="#2a2233", edge="#a86ba8")

    group(ax, 4, 12, 92, 36, "LastLight.Gameplay   (plain C#, zero MonoBehaviours)", edge="#5b7fa8")
    d = box(ax, 8, 31, 24, 12, "RunController\nRunState", fill="#1d2430", edge="#5b7fa8")
    e = box(ax, 38, 31, 24, 12, "CombatController\nCombatState", fill="#1d2430", edge="#5b7fa8")
    f = box(ax, 68, 31, 24, 12, "DeckService\ndraw · hand · discard", fill="#1d2430", edge="#5b7fa8")
    # Each lower box sits directly beneath its parent, so no arrow crosses another.
    h = box(ax, 38, 16, 24, 11, "CardEffect atoms", fill="#1d2430", edge="#5b7fa8")
    g = box(ax, 68, 16, 24, 11, "CardDefinition\nRuntimeCard", fill="#1d2430", edge="#5b7fa8")

    arrow(ax, (a[0], 61), (d[0], 43), colour="#7f8ba0")
    arrow(ax, (c[0], 61), (e[0], 43), colour="#7f8ba0")
    arrow(ax, (32, 37), (38, 37), colour=MUTED)
    arrow(ax, (62, 37), (68, 37), colour=MUTED)
    arrow(ax, (e[0], 31), (h[0], 27), colour=MUTED)
    arrow(ax, (f[0], 31), (g[0], 27), colour=MUTED)

    ax.text(50, 5.5, "Gameplay cannot reference Presentation, so every rule is testable with no scene loaded",
            ha="center", color=GOOD, fontsize=9.5, style="italic")
    save(fig, "01-layers")


# ------------------------------------------------------------ 2. card to resolution


def diagram_card_flow() -> None:
    fig, ax = canvas(12, 5.2)
    ax.text(50, 94, "From authored data to an applied effect", ha="center", color=INK,
            fontsize=14, fontweight="bold")

    a = box(ax, 1, 55, 16, 15, "CardCatalog\nC# table", fill="#232a22", edge="#7aa86b", size=9.5)
    b = box(ax, 21, 55, 17, 15, "CardDefinition\nScriptableObject\nread only", size=9.5)
    c = box(ax, 42, 55, 16, 15, "RuntimeCard\nIsUpgraded", fill="#26202e", edge="#a86ba8", size=9.5)
    d = box(ax, 62, 55, 15, 15, "DeckService\nhand", size=9.5)
    e = box(ax, 81, 55, 18, 15, "TryPlayCard\nvalidated", fill="#2b2320", edge=DANGER, size=9.5)

    f = box(ax, 55, 22, 20, 14, "EffectContext\nthe only surface\nan effect sees", fill="#20262f",
            edge=WARD, size=9.5)
    g = box(ax, 30, 22, 18, 14, "CardEffect\nResolve()", size=9.5)
    h = box(ax, 3, 22, 20, 14, "CombatState\nLight · Ward · Focus", size=9.5)

    arrow(ax, (17, 62), (21, 62), label="generated once", label_dy=3.4)
    arrow(ax, (38, 62), (42, 62), label="referenced,\nnever written", label_dy=4.6)
    arrow(ax, (58, 62), (62, 62))
    arrow(ax, (77, 62), (81, 62))
    arrow(ax, (89, 55), (70, 36), colour=DANGER)
    arrow(ax, (55, 29), (48, 29))
    arrow(ax, (30, 29), (23, 29))

    i = box(ax, 81, 22, 18, 14, "Rules text\non the card", fill="#232a22", edge="#7aa86b", size=9.5)
    arrow(ax, (30, 55), (85, 36), colour=GOOD, dashed=True, rad=-0.28)
    arrow(ax, (44, 24), (81, 26), colour=GOOD, dashed=True, rad=0.18)

    ax.text(50, 9, "the dotted paths are why printed text cannot drift from behaviour: both come from the same effects",
            ha="center", color=GOOD, fontsize=9, style="italic")
    save(fig, "02-card-flow")


# ------------------------------------------------------------------- 3. turn machine


def diagram_turn_machine() -> None:
    fig, ax = canvas(10.5, 6.4)
    ax.text(50, 96, "Combat turn machine", ha="center", color=INK, fontsize=14, fontweight="bold")
    ax.text(50, 90.5, "PlayerAction is the only phase in which a card can be played",
            ha="center", color=MUTED, fontsize=9.5)

    a = box(ax, 36, 76, 28, 10, "CombatStart", size=10)
    b = box(ax, 33, 60, 34, 11, "PlayerTurnStart\nexpire Ward · tick statuses\nrefill Focus · draw 5",
            size=9)
    c = box(ax, 33, 44, 34, 10, "PlayerAction", fill="#2b2320", edge=LIGHT, fg=LIGHT, size=11,
            weight="bold")
    d = box(ax, 33, 29, 34, 9, "PlayerTurnEnd\ndiscard hand", size=9)
    e = box(ax, 33, 14, 34, 9, "ResolveCheck", size=10)
    f = box(ax, 74, 29, 22, 9, "EnemyTurn\nresolve intent", size=9)
    g = box(ax, 4, 14, 24, 9, "CombatEnd", fill="#2b2320", edge=DANGER, fg=DANGER, size=10)

    arrow(ax, (50, 76), (50, 71))
    arrow(ax, (50, 60), (50, 54))
    arrow(ax, (50, 44), (50, 38))
    arrow(ax, (50, 29), (50, 23))
    arrow(ax, (67, 18), (85, 29), label="both alive", label_dy=-3.2)
    arrow(ax, (85, 29), (67, 20), rad=0.0)
    arrow(ax, (33, 18), (28, 18), colour=DANGER, label="someone died", label_dy=3.2)
    arrow(ax, (33, 20), (20, 60), colour=MUTED, rad=0.3, label="loop", label_dx=-6)
    arrow(ax, (67, 49), (80, 49), colour=LIGHT, rad=0.0)
    ax.text(88, 49, "TryPlayCard\nrepeatable", ha="center", va="center", color=LIGHT, fontsize=8.5)

    ax.text(50, 6, "a card that kills mid-turn ends the combat immediately: TryPlayCard re-checks the outcome",
            ha="center", color=MUTED, fontsize=9, style="italic")
    save(fig, "03-turn-machine")


# ------------------------------------------------------------------ 4. deck lifecycle


def diagram_deck() -> None:
    fig, ax = canvas(11, 4.6)
    ax.text(50, 93, "Deck lifecycle", ha="center", color=INK, fontsize=14, fontweight="bold")

    a = box(ax, 2, 52, 22, 15, "Run deck\npersists all run", fill="#26202e", edge="#a86ba8", size=10)
    b = box(ax, 32, 52, 18, 15, "Draw pile", size=10.5)
    c = box(ax, 58, 52, 18, 15, "Hand", fill="#2b2320", edge=LIGHT, fg=LIGHT, size=10.5)
    d = box(ax, 82, 52, 17, 15, "Discard", size=10.5)

    arrow(ax, (24, 59), (32, 59), label="new combat:\nshuffle", label_dy=5)
    arrow(ax, (50, 59), (58, 59), label="draw 5\neach turn", label_dy=5)
    arrow(ax, (76, 59), (82, 59), label="played, or\nturn ends", label_dy=5)
    arrow(ax, (90, 52), (41, 52), colour=WARD, rad=0.32)
    ax.text(65, 33, "draw pile empty: reshuffle the discard", ha="center", color=WARD, fontsize=9)

    ax.text(50, 17, "A played card leaves the hand BEFORE it resolves and enters the discard AFTER,",
            ha="center", color=INK, fontsize=9.5)
    ax.text(50, 11, "so a card that draws cards can never redraw itself mid-resolution.",
            ha="center", color=INK, fontsize=9.5)
    ax.text(50, 4, "with every pile empty, Draw returns what it could rather than looping forever",
            ha="center", color=MUTED, fontsize=8.8, style="italic")
    save(fig, "04-deck-lifecycle")


# ----------------------------------------------------------------------- 5. run flow


def diagram_run_flow() -> None:
    fig, ax = canvas(13.5, 6.0)
    ax.text(50, 96, "Run flow", ha="center", color=INK, fontsize=14, fontweight="bold")
    ax.text(50, 90.5, "the run is a list of nodes in an asset; nothing in the code knows there are three fights",
            ha="center", color=MUTED, fontsize=9.5)

    # One straight chain: every node in a row, endings hanging below it.
    y, w, h = 68, 14, 14
    xs = [1, 17.8, 34.6, 51.4, 68.2, 85]

    box(ax, xs[0], y, w, h, "Main Menu\nscene", size=9)
    box(ax, xs[1], y, w, h, "Combat\nFledgling\nShade", fill="#2b2320", edge=DANGER, size=8.8)
    box(ax, xs[2], y, w, h, "Card Reward\n1 of 3,\nor skip", fill="#20262f", edge=WARD, size=8.8)
    box(ax, xs[3], y, w, h, "Combat\nGrasping\nMire", fill="#2b2320", edge=DANGER, size=8.8)
    box(ax, xs[4], y, w, h, "Shrine\nsharpen ·\nrelease · rest", fill="#20262f", edge=WARD, size=8.4)
    box(ax, xs[5], y, w, h, "Combat\nThe Devouring\nDark", fill="#2b2320", edge=DANGER, size=8.4)

    for i in range(5):
        arrow(ax, (xs[i] + w, y + h / 2), (xs[i + 1], y + h / 2),
              label="Begin" if i == 0 else None, label_dy=-11.5)

    summary = box(ax, 36, 34, 28, 13, "Run summary", fill="#232a22", edge=GOOD, size=11)
    newrun = box(ax, 36, 12, 28, 12, "New Run\nin place, no scene reload", fill="#232a22",
                 edge=GOOD, size=9.5)

    # Victory: off the end of the node list.
    arrow(ax, (92, 68), (64, 45), colour=GOOD)
    ax.text(87, 46, "past the last\nnode: victory", ha="center", color=GOOD, fontsize=8.6)

    # Defeat: from any combat, the moment Light hits zero.
    arrow(ax, (24.8, 68), (36, 45), colour=DANGER)
    ax.text(13, 52, "Light reaches 0\nat any point:\ndefeat", ha="center", color=DANGER, fontsize=8.6)

    arrow(ax, (50, 34), (50, 24), colour=GOOD)
    # Bows out to the left so it clears both the summary panel and the defeat arrow.
    arrow(ax, (36, 18), (21, 68), colour=GOOD, rad=-0.42)

    ax.text(50, 5.5, "Both endings funnel through RunEnded, so neither needs a special case. "
                     "Light and the deck carry between stages.",
            ha="center", color=MUTED, fontsize=8.8, style="italic")
    save(fig, "05-run-flow")


def main() -> None:
    print("Rendering diagrams into Documentation/diagrams/")
    diagram_layers()
    diagram_card_flow()
    diagram_turn_machine()
    diagram_deck()
    diagram_run_flow()
    print("done")


if __name__ == "__main__":
    main()
