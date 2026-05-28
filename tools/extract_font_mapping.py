"""
Extract font-to-UI-component mapping from Block N Load's Scenes asset bundle.
"""

import sys
import json
import traceback
from collections import defaultdict

# Use the correct import name
import UnityPy

BUNDLE_PATH = r"H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\Scenes"

print(f"Loading bundle: {BUNDLE_PATH}", flush=True)
env = UnityPy.load(BUNDLE_PATH)
print("Bundle loaded. Enumerating objects...", flush=True)

# ── Storage ────────────────────────────────────────────────────────────────────
fonts           = {}   # path_id -> name
game_objects    = {}   # path_id -> name
transforms      = {}   # path_id -> raw object (read)
mono_objects    = {}   # path_id -> raw unity obj (NOT yet read)
go_components   = defaultdict(list)  # go_path_id -> [component path_ids]

# ── First pass: read lightweight objects ──────────────────────────────────────
total = 0
for obj in env.objects:
    total += 1
    t = obj.type.name
    try:
        if t == "Font":
            d = obj.read()
            fonts[obj.path_id] = getattr(d, 'name', str(obj.path_id))
        elif t == "GameObject":
            d = obj.read()
            game_objects[obj.path_id] = d
        elif t in ("Transform", "RectTransform"):
            d = obj.read()
            transforms[obj.path_id] = d
        elif t == "MonoBehaviour":
            mono_objects[obj.path_id] = obj
    except Exception as e:
        pass

print(f"Total objects: {total}", flush=True)
print(f"Fonts found: {len(fonts)}", flush=True)
print(f"GameObjects: {len(game_objects)}", flush=True)
print(f"Transforms:  {len(transforms)}", flush=True)
print(f"MonoBehaviours: {len(mono_objects)}", flush=True)
print(flush=True)

# ── Print all fonts ────────────────────────────────────────────────────────────
print("=== FONTS ===")
for pid, name in sorted(fonts.items(), key=lambda x: x[1]):
    print(f"  path_id={pid}  name={name!r}")
print(flush=True)

# ── Build transform tree ───────────────────────────────────────────────────────
# Map transform path_id -> GameObject path_id
transform_to_go = {}
go_to_transform = {}

for go_pid, go_obj in game_objects.items():
    try:
        go = go_obj  # already read
        for comp_ref in go.m_Component:
            # comp_ref may be a tuple (index, pptr) or just a pptr depending on version
            try:
                if hasattr(comp_ref, 'component'):
                    pptr = comp_ref.component
                elif isinstance(comp_ref, (list, tuple)):
                    pptr = comp_ref[1] if len(comp_ref) > 1 else comp_ref[0]
                else:
                    pptr = comp_ref
                comp_pid = pptr.path_id
                go_components[go_pid].append(comp_pid)
                if comp_pid in transforms:
                    transform_to_go[comp_pid] = go_pid
                    go_to_transform[go_pid] = comp_pid
            except Exception:
                pass
    except Exception:
        pass

# Also map via transform's m_GameObject if available
for t_pid, t_obj in transforms.items():
    try:
        go_ref = t_obj.m_GameObject
        go_pid = go_ref.path_id
        transform_to_go[t_pid] = go_pid
        go_to_transform[go_pid] = t_pid
    except Exception:
        pass

def get_go_path(go_pid):
    """Walk up the parent chain to build a full path string."""
    parts = []
    visited = set()
    current_go = go_pid
    while current_go and current_go not in visited:
        visited.add(current_go)
        go_obj = game_objects.get(current_go)
        if go_obj is None:
            parts.append(f"<go:{current_go}>")
            break
        try:
            parts.append(go_obj.name)
        except Exception:
            parts.append(f"<go:{current_go}>")
            break
        # Get transform to find parent
        t_pid = go_to_transform.get(current_go)
        if t_pid is None:
            break
        t_obj = transforms.get(t_pid)
        if t_obj is None:
            break
        try:
            parent_ref = t_obj.m_Father
            if parent_ref is None or parent_ref.path_id == 0:
                break
            parent_t_pid = parent_ref.path_id
            parent_go_pid = transform_to_go.get(parent_t_pid)
            if parent_go_pid is None:
                break
            current_go = parent_go_pid
        except Exception:
            break
    parts.reverse()
    return "/".join(parts) if parts else f"<go:{go_pid}>"


def get_go_for_component(comp_pid):
    """Given a component path_id, find the owning GameObject path_id."""
    # Direct lookup via transform mapping
    if comp_pid in transform_to_go:
        return transform_to_go[comp_pid]
    # Search via go_components
    for go_pid, comps in go_components.items():
        if comp_pid in comps:
            return go_pid
    # Try reading the monobehaviour's m_GameObject
    obj = mono_objects.get(comp_pid)
    if obj:
        try:
            d = obj.read()
            go_ref = d.m_GameObject
            if go_ref and go_ref.path_id != 0:
                return go_ref.path_id
        except Exception:
            pass
    return None


# ── Process MonoBehaviours ─────────────────────────────────────────────────────
print("=== MONOBEHAVIOUR ANALYSIS ===", flush=True)
print(f"Processing {len(mono_objects)} MonoBehaviours...", flush=True)

font_users = defaultdict(list)       # font_pid -> list of (path_string, via_desc)
stylesheet_font_data = {}            # path_id -> {font_pid, name}
ui_style_font_components = []        # list of {mb_pid, go_pid, font_data_pid}
text_components_found = []           # list of {mb_pid, go_pid, font_pid}
replace_font_components = []         # list of {mb_pid, go_pid, from_list, to_list}

interesting_scripts = {}

for pid, obj in mono_objects.items():
    try:
        d = obj.read()
        script_name = ''
        if hasattr(d, 'm_Script') and d.m_Script:
            try:
                script = d.m_Script.read()
                script_name = (getattr(script, 'm_ClassName', '') or
                               getattr(script, 'name', '') or '')
            except Exception:
                pass

        sn_lower = script_name.lower()

        if any(kw in sn_lower for kw in ['font', 'stylesheet', 'replace', 'text']):
            interesting_scripts[pid] = script_name

        # ── StylesheetFontData ──
        if 'stylesheetfontdata' in sn_lower or script_name == 'StylesheetFontData':
            font_ref = None
            try:
                font_ref = d.font
            except Exception:
                pass
            if font_ref is None:
                try:
                    dd = d.to_dict() if hasattr(d, 'to_dict') else {}
                    font_ref = dd.get('font')
                except Exception:
                    pass
            font_pid = None
            if font_ref is not None:
                try:
                    font_pid = font_ref.path_id
                except Exception:
                    font_pid = font_ref
            stylesheet_font_data[pid] = {
                'font_pid': font_pid,
                'name': getattr(d, 'name', '') or getattr(d, 'm_Name', '')
            }

        # ── UiStyleFontComponent ──
        elif 'uistylefontcomponent' in sn_lower or script_name == 'UiStyleFontComponent':
            font_data_pid = None
            try:
                fd = d.fontData
                font_data_pid = fd.path_id
            except Exception:
                try:
                    dd = d.to_dict() if hasattr(d, 'to_dict') else {}
                    fd = dd.get('fontData')
                    if fd is not None:
                        font_data_pid = getattr(fd, 'path_id', fd)
                except Exception:
                    pass
            go_pid = get_go_for_component(pid)
            ui_style_font_components.append({
                'mb_pid': pid,
                'go_pid': go_pid,
                'font_data_pid': font_data_pid
            })

        # ── UnityEngine Text component ──
        elif script_name in ('Text', '') and not sn_lower:
            # Unity built-in Text doesn't have m_Script usually — handled below
            pass

        # ── ReplaceFont ──
        elif 'replacefont' in sn_lower or script_name == 'ReplaceFont':
            from_list = []
            to_list = []
            try:
                dd = d.to_dict() if hasattr(d, 'to_dict') else {}
                raw_from = dd.get('From') or dd.get('from') or []
                raw_to   = dd.get('To')   or dd.get('to')   or []
                for item in (raw_from if isinstance(raw_from, list) else [raw_from]):
                    try:
                        from_list.append(item.path_id)
                    except Exception:
                        from_list.append(str(item))
                for item in (raw_to if isinstance(raw_to, list) else [raw_to]):
                    try:
                        to_list.append(item.path_id)
                    except Exception:
                        to_list.append(str(item))
            except Exception:
                pass
            go_pid = get_go_for_component(pid)
            replace_font_components.append({
                'mb_pid': pid,
                'go_pid': go_pid,
                'from_list': from_list,
                'to_list': to_list
            })

    except Exception as e:
        pass

# ── Also scan for Unity built-in Text (type = "TextMesh" or via obj type) ──────
# UnityEngine.UI.Text appears as MonoBehaviour sometimes, but also check if any
# objects have a "font" attribute directly
print(f"Interesting script names found: {sorted(set(interesting_scripts.values()))}", flush=True)
print(flush=True)

# ── Now look for all MonoBehaviours and do a broader scan for font refs ─────────
print("=== BROAD FONT REFERENCE SCAN ===", flush=True)
for pid, obj in mono_objects.items():
    try:
        d = obj.read()
        dd = d.to_dict() if hasattr(d, 'to_dict') else {}
        script_name = interesting_scripts.get(pid, '')

        # Look for any key called 'font' at top level
        font_val = dd.get('font') or dd.get('Font') or dd.get('m_Font')
        if font_val is not None:
            try:
                font_pid = font_val.path_id if hasattr(font_val, 'path_id') else None
                if font_pid and font_pid in fonts:
                    go_pid = get_go_for_component(pid)
                    go_path = get_go_path(go_pid) if go_pid else f"<unknown_go>"
                    text_components_found.append({
                        'mb_pid': pid,
                        'go_pid': go_pid,
                        'go_path': go_path,
                        'font_pid': font_pid,
                        'script': script_name
                    })
            except Exception:
                pass
    except Exception:
        pass

# ── Resolve UiStyleFontComponent → StylesheetFontData → Font ──────────────────
print("=== RESOLVING FONT CHAINS ===", flush=True)

for entry in ui_style_font_components:
    go_pid = entry['go_pid']
    fd_pid = entry['font_data_pid']
    go_path = get_go_path(go_pid) if go_pid else "<unknown>"

    font_pid = None
    if fd_pid and fd_pid in stylesheet_font_data:
        font_pid = stylesheet_font_data[fd_pid]['font_pid']
    elif fd_pid:
        # Try reading it directly
        obj = mono_objects.get(fd_pid)
        if obj:
            try:
                d2 = obj.read()
                font_pid = d2.font.path_id
            except Exception:
                pass

    if font_pid:
        font_users[font_pid].append((go_path, "via UiStyleFontComponent → StylesheetFontData"))
    else:
        font_users[None].append((go_path, f"UiStyleFontComponent with unresolved fontData pid={fd_pid}"))

for entry in text_components_found:
    font_pid = entry['font_pid']
    go_path  = entry['go_path']
    script   = entry['script'] or 'Text/unknown'
    font_users[font_pid].append((go_path, f"direct font ref (script={script!r})"))

# ── Print full report ──────────────────────────────────────────────────────────
print("\n" + "="*80)
print("FULL FONT USAGE REPORT")
print("="*80 + "\n")

all_font_pids = set(fonts.keys()) | set(font_users.keys())
for font_pid in sorted(all_font_pids, key=lambda x: fonts.get(x, '') if x else ''):
    fname = fonts.get(font_pid, f"<unknown font pid={font_pid}>") if font_pid else "<unresolved>"
    users = font_users.get(font_pid, [])
    print(f'Font "{fname}" (path_id={font_pid})')
    if users:
        for go_path, via in sorted(set(users)):
            print(f"  - {go_path}  [{via}]")
    else:
        print("  (no direct UI references found)")
    print()

# ── ReplaceFont components ─────────────────────────────────────────────────────
if replace_font_components:
    print("\n" + "="*80)
    print("REPLACEFONT COMPONENTS")
    print("="*80)
    for entry in replace_font_components:
        go_path = get_go_path(entry['go_pid']) if entry['go_pid'] else "<unknown>"
        from_names = [fonts.get(p, f"pid={p}") for p in entry['from_list']]
        to_names   = [fonts.get(p, f"pid={p}") for p in entry['to_list']]
        print(f"  GameObject: {go_path}")
        print(f"    From: {from_names}")
        print(f"    To:   {to_names}")
        print()

# ── StylesheetFontData dump ────────────────────────────────────────────────────
print("\n" + "="*80)
print("STYLESHEET FONT DATA OBJECTS")
print("="*80)
for pid, info in sorted(stylesheet_font_data.items()):
    font_name = fonts.get(info['font_pid'], f"<pid={info['font_pid']}>")
    print(f"  path_id={pid}  name={info['name']!r}  font={font_name!r} (pid={info['font_pid']})")

# ── Interesting-script dump for debugging ──────────────────────────────────────
print("\n" + "="*80)
print("ALL INTERESTING MONOBEHAVIOUR SCRIPTS (font/stylesheet/replace/text)")
print("="*80)
for pid, sname in sorted(interesting_scripts.items(), key=lambda x: x[1]):
    go_pid = get_go_for_component(pid)
    go_path = get_go_path(go_pid) if go_pid else "<unknown>"
    print(f"  path_id={pid}  script={sname!r}  go={go_path!r}")

print("\nDone.", flush=True)
