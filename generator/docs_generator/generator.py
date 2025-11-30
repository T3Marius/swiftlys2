import os
import re
import yaml

SOURCE_DIR = "../../api"
DEST_DIR = "../../docs"

os.makedirs(DEST_DIR, exist_ok=True)

def get_namespace(yaml_data):
    """Extract namespace from the YAML data."""
    for item in yaml_data.get('body', []):
        if 'facts' in item:
            for fact in item['facts']:
                if fact.get('name') == 'Namespace':
                    if isinstance(fact.get('value'), dict):
                        return fact['value'].get('text', '')
                    return fact.get('value', '')
    return ''

def extract_metadata(yaml_data, is_index=False):
    """Extract front-matter metadata from DocFX ApiPage YAML."""
    title = yaml_data.get('title', '')
    if not title:
        return {}

    if is_index:
        # Only keep the last part after the last dot
        clean_title = title.split(".")[-1]
    else:
        # Default behavior: last word
        words = title.split()
        clean_title = words[-1] if words else ''

    clean_title = re.sub(r'<[^>]+>', '', clean_title)
    clean_title = re.sub(r'\[[^\]]+\]', '', clean_title)

    return {'title': clean_title}

def convert_to_path(s):
    if s.lower().endswith(".html"):
        s = s[10:]
        s = s[:-5]
    # Merge Core and Shared into the same path
    s = s.replace("SwiftlyS2.Core.", "SwiftlyS2.").replace("SwiftlyS2.Shared.", "SwiftlyS2.")
    path = "/".join(s.split(".")).lower()
    # Transform -NUMBER to t repeated NUMBER times
    parts = path.split("/")
    parts[-1] = transform_filename(parts[-1])
    path = "/".join(parts)
    return path

def transform_filename(base_name):
    """
    If filename ends with -NUMBER, replace with NUMBER times 't'.
    Example: class-3 -> classttt
    """
    match = re.match(r"^(.*?)-(\d+)$", base_name)
    if match:
        name, num = match.groups()
        num = int(num)
        return name + ("t" * num)
    return base_name

def generate_markdown(yaml_data):
    """Generate Markdown content from DocFX ApiPage YAML."""
    md = ""
    namespace = get_namespace(yaml_data)
    
    for item in yaml_data.get('body', []):
        if 'api1' in item:
            api1_title = str(item.get('api1', ''))
            api1_title = re.sub(r'<[^>]+>', '', api1_title)
            md += f"# {api1_title}\n\n"
            if 'src' in item:
                src = item['src'].replace('/blob/main', '/blob/master')
                md += f"[View Source]({src})\n\n"
        if 'facts' in item:
            for fact in item['facts']:
                fact_name = fact.get('name', '')
                fact_value = fact.get('value', '')
                if isinstance(fact_value, dict):
                    fact_text = fact_value.get('text', '')
                    fact_url = fact_value.get('url', '')
                    if fact_url and fact_url.endswith('.html'):
                        fact_url = "/docs/api/" + convert_to_path(fact_url)
                        md += f"**{fact_name}**: [{fact_text}]({fact_url})\n\n"
                    else:
                        md += f"**{fact_name}**: {fact_text}\n\n"
                else:
                    md += f"**{fact_name}**: {fact_value}\n\n"
        if 'markdown' in item:
            md += f"{item['markdown']}\n\n"
        if 'h2' in item:
            md += f"## {item['h2']}\n\n"
        if 'h4' in item:
            md += f"#### {item['h4']}\n\n"
        if 'code' in item:
            md += "```csharp\n" + item['code'] + "\n```\n\n"
        if 'inheritance' in item:
            for inherit in item['inheritance']:
                inherit_text = inherit.get('text', '')
                inherit_url = inherit.get('url', '')
                if inherit_url:
                    if inherit_url.endswith('.html'):
                        inherit_url = "/docs/api/" + convert_to_path(inherit_url)
                    md += f"- [{inherit_text}]({inherit_url})\n"
                else:
                    md += f"- {inherit_text}\n"
            md += "\n"
        if 'list' in item:
            for list_item in item['list']:
                list_text = list_item.get('text', '')
                list_url = list_item.get('url', '')
                if list_url:
                    if list_url.endswith('.html'):
                        list_url = "/docs/api/" + convert_to_path(list_url)
                    md += f"- [{list_text}]({list_url})\n"
                else:
                    md += f"- {list_text}\n"
            md += "\n"
        if 'parameters' in item:
            for param in item['parameters']:
                param_name = param.get('name', '')
                param_default = param.get('default', '')
                param_type = ''
                param_description = param.get('description', '')
                parts = []
                if 'type' in param:
                    for t in param['type']:
                        if isinstance(t, dict):
                            url = t.get('url', '')
                            if url.endswith('.html'):
                                url = "/docs/api/" + convert_to_path(url)
                            parts.append(f"[{t['text']}]({url})")
                        else:
                            if str(t) == "text":
                                parts.append(f"[{param['type']['text']}]")
                            elif str(t) == "url":
                                url = param['type']['url']
                                if url.endswith('.html'):
                                    url = "/docs/api/" + convert_to_path(url)
                                parts.append(f"({url})")
                            else:
                                parts.append(str(t))

                if param_default != '':
                    parts.append(f"{param_default}")
                if param_description != '':
                    parts.append(f" - {param_description}")
                param_type = ''.join(parts)
                md += f"- **{param_name}**: {param_type}\n" if param_name else f"- {param_type}\n"
            md += "\n"
        if 'api3' in item:
            src = item.get('src', '')
            api3_title = str(item.get('api3', ''))
            api3_title = re.sub(r'<[^>]+>', '', api3_title)
            api3_title = re.sub(r'\[[^\]]+\]', '', api3_title)
            md += f"### {api3_title}\n\n"
            if src != '':
                src = src.replace('/blob/main', '/blob/master')
                md += f"[View Source]({src})\n\n"
    return md

def convert_yaml_file(src_path, dest_path):
    with open(src_path, 'r', encoding='utf-8') as f:
        yaml_data = yaml.safe_load(f)

    if isinstance(yaml_data, list):
        yaml_data = yaml_data[0]

    # Detect if it's an index.md (folder-style)
    folder_path, file_name = os.path.split(dest_path)
    base_name, ext = os.path.splitext(file_name)
    is_index = (
        os.path.isdir(dest_path) or os.path.exists(os.path.join(folder_path, base_name))
    )

    metadata = extract_metadata(yaml_data, is_index=is_index)
    if metadata == {}:
        return
    
    md_content = "---\n"
    md_content += yaml.safe_dump(metadata, sort_keys=False)
    md_content += "---\n\n"
    md_content += generate_markdown(yaml_data)

    base_name = transform_filename(base_name)
    final_path = os.path.join(folder_path, base_name + ".md")

    if is_index:
        final_folder = os.path.join(folder_path, base_name)
        os.makedirs(final_folder, exist_ok=True)
        final_path = os.path.join(final_folder, "index.md")
    else:
        os.makedirs(folder_path, exist_ok=True)

    with open(final_path, 'w', encoding='utf-8') as f:
        f.write(md_content)

for root, dirs, files in os.walk(SOURCE_DIR):
    for file in files:
        if file.endswith(".yml") or file.endswith(".yaml"):
            raw_base = os.path.splitext(file)[0]
            # Merge Core and Shared paths
            raw_base = raw_base.replace("SwiftlyS2.Core.", "SwiftlyS2.").replace("SwiftlyS2.Shared.", "SwiftlyS2.")
            new_base = transform_filename(raw_base)
            dest_file = os.path.join(DEST_DIR, convert_to_path(new_base) + ".md")
            convert_yaml_file(os.path.join(root, file), dest_file)

print("Markdown generation complete!")
