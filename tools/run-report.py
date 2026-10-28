import argparse
import collections
import datetime
import re
import statistics

DEFAULT_LOG = r"G:\WoW Vanilla\server\logs\LevelUp.log"
NEW_RUN_GAP_SECONDS = 300

HEAD = re.compile(r"^(\S+ \S+) \[LevelUp\] \(acc (\d+),.*?name ([^,]+),.*?\) Bonk (\w+): (.*)$")
WHERE = r"\(stage (\d+), (\w+), (\w+)(?:, class (\d+))?\)"
MINUTE = re.compile(r"at run minute (\d+) " + WHERE + r": level (\d+), (\d+) kills \(\+(\d+)\), took (\d+) damage \((\d+)% of max health\), "
                    r"healed (\d+) \((\d+)%\), lowest health (\d+)%, (\d+) enemies alive, block \"([^\"]*)\"")
BOSS = re.compile(r"kills (.+?) \(([\w -]+)\) at run minute ([\d.]+) " + WHERE + r", level (\d+): ([\d.]+) s after it arrived; "
                  r"took (\d+) damage meanwhile \((\d+)% of max health\), health now (\d+)%")
DEATH = re.compile(r"at run minute ([\d.]+) " + WHERE + r", level (\d+), (\d+) kills: last hit by (.+?) for (\d+); "
                   r"(\d+) damage in the last 10 s from (\d+) hits \((\d+)% of max health\); (\d+) enemies alive, block \"([^\"]*)\"")
LEVEL = re.compile(r"reaches level (\d+) at run minute ([\d.]+) " + WHERE)
CLASSES = {1: "Warrior", 2: "Paladin", 3: "Hunter", 4: "Rogue", 5: "Priest", 7: "Shaman", 8: "Mage", 9: "Warlock", 11: "Druid"}


class Run:
    def __init__(self, name, account, started):
        self.name = name
        self.account = account
        self.started = started
        self.last = started
        self.last_minute = -1.0
        self.tier = ""
        self.class_id = 0
        self.zones = collections.OrderedDict()
        self.minutes = []
        self.bosses = []
        self.death = None
        self.top_level = 0
        self.real_time_minutes = 0
        self.last_minute_line = None

    def label(self):
        cls = CLASSES.get(self.class_id, f"class {self.class_id}")
        return f"{self.name} ({cls}), {self.tier}, started {self.started:%Y-%m-%d %H:%M}"


def parse(path, since, name_filter):
    runs = []
    open_runs = {}
    with open(path, encoding="utf-8", errors="ignore") as handle:
        for line in handle:
            head = HEAD.match(line.rstrip("\n"))
            if not head:
                continue
            when = datetime.datetime.strptime(head[1], "%Y-%m-%d %H:%M:%S")
            if when < since:
                continue
            account, name, kind, body = head[2], head[3], head[4], head[5]
            if name_filter and name.lower() != name_filter.lower():
                continue
            minute = run_minute(kind, body)
            if minute is None:
                continue
            run = open_runs.get(name)
            if run is None or (when - run.last).total_seconds() > NEW_RUN_GAP_SECONDS or minute + 1.0 < run.last_minute or run.death:
                run = Run(name, account, when)
                open_runs[name] = run
                runs.append(run)
            run.last = when
            run.last_minute = max(run.last_minute, minute)
            record(run, kind, body, when)
    return runs


def run_minute(kind, body):
    found = re.search(r"at run minute ([\d.]+)", body)
    return float(found[1]) if found and kind in ("minute", "boss", "death", "level", "pick") else None


def note_where(run, stage, zone, tier, class_id):
    run.tier = tier
    run.zones.setdefault(int(stage), zone)
    if class_id:
        run.class_id = int(class_id)


def record(run, kind, body, when):
    if kind == "minute":
        m = MINUTE.search(body)
        if not m:
            return
        note_where(run, m[2], m[3], m[4], m[5])
        if run.last_minute_line and 40 <= (when - run.last_minute_line).total_seconds() <= 90:
            run.real_time_minutes += 1
        run.last_minute_line = when
        run.top_level = max(run.top_level, int(m[6]))
        gold = re.search(r"gold earned (\d+)", body)
        run.minutes.append(dict(minute=int(m[1]), stage=int(m[2]), level=int(m[6]), kills=int(m[7]), kpm=int(m[8]), taken=int(m[10]),
                                healed=int(m[12]), low=int(m[13]), alive=int(m[14]), block=m[15], gold=int(gold[1]) if gold else None))
    elif kind == "boss":
        m = BOSS.search(body)
        if not m:
            return
        note_where(run, m[4], m[5], m[6], m[7])
        run.bosses.append(dict(name=m[1], kind=m[2], minute=float(m[3]), stage=int(m[4]), level=int(m[8]), seconds=float(m[9]),
                               taken=int(m[11]), health=int(m[12])))
    elif kind == "death":
        m = DEATH.search(body)
        if not m:
            return
        note_where(run, m[2], m[3], m[4], m[5])
        run.death = dict(minute=float(m[1]), stage=int(m[2]), level=int(m[6]), kills=int(m[7]), by=m[8], hit=int(m[9]),
                         recent=int(m[10]), hits=int(m[11]), recent_pct=int(m[12]), alive=int(m[13]), block=m[14])
    elif kind == "level":
        m = LEVEL.search(body)
        if m:
            note_where(run, m[3], m[4], m[5], m[6])
            run.top_level = max(run.top_level, int(m[1]))


def print_run(run):
    real = "real time" if run.real_time_minutes >= max(1, len(run.minutes) // 2) else "fast-forwarded or probe"
    zones = ", ".join(f"{stage}: {zone}" for stage, zone in run.zones.items())
    print(f"\n== {run.label()} [{real}]")
    print(f"   floors {zones}")
    if run.death:
        d = run.death
        print(f"   DIED floor {d['stage']} at {d['minute']:.2f} min, level {d['level']}, {d['kills']} kills: {d['by']} hit for {d['hit']}; "
              f"{d['recent_pct']}% of max health in the last 10 s from {d['hits']} hits; {d['alive']} alive, block \"{d['block']}\"")
    else:
        print(f"   ended without a death line (went home, victory or still running) at minute {run.last_minute:.2f}, level {run.top_level}")
    if run.minutes:
        print("   min floor  lvl  kills/min  alive  taken%  healed%  low%     gold  block")
        for m in run.minutes:
            gold = f"{m['gold'] / 10000:8.1f}g" if m["gold"] is not None else "        -"
            print(f"   {m['minute']:3d} {m['stage']:5d} {m['level']:4d} {m['kpm']:9d} {m['alive']:6d} {m['taken']:7d} {m['healed']:8d} {m['low']:5d} {gold}  {m['block']}")
        print_floor_gold(run)
    for b in run.bosses:
        print(f"   {b['kind']:<9} floor {b['stage']:2d} at {b['minute']:6.2f} min: {b['name']} died {b['seconds']:.1f} s after arriving, "
              f"took {b['taken']}% of max health meanwhile, health now {b['health']}%")


def print_floor_gold(run):
    last_by_floor = {}
    for m in run.minutes:
        if m["gold"] is not None:
            last_by_floor[m["stage"]] = m["gold"]
    if not last_by_floor:
        return
    previous = 0
    parts = []
    for stage in sorted(last_by_floor):
        parts.append(f"floor {stage} {(last_by_floor[stage] - previous) / 10000:.1f}g")
        previous = last_by_floor[stage]
    print("   gold by floor (kills and clears, before end-of-run salvage): " + ", ".join(parts))


def print_summary(runs):
    by_key = collections.defaultdict(list)
    for run in runs:
        for m in run.minutes:
            by_key[(run.tier, m["stage"], m["minute"] - 10 * (m["stage"] - 1))].append(m)
    if not by_key:
        return
    print("\n== Median per floor minute across these runs")
    print("   tier      floor min   n  kills/min  alive  taken%  low%")
    for (tier, stage, minute), rows in sorted(by_key.items()):
        if minute < 0:
            continue
        print(f"   {tier:<9} {stage:5d} {minute:3d} {len(rows):3d} {statistics.median(r['kpm'] for r in rows):9.0f} "
              f"{statistics.median(r['alive'] for r in rows):6.0f} {statistics.median(r['taken'] for r in rows):7.0f} "
              f"{statistics.median(r['low'] for r in rows):5.0f}")


def main():
    parser = argparse.ArgumentParser(description="Per-run tables from the Bonk lines in LevelUp.log.")
    parser.add_argument("--since", help="YYYY-MM-DD HH:MM (default: today 00:00)")
    parser.add_argument("--name", help="only this character")
    parser.add_argument("--log", default=DEFAULT_LOG)
    parser.add_argument("--all", action="store_true", help="include fast-forwarded and probe runs")
    args = parser.parse_args()
    since = datetime.datetime.strptime(args.since, "%Y-%m-%d %H:%M") if args.since else datetime.datetime.combine(datetime.date.today(), datetime.time())
    runs = [run for run in parse(args.log, since, args.name) if run.minutes or run.bosses or run.death]
    if not args.all:
        runs = [run for run in runs if run.real_time_minutes >= max(1, len(run.minutes) // 2)]
    for run in runs:
        print_run(run)
    print_summary(runs)
    if not runs:
        print("No runs found. Try --since or --all.")


if __name__ == "__main__":
    main()
