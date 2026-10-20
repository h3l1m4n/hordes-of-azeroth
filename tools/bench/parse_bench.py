import argparse
import json
import re
import sys
from pathlib import Path

WINDOW_FROM = 1.0
WINDOW_TO = 30.0
FIELD_KILLS = 4
FIELD_SLOTS = 13
LINE = re.compile(r"^(\d+)/(\d+) (\d+):(\d+):(\d+(?:\.\d+)?)\s+(.*)$")
MARKER = re.compile(r"^To \S+: window pack (\S+)")
BENCH_NOTICE = "BONK~msg~Bench mode is on"


def clock_seconds(month, day, hours, minutes, seconds):
    return ((int(month) * 31 + int(day)) * 24 + int(hours)) * 3600 + int(minutes) * 60 + float(seconds)


def since_seconds(text):
    match = re.match(r"^(\d+)/(\d+) (\d+):(\d+):(\d+(?:\.\d+)?)$", text)
    if not match:
        sys.exit(f"bad --since value {text!r}, expected MM/DD HH:MM:SS")
    return clock_seconds(*match.groups())


def read_log(path, since):
    windows, states, bench = [], [], False
    for raw in open(path, encoding="utf-8", errors="replace"):
        match = LINE.match(raw.rstrip("\r\n"))
        if not match:
            continue
        seconds = clock_seconds(*match.groups()[:5])
        if seconds < since:
            continue
        text = match.group(6)
        marker = MARKER.match(text)
        if marker:
            windows.append((seconds, marker.group(1)))
        elif text.startswith(BENCH_NOTICE):
            bench = True
        elif text.startswith("BONK~state~"):
            parts = text.split("~")
            if len(parts) <= FIELD_SLOTS:
                continue
            slots = [tuple(int(v) for v in slot.split(":")) for slot in parts[FIELD_SLOTS].split(",") if slot]
            states.append((seconds, int(parts[FIELD_KILLS]), slots))
    return windows, states, bench


def measure(start, states, slot_count):
    inside = [s for s in states if start + WINDOW_FROM <= s[0] <= start + WINDOW_TO and len(s[2]) == slot_count]
    if len(inside) < 2:
        return None
    first, last = inside[0], inside[-1]
    kills = last[1] - first[1]
    dead_at = next(s[0] for s in inside if s[1] - first[1] >= kills)
    still_dying = dead_at >= last[0] and kills > 0
    span = max(1.0, dead_at - first[0])
    damage = [last[2][i][0] - first[2][i][0] for i in range(slot_count)]
    return {"kills": kills, "span": span, "finished": not still_dying, "damage": damage}


def describe(build, result):
    names = [spell["name"] for spell in build["spells"]]
    if not result:
        return {"key": build["key"], "label": build["label"], "missing": True}
    total = sum(result["damage"])
    shares = [100.0 * d / total if total else 0.0 for d in result["damage"]]
    top = max(range(len(shares)), key=lambda i: shares[i])
    return {
        "key": build["key"],
        "label": build["label"],
        "ids": [spell["id"] for spell in build["spells"]],
        "kills": result["kills"],
        "dead": result["span"],
        "finished": result["finished"],
        "per_second": total / result["span"],
        "shares": list(zip(names, shares)),
        "top": f"{names[top]} {shares[top]:.0f}%",
    }


def dead_text(row):
    return f"{row['dead']:.1f}" if row["finished"] else f"{WINDOW_TO - WINDOW_FROM:.0f}+"


def print_text(rows):
    for row in rows:
        if row.get("missing"):
            print(f"{row['key']:<8} no data")
            continue
        shares = ", ".join(f"{name} {share:.0f}%" for name, share in row["shares"])
        print(f"{row['key']:<8} kills {row['kills']:3d}  dead {dead_text(row):>5}s  {row['per_second'] / 1000:5.1f}k/s | {shares}")


def print_markdown(rows, date, hero):
    for row in rows:
        if row.get("missing"):
            continue
        ids = "/".join(str(i) for i in row["ids"])
        print(f"| {date} | {hero} | {row['label']} ({ids}) | {row['kills']} | {dead_text(row)} | "
              f"{row['per_second'] / 1000:.1f}k | {row['top']} |")


def main():
    parser = argparse.ArgumentParser(description="Report sandbox build benchmark results from the chat log.")
    parser.add_argument("manifest", help="manifest JSON written by run-bench.ps1")
    parser.add_argument("--log", default=str(Path(__file__).resolve().parents[2] / "Logs" / "WoWChatLog.txt"))
    parser.add_argument("--markdown", action="store_true", help="print docs table rows")
    args = parser.parse_args()

    manifest = json.loads(Path(args.manifest).read_text(encoding="utf-8-sig"))
    windows, states, bench = read_log(args.log, since_seconds(manifest["since"]))
    if not bench:
        print("WARNING: no bench mode notice in the log; talents, upgrades and gear may have counted.", file=sys.stderr)
    starts = {key: seconds for seconds, key in windows}
    rows = []
    for build in manifest["builds"]:
        start = starts.get(build["key"])
        result = measure(start, states, len(build["spells"])) if start is not None else None
        rows.append(describe(build, result))

    if args.markdown:
        print_markdown(rows, manifest["date"], manifest["hero"])
    else:
        print_text(rows)


if __name__ == "__main__":
    main()
