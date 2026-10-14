import re
import sys
from pathlib import Path

SUMMARY_LABELS = {
    "cases": "Cases passed",
    "highlight": "Highlighted sentence holds the fact",
    "refusal": "Refused when the documents do not say",
    "median": "Median time per answer",
}


def table_value(text: str, label: str) -> str:
    match = re.search(rf"^\|\s*{re.escape(label)}\s*\|\s*(.+?)\s*\|\s*$", text, re.MULTILINE)
    if not match:
        raise ValueError(f"missing '{label}'")
    return match.group(1)


def header_value(text: str, pattern: str) -> str:
    match = re.search(pattern, text, re.MULTILINE)
    if not match:
        raise ValueError(f"missing {pattern}")
    return match.group(1)


def row(path: Path) -> tuple[str, str, str]:
    text = path.read_text(encoding="utf-8")
    company = header_value(text, r"^# Evaluation: (.+)$")
    model = header_value(text, r"^- Chat model: `(.+)`$")
    cases = table_value(text, SUMMARY_LABELS["cases"]).split(" (")[0]
    total = cases.split(" / ")[1]
    highlight = table_value(text, SUMMARY_LABELS["highlight"]).split(" (")[0]
    refusal = table_value(text, SUMMARY_LABELS["refusal"]).split(" (")[0]
    median = table_value(text, SUMMARY_LABELS["median"])
    line = f"| {company} ({total}) | `{model}` | {cases} | {highlight} | {refusal} | {median} |"
    return company, model, line


def main(paths: list[str]) -> int:
    reports = [Path(p) for p in paths if "first-run" not in p]
    if not reports:
        print("usage: summarize.py <report.md> ...", file=sys.stderr)
        return 1

    rows = sorted(row(p) for p in reports)
    print("| Set | Chat model | Cases passed | Right sentence highlighted | Refused when it should | Median answer time |")
    print("|---|---|---|---|---|---|")
    for _, _, line in rows:
        print(line)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
