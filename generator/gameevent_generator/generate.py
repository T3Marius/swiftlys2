# Task:

# Write a code generator that generate event definitions to csharp files

# Parse all gameevents from ./gameevents folder
# including names, field names, field types, event comment, field comment

# CSharp templates: a example can be found in managed/src/SwiftlyS2.Core/Modules/GameEvents/EventTest.cs and EventTestImpl.cs
# Keep the inheritance like the example
# Write comment to fields in interface definition
# Call setter/getter that are already defined in base class GameEvent.cs on impl class for fields
# write the generated files to managed/src/SwiftlyS2.Generated/GameEvents/Classes and managed/src/SwiftlyS2.Generated/GameEvents/Classes/Interfaces

# other generator example can be found in generator/schema_generator/generator.py


import wordninja
import tqdm
import urllib.request

import os
import re
from pathlib import Path
from typing import Dict, List, Tuple

ROOT = Path(__file__).parent
INPUT_DIR = ROOT / "gameevents"
OUT_INTERFACES = ROOT.parent.parent / "managed" / "src" / "SwiftlyS2.Generated" / "GameEvents" / "Interfaces"
OUT_CLASSES = ROOT.parent.parent / "managed" / "src" / "SwiftlyS2.Generated" / "GameEvents" / "Classes"

HASHES = []

# Ensure output directories
os.makedirs(OUT_INTERFACES, exist_ok=True)
os.makedirs(OUT_CLASSES, exist_ok=True)

# Simple KeyValues-like parser for .gameevents files
_event_name_re = re.compile(r'^\s*"([^"]+)"')
_field_re = re.compile(r'^\s*"([^"]+)"\s+"([^"]+)"(?:\s*//\s*(.*))?\s*$')
_comment_inline_re = re.compile(r'^\s*"[^"]+"\s*(//\s*(.*))?$')

# Types mapping to GameEvent base methods
# value: (csharp_type, base_accessor_key, supports_setter, cast_kind)
# base_accessor_key is the suffix of GetX/SetX methods, e.g. "String" or "Int"
# cast_kind: None|"byte"|"short" used to cast int to narrower types
TYPE_MAP: Dict[str, Tuple[str, str, bool, str]] = {
  "string": ("string", "String", True, None),
  "bool": ("bool", "Bool", True, None),
  "byte": ("byte", "Int", True, "byte"),
  "short": ("short", "Int", True, "short"),
  # Valve keyvalues says long is 32-bit signed
  "long": ("int", "Int", True, None),
  "int": ("int", "Int", True, None),
  "float": ("float", "Float", True, None),
  "uint64": ("ulong", "Uint64", True, None),
  # gameplay specific
  "player_controller": ("int", "PlayerSlot", True, None),
  "player_controller_and_pawn": ("int", "PlayerSlot", True, None),
  # For player_pawn we only have getters in base: GetPawnEntityIndex (int)
  "player_pawn": ("int", "PawnEntityIndex", False, None),
  # handle types
  "ehandle": ("nint", "Ptr", True, None),
}

SKIP_TYPES = set(["none", "local"])  # "local" fields are not networked/for internal lists; skip

# Known tokens to help split concatenated lowercase field names into PascalCase
KNOWN_TOKENS = {
  # ids and basics
  "id", "user", "player", "controller", "pawn", "team", "entity", "index", "slot", "site", "map", "name",
  # common metrics
  "count", "time", "tick", "reason", "message", "distance", "health", "armor", "damage", "group", "score",
  # booleans
  "bot", "silenced", "silencer", "kit", "has", "is", "in", "on", "off", "no",
  # gameplay terms
  "weapon", "item", "hint", "icon", "offset", "range", "flags", "binding", "gamepad", "color", "activator",
  "steam", "xuid", "steamid", "assist", "assisted", "flash", "head", "shot", "headshot", "replay",
  "noscope", "scope", "thru", "smoke", "attacker", "blind", "penetrated", "wipe", "dominated", "revenge",
  # coords
  "pos", "ang", "start", "end", "x", "y", "z",
}

# Convert event or field names to PascalCase identifiers

def to_pascal_case(name: str) -> str:
  # remove non-alphanum except underscore
  name = re.sub(r'[^0-9a-zA-Z_]', '_', name)
  parts = [p for p in name.split('_') if p]
  if not parts:
    return "Unnamed"
  converted = ''.join(p[:1].upper() + p[1:] for p in parts)
  # identifiers cannot start with digit
  if converted[0].isdigit():
    converted = "E" + converted
  return converted


def _split_concatenated_lowercase(word: str):
  s = word.lower()
  if s == "assister":
    return ["Assister"]
  tokens = wordninja.split(s)
  return tokens

# Property name conversion: enforce PascalCase; split concatenated lowercase sequences using KNOWN_TOKENS

def to_property_name(field: str) -> str:
  # Special case to get usual casing
  if field.lower() == 'userid':
    return 'UserId'
  # underscores handled by to_pascal_case already
  if '_' in field:
    return to_pascal_case(field)
  # if the field is all lowercase letters/digits, attempt token split
  if re.fullmatch(r'[a-z0-9]+', field):
    tokens = _split_concatenated_lowercase(field)
    # title-case with some acronyms
    def to_title(tok: str) -> str:
      if tok in ("id", "ui", "ip", "x", "y", "z"):
        return tok.upper()
      return tok[:1].upper() + tok[1:]
    return ''.join(to_title(t) for t in tokens)
  return to_pascal_case(field)

class EventField:
  def __init__(self, name: str, type_name: str, comment: str | None):
    self.name = name
    self.type_name = type_name
    self.comment = comment or ""

class GameEventDef:
  def __init__(self, name: str, section_comment: str | None = None):
    self.name = name
    self.fields: Dict[str, EventField] = {}
    self.comment = section_comment or ""

  def add_field(self, field: EventField):
    # Merge: keep first, extend comment if needed
    if field.name in self.fields:
      existing = self.fields[field.name]
      if (not existing.comment) and field.comment:
        existing.comment = field.comment
      # prefer more specific type if existing is missing mapping
      if existing.type_name not in TYPE_MAP and field.type_name in TYPE_MAP:
        existing.type_name = field.type_name
    else:
      self.fields[field.name] = field


def _extract_inline_comment(line: str) -> str:
  idx = line.find('//')
  if idx != -1:
    return line[idx+2:].strip()
  return ""

# FNV-1a 32-bit hash for event names

def fnv1a32(text: str) -> int:
  h = 2166136261
  for b in text.encode('utf-8'):
    h ^= b
    h = (h * 16777619) & 0xFFFFFFFF
  return h

def download_gameevents():
  print("Downloading gameevents files...")
  url_map = {
    "core.gameevents": "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/core/pak01_dir/resource/core.gameevents",
    "game.gameevents": "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/pak01_dir/resource/game.gameevents",
    "mod.gameevents":  "https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/pak01_dir/resource/mod.gameevents",
  }
  os.makedirs(INPUT_DIR, exist_ok=True)
  for filename, url in url_map.items():
    dst = INPUT_DIR / filename
    try:
      with urllib.request.urlopen(url, timeout=20) as resp:
        content = resp.read()
      with open(dst, 'wb') as f:
        f.write(content)
    except Exception as e:
      print(f"WARNING: Failed to download {url}: {e}. Using existing local file if present.")


def parse_gameevents_file(path: Path) -> Dict[str, GameEventDef]:
  events: Dict[str, GameEventDef] = {}

  with open(path, 'r', encoding='utf-8') as f:
    lines = f.readlines()

  i = 0
  while i < len(lines):
    line_raw = lines[i]
    line = line_raw.strip()
    i += 1
    if not line:
      continue
    if line.startswith('//'):
      continue

    # Section or event name
    m = _event_name_re.match(line)
    if m:
      name = m.group(1)
      header_comment = _extract_inline_comment(line_raw)
      # Look ahead for '{' or '{}' on same/next line
      # Check if {} is on the same line
      if '{}' in line_raw:
        # Empty event definition on same line like: "event_name" {}
        ev = events.get(name) or GameEventDef(name)
        if header_comment and not ev.comment:
          ev.comment = header_comment
        events[name] = ev
        continue
      
      # Next non-empty, non-comment line should be '{'
      j = i
      while j < len(lines) and (not lines[j].strip() or lines[j].strip().startswith('//')):
        j += 1
      
      # Check if next line is just '{}'
      if j < len(lines) and lines[j].strip() == '{}':
        ev = events.get(name) or GameEventDef(name)
        if header_comment and not ev.comment:
          ev.comment = header_comment
        events[name] = ev
        i = j + 1
        continue
      
      if j < len(lines) and lines[j].strip().startswith('{'):
        # Parse nested until matching '}'
        i = j + 1
        depth = 1
        while i < len(lines) and depth > 0:
          raw = lines[i]
          s = raw.strip()
          i += 1
          if not s or s.startswith('//'):
            continue
          if s.startswith('}'):
            depth -= 1
            continue
          if s.startswith('{'):
            depth += 1
            continue
          mm = _event_name_re.match(s)
          if mm:
            ev_name = mm.group(1)
            ev_header_comment = _extract_inline_comment(raw)
            
            # Check if {} is on the same line
            if '{}' in raw:
              ev = events.get(ev_name) or GameEventDef(ev_name)
              if ev_header_comment and not ev.comment:
                ev.comment = ev_header_comment
              events[ev_name] = ev
              continue
            
            # expect '{' next
            k = i
            while k < len(lines) and (not lines[k].strip() or lines[k].strip().startswith('//')):
              k += 1
            
            # Check if next line is just '{}'
            if k < len(lines) and lines[k].strip() == '{}':
              ev = events.get(ev_name) or GameEventDef(ev_name)
              if ev_header_comment and not ev.comment:
                ev.comment = ev_header_comment
              events[ev_name] = ev
              i = k + 1
              continue
            
            if k < len(lines) and lines[k].strip().startswith('{'):
              i = k + 1
              ev = events.get(ev_name) or GameEventDef(ev_name)
              if ev_header_comment and not ev.comment:
                ev.comment = ev_header_comment
              depth_inner = 1
              while i < len(lines) and depth_inner > 0:
                rawf = lines[i]
                sf = rawf.strip()
                i += 1
                if not sf or sf.startswith('//'):
                  continue
                if sf.startswith('}'):
                  depth_inner -= 1
                  continue
                if sf.startswith('{'):
                  depth_inner += 1
                  continue
                fm = _field_re.match(sf)
                if fm:
                  field_name = fm.group(1)
                  field_type = fm.group(2).lower()
                  field_comment = fm.group(3) or ""
                  if field_type in SKIP_TYPES:
                    continue
                  if field_type in ['1', '0']:
                    continue
                  if field_type == 'uint64_t':
                    field_type = 'uint64'
                  if field_type == 'ehandle_t':
                    field_type = 'ehandle'
                  ev.add_field(EventField(field_name, field_type, field_comment))
              events[ev_name] = ev
            else:
              continue
        continue
      else:
        continue
  return events


def render_header_comment(event: GameEventDef) -> List[str]:
  lines: List[str] = []
  header = f'/// <summary> \n/// Event "{event.name}"'
  if event.comment:
    header += f'\n/// {event.comment.strip()}'
  header += '\n/// </summary>'
  lines.append(header)
  return lines


def render_interface(event: GameEventDef) -> str:
  original_name = event.name
  base_type_name = to_pascal_case(original_name)
  type_name = f"Event{base_type_name}"
  usings = [
    "using SwiftlyS2.Shared.SchemaDefinitions;",
    "using SwiftlyS2.Shared.GameEvents;",
    "using SwiftlyS2.Core.GameEventDefinitions;",
    "using SwiftlyS2.Shared.Players;",
  ]
  lines: List[str] = []
  lines.extend(usings)
  lines.append("")
  lines.append("namespace SwiftlyS2.Shared.GameEventDefinitions;")
  lines.append("")
  lines.extend(render_header_comment(event))
  lines.append(f"public interface {type_name} : IGameEvent<{type_name}> {{")
  lines.append("")
  # static abstract implementations required by IGameEvent<T>
  lines.append(f"  static {type_name} IGameEvent<{type_name}>.Create(nint address) => new {type_name}Impl(address);")
  lines.append("")
  lines.append(f"  static string IGameEvent<{type_name}>.GetName() => \"{original_name}\";")
  lines.append("")
  hash_value = fnv1a32(original_name)
  if hash_value in HASHES:
    print("WARNING: Hash collision detected for event: ", original_name)
  HASHES.append(hash_value)
  lines.append(f"  static uint IGameEvent<{type_name}>.GetHash() => 0x{hash_value:08X}u;")
  # fields as properties
  used_prop_names: Dict[str, int] = {}
  for fname, fdef in event.fields.items():
    ftype = fdef.type_name
    # userid override: three properties
    if fname.lower() == 'userid':
      base_prop = to_property_name(fname)  # UserId
      # Controller (readonly)
      prop_name_ctrl = f"{base_prop}Controller"
      if prop_name_ctrl in used_prop_names:
        used_prop_names[prop_name_ctrl] += 1
        prop_name_ctrl = f"{prop_name_ctrl}{used_prop_names[prop_name_ctrl]}"
      else:
        used_prop_names[prop_name_ctrl] = 1
      comment_lines: List[str] = ["  /// <summary>"]
      if fdef.comment:
        comment_lines.append(f"  /// {fdef.comment.strip()}")
      comment_lines.append(f"  /// <br/>")
      comment_lines.append(f"  /// type: {ftype}")
      comment_lines.append("  /// </summary>")
      lines.extend(comment_lines)
      lines.append(f"  CCSPlayerController {prop_name_ctrl} {{ get; }}")
      lines.append("")
      # Pawn (readonly)
      prop_name_pawn = f"{base_prop}Pawn"
      if prop_name_pawn in used_prop_names:
        used_prop_names[prop_name_pawn] += 1
        prop_name_pawn = f"{prop_name_pawn}{used_prop_names[prop_name_pawn]}"
      else:
        used_prop_names[prop_name_pawn] = 1
      comment_lines = ["  /// <summary>"]
      if fdef.comment:
        comment_lines.append(f"  /// {fdef.comment.strip()}")
      comment_lines.append(f"  /// <br/>")
      comment_lines.append(f"  /// type: {ftype}")
      comment_lines.append("  /// </summary>")
      lines.extend(comment_lines)
      lines.append(f"  CCSPlayerPawn {prop_name_pawn} {{ get; }}")
      lines.append("")

      # Player
      prop_name_player = f"{base_prop}Player"
      if prop_name_player in used_prop_names:
        used_prop_names[prop_name_player] += 1
        prop_name_player = f"{prop_name_player}{used_prop_names[prop_name_player]}"
      else:
        used_prop_names[prop_name_player] = 1
      getter_player = f'Accessor.GetPlayer("{fname}")'
      lines.append("")
      if fdef.comment:
        lines.append(f"  // {fdef.comment.strip()}")
      lines.append(f"  public IPlayer {prop_name_player}")
      lines.append(f"  {{ get => {getter_player}; }}")

      # Raw int (read/write)
      prop_name_raw = base_prop
      if prop_name_raw in used_prop_names:
        used_prop_names[prop_name_raw] += 1
        prop_name_raw = f"{prop_name_raw}{used_prop_names[prop_name_raw]}"
      else:
        used_prop_names[prop_name_raw] = 1
      comment_lines = ["  /// <summary>"]
      if fdef.comment:
        comment_lines.append(f"  /// {fdef.comment.strip()}")
      comment_lines.append(f"  /// <br/>")
      comment_lines.append(f"  /// type: {ftype}")
      comment_lines.append("  /// </summary>")
      lines.extend(comment_lines)
      lines.append(f"  int {prop_name_raw} {{ get; set; }}")
      lines.append("")
      continue

    if ftype not in TYPE_MAP:
      continue
    cs_type, accessor, can_set, cast_kind = TYPE_MAP[ftype]
    prop_name = to_property_name(fname)
    if prop_name in used_prop_names:
      used_prop_names[prop_name] += 1
      prop_name = f"{prop_name}{used_prop_names[prop_name]}"
    else:
      used_prop_names[prop_name] = 1

    comment_lines: List[str] = ["  /// <summary>"]
    if fdef.comment:
      comment_lines.append(f"  /// {fdef.comment.strip()}")
      comment_lines.append(f"  /// <br/>")
    comment_lines.append(f"  /// type: {ftype}")
    comment_lines.append("  /// </summary>")
    lines.extend(comment_lines)
    if can_set:
      lines.append(f"  {cs_type} {prop_name} {{ get; set; }}")
    else:
      lines.append(f"  {cs_type} {prop_name} {{ get; }}")
    lines.append("")

  lines.append("}")
  return "\n".join(lines)


def render_class(event: GameEventDef) -> str:
  original_name = event.name
  base_type_name = to_pascal_case(original_name)
  type_name = f"Event{base_type_name}"
  lines: List[str] = []
  lines.append("using SwiftlyS2.Core.GameEvents;")
  lines.append("using SwiftlyS2.Shared.GameEvents;")
  lines.append("using SwiftlyS2.Shared.SchemaDefinitions;")
  lines.append("using SwiftlyS2.Shared.GameEventDefinitions;")
  lines.append("using SwiftlyS2.Shared.Players;")
  lines.append("")
  lines.append("namespace SwiftlyS2.Core.GameEventDefinitions;")
  lines.append("")
  lines.append("// generated")
  lines.extend(render_header_comment(event))
  lines.append(f"internal class {type_name}Impl : GameEvent<{type_name}>, {type_name}")
  lines.append("{")
  lines.append("")
  lines.append(f"  public {type_name}Impl(nint address) : base(address)")
  lines.append("  {")
  lines.append("  }")

  used_prop_names: Dict[str, int] = {}
  for fname, fdef in event.fields.items():
    ftype = fdef.type_name

    if ftype == 'player_controller' or ftype == 'player_controller_and_pawn' or fname.lower() == 'userid':
      base_prop = to_property_name(fname)  # UserId
      # Controller
      prop_name_ctrl = f"{base_prop}Controller"
      if prop_name_ctrl in used_prop_names:
        used_prop_names[prop_name_ctrl] += 1
        prop_name_ctrl = f"{prop_name_ctrl}{used_prop_names[prop_name_ctrl]}"
      else:
        used_prop_names[prop_name_ctrl] = 1
      getter_ctrl = f'Accessor.GetPlayerController("{fname}")'
      lines.append("")
      if fdef.comment:
        lines.append(f"  // {fdef.comment.strip()}")
      lines.append(f"  public CCSPlayerController {prop_name_ctrl}")
      lines.append(f"  {{ get => {getter_ctrl}; }}")
      # Pawn
      prop_name_pawn = f"{base_prop}Pawn"
      if prop_name_pawn in used_prop_names:
        used_prop_names[prop_name_pawn] += 1
        prop_name_pawn = f"{prop_name_pawn}{used_prop_names[prop_name_pawn]}"
      else:
        used_prop_names[prop_name_pawn] = 1
      getter_pawn = f'Accessor.GetPlayerPawn("{fname}")'
      lines.append("")
      if fdef.comment:
        lines.append(f"  // {fdef.comment.strip()}")
      lines.append(f"  public CCSPlayerPawn {prop_name_pawn}")
      lines.append(f"  {{ get => {getter_pawn}; }}")

      # Player
      prop_name_player = f"{base_prop}Player"
      if prop_name_player in used_prop_names:
        used_prop_names[prop_name_player] += 1
        prop_name_player = f"{prop_name_player}{used_prop_names[prop_name_player]}"
      else:
        used_prop_names[prop_name_player] = 1
      getter_player = f'Accessor.GetPlayer("{fname}")'
      lines.append("")
      if fdef.comment:
        lines.append(f"  // {fdef.comment.strip()}")
      lines.append(f"  public IPlayer {prop_name_player}")
      lines.append(f"  {{ get => {getter_player}; }}")

      # Raw int
      prop_name_raw = base_prop
      if prop_name_raw in used_prop_names:
        used_prop_names[prop_name_raw] += 1
        prop_name_raw = f"{prop_name_raw}{used_prop_names[prop_name_raw]}"
      else:
        used_prop_names[prop_name_raw] = 1
      getter_raw = f'Accessor.GetInt32("{fname}")'
      setter_raw = f'Accessor.SetInt32("{fname}", value)'
      lines.append("")
      if fdef.comment:
        lines.append(f"  // {fdef.comment.strip()}")
      lines.append(f"  public int {prop_name_raw}")
      lines.append(f"  {{ get => {getter_raw}; set => {setter_raw}; }}")
      continue

    if ftype not in TYPE_MAP:
      continue
    cs_type, accessor, can_set, cast_kind = TYPE_MAP[ftype]
    prop_name = to_property_name(fname)
    if prop_name in used_prop_names:
      used_prop_names[prop_name] += 1
      prop_name = f"{prop_name}{used_prop_names[prop_name]}"
    else:
      used_prop_names[prop_name] = 1

    key_literal = fname

    if accessor == "String":
      getter = f'Accessor.GetString("{key_literal}")'
      setter = f'Accessor.SetString("{key_literal}", value)'
    elif accessor == "Bool":
      getter = f'Accessor.GetBool("{key_literal}")'
      setter = f'Accessor.SetBool("{key_literal}", value)'
    elif accessor == "Int":
      if cast_kind == "byte":
        getter = f'(byte)Accessor.GetInt32("{key_literal}")'
        setter = f'Accessor.SetInt32("{key_literal}", value)'
      elif cast_kind == "short":
        getter = f'(short)Accessor.GetInt32("{key_literal}")'
        setter = f'Accessor.SetInt32("{key_literal}", value)'
      else:
        getter = f'Accessor.GetInt32("{key_literal}")'
        setter = f'Accessor.SetInt32("{key_literal}", value)'
    elif accessor == "Uint64":
      getter = f'Accessor.GetUInt64("{key_literal}")'
      setter = f'Accessor.SetUInt64("{key_literal}", value)'
    elif accessor == "Float":
      getter = f'Accessor.GetFloat("{key_literal}")'
      setter = f'Accessor.SetFloat("{key_literal}", value)'
    elif accessor == "PlayerSlot":
      getter = f'Accessor.GetPlayerSlot("{key_literal}")'
      setter = f'Accessor.SetPlayerSlot("{key_literal}", value)'
    elif accessor == "PawnEntityIndex":
      getter = f'Accessor.GetPawnEntityIndex("{key_literal}")'
      setter = None
    elif accessor == "Ptr":
      getter = f'Accessor.GetPtr("{key_literal}")'
      setter = f'Accessor.SetPtr("{key_literal}", value)'
    else:
      continue

    lines.append("")
    if fdef.comment:
      lines.append(f"  // {fdef.comment.strip()}")
    lines.append(f"  public {cs_type} {prop_name}")
    if can_set and setter is not None:
      lines.append(f"  {{ get => {getter}; set => {setter}; }}")
    else:
      lines.append(f"  {{ get => {getter}; }}")

  lines.append("}")
  return "\n".join(lines)


def main():
  # Parse all files and merge events (later files can extend earlier ones)
  all_events: Dict[str, GameEventDef] = {}
  # Always refresh local .gameevents from upstream
  download_gameevents()
  file_order = [
    INPUT_DIR / "core.gameevents",
    INPUT_DIR / "game.gameevents",
    INPUT_DIR / "mod.gameevents",
  ]
  for p in tqdm.tqdm(file_order, desc="Parsing gameevents files..."):
    if not p.exists():
      continue
    parsed = parse_gameevents_file(p)
    for name, ev in parsed.items():
      if name not in all_events:
        all_events[name] = ev
      else:
        for fname, fdef in ev.fields.items():
          all_events[name].add_field(fdef)

  for ev in tqdm.tqdm(all_events.values(), desc="Generating gameevents files..."):
    iface_code = render_interface(ev)
    class_code = render_class(ev)

    base_type_name = to_pascal_case(ev.name)
    type_name = f"Event{base_type_name}"
    with open(OUT_INTERFACES / f"{type_name}.cs", 'w', encoding='utf-8', newline='\n') as f:
      f.write(iface_code + "\n")
    with open(OUT_CLASSES / f"{type_name}Impl.cs", 'w', encoding='utf-8', newline='\n') as f:
      f.write(class_code + "\n")

if __name__ == '__main__':
  main()
