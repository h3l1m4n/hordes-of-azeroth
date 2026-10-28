import json
import statistics
import sys
from pathlib import Path

HERE = Path(__file__).parent
SPECS = [line.split(' ')[:4] for line in (HERE / 'reference.txt').read_text().splitlines() if line.strip()]
COLUMNS = [
    ('Naked', 'dummies-s1naked', 'base'),
    ('M1 base', 'waves-ref-t2-s1-{tree}', 'base'),
    ('M1', 'waves-ref-t2-s1-{tree}', 'evolved'),
    ('M1 no cap', 'waves-ref-t2-s1-nocap-{tree}', 'evolved'),
    ('M1 final', 'waves-final-m1-{tree}', 'evolved'),
    ('Floor 10', 'waves-floor10-{tree}', 'evolved'),
    ('Floor 20', 'waves-floor20-{tree}', 'evolved'),
    ('Dummies M1', 'dummies-dref-{tree}', 'evolved'),
    ('Dummies final', 'dummies-dfinal-{tree}', 'evolved'),
    ('Dummies M1 cards', 'dummies-dcards-{tree}', 'evolved'),
]
NAKED_OVERRIDES = ['dummies-s1hunterfix']


def lookup(results, mode, cls, key):
    return results.get(mode, {}).get(cls, {}).get(key, {}).get('per_second')


def cell_value(results, mode, cls, key):
    if mode == 'dummies-s1naked':
        for override in NAKED_OVERRIDES:
            value = lookup(results, override, cls, key)
            if value:
                return value
    return lookup(results, mode, cls, key)


def collect(results):
    table = {}
    for cls, tree, base, evolved in SPECS:
        table[(cls, tree, base)] = [cell_value(results, mode.format(tree=tree), cls, base if which == 'base' else evolved)
            for _, mode, which in COLUMNS]
    return table


def main():
    results = json.loads((HERE / 'results.json').read_text())
    table = collect(results)
    medians = []
    for index in range(len(COLUMNS)):
        values = [row[index] for row in table.values() if row[index]]
        medians.append(statistics.median(values) if values else None)
    markdown = '--markdown' in sys.argv
    header = ['Spec'] + [name for name, _, _ in COLUMNS]
    if markdown:
        print('| ' + ' | '.join(header) + ' |')
        print('|' + '---|' * len(header))
    else:
        print(f"{'Spec':18}" + ''.join(f'{name:>16}' for name in header[1:]))
    for (cls, tree, base), row in table.items():
        cells = []
        for value, median in zip(row, medians):
            if not value:
                cells.append('-')
                continue
            ratio = value / median
            flag = ' !' if ratio > 1.3 or ratio < 0.7 else ''
            cells.append(f'{value / 1000:.1f}k ({ratio:.2f}){flag}')
        name = f'{cls} {base}'
        if markdown:
            print('| ' + ' | '.join([name] + cells) + ' |')
        else:
            print(f'{name:18}' + ''.join(f'{cell:>16}' for cell in cells))
    median_cells = [f'{m / 1000:.1f}k' if m else '-' for m in medians]
    if markdown:
        print('| median | ' + ' | '.join(median_cells) + ' |')
    else:
        print(f"{'median':18}" + ''.join(f'{cell:>16}' for cell in median_cells))


main()
