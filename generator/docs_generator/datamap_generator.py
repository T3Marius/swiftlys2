import json
import os
import re

SCRIPT_DIR = os.path.pardir
DEFAULT_DATAMAP_PATH = os.path.join(SCRIPT_DIR, "datamaps.json")


def _slugify(name):
    """Convert a class name to a safe, lowercase filename segment."""
    if not name:
        return "unknown"
    cleaned = re.sub(r"\s+", "-", name.strip())
    cleaned = re.sub(r"[^A-Za-z0-9_-]+", "-", cleaned)
    cleaned = cleaned.strip("-")
    return cleaned.lower() or "unknown"


def _escape_cell(value):
    """Escape characters that break markdown tables."""
    if value is None:
        return ""
    text = str(value)
    text = text.replace("|", "\\|").replace("`", "\\`").replace("\n", "<br>")
    return text


def _render_section(section_name, rows):
    """Render a markdown table section for a specific category."""
    lines = [f"## {section_name}", ""]
    if not rows:
        lines.append("_No Entry_")
        lines.append("")
        return "\n".join(lines)

    if section_name == "Functions":
        headers = ["Function Name"]
        rows_data = [[row.get("fieldName") or row.get("keyvaluename") or ""] for row in rows]
    elif section_name == "Input":
        headers = ["Input Name", "Field Type"]
        rows_data = [
            [row.get("keyvaluename") or "", row.get("fieldType") or ""]
            for row in rows
        ]
    elif section_name == "Output":
        headers = ["Output Name", "Field Type"]
        rows_data = [
            [row.get("keyvaluename") or "", row.get("fieldType") or ""]
            for row in rows
        ]
    else:  # Fields
        headers = ["EntityKeyValues Name", "Field Name", "Field Type"]
        rows_data = [
            [
                row.get("keyvaluename") or "",
                row.get("fieldName") or "",
                row.get("fieldType") or "",
            ]
            for row in rows
        ]

    header_line = "| " + " | ".join(headers) + " |"
    separator_line = "| " + " | ".join(["---"] * len(headers)) + " |"
    lines.append(header_line)
    lines.append(separator_line)

    for row_items in rows_data:
        cells = []
        for cell in row_items:
            cell_text = _escape_cell(cell)
            cells.append(f"`{cell_text}`" if cell_text else "—")
        lines.append("| " + " | ".join(cells) + " |")

    lines.append("")
    return "\n".join(lines)


def _group_fields(fields):
    """Group datamap fields into sections."""
    groups = {
        "Input": [],
        "Output": [],
        "Functions": [],
        "Fields": [],
    }

    for field in fields or []:
        entry = {
            "keyvaluename": field.get("externalName") or field.get("fieldName") or "",
            "fieldName": field.get("fieldName", ""),
            "fieldType": field.get("fieldType", ""),
        }

        if field.get("isFunction"):
            groups["Functions"].append(entry)
        elif field.get("isInput"):
            groups["Input"].append(entry)
        elif field.get("isOutput"):
            groups["Output"].append(entry)
        else:
            groups["Fields"].append(entry)

    return groups


def generate_datamap_docs(dest_root, datamap_path=None):
    """
    Generate MDX files for datamaps.

    Args:
        dest_root: Base docs output directory (e.g., ../../docs).
        datamap_path: Optional path to datamaps.json.

    Returns:
        Number of datamap files generated.
    """
    datamap_path = datamap_path or DEFAULT_DATAMAP_PATH
    if not os.path.exists(datamap_path):
        print(f"Warning: datamap source not found at {datamap_path}")
        return 0

    with open(datamap_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    datamaps = data.get("datamaps", [])
    output_dir = os.path.join(dest_root, "datamaps")
    os.makedirs(output_dir, exist_ok=True)

    generated = 0
    for entry in datamaps:
        class_name = entry.get("class_name") or entry.get("data_class_name") or "Unknown"
        data_class_name = entry.get("data_class_name") or ""
        base_class = entry.get("base_data_class_name") or ""
        slug = _slugify(class_name)
        groups = _group_fields(entry.get("fields", []))

        lines = [
            "---",
            f"title: {class_name}",
            "---",
            "",
            f"# {class_name}",
            "",
        ]

        if data_class_name and data_class_name != class_name:
            lines.append(f"Data class: `{_escape_cell(data_class_name)}`")
            lines.append("")

        if base_class:
            base_slug = _slugify(base_class)
            lines.append(f"Inherited from [{base_class}](/docs/api/datamaps/{base_slug})")
            lines.append("")

        for section_name in ("Fields", "Input", "Output", "Functions"):
            lines.append(_render_section(section_name, groups[section_name]))

        dest_path = os.path.join(output_dir, f"{slug}.mdx")
        with open(dest_path, "w", encoding="utf-8") as f:
            f.write("\n".join(lines).strip() + "\n")

        generated += 1

    return generated
