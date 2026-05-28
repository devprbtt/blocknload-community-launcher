"""
Block N Load -- Complete font-to-UI-component mapping extractor.
Handles NGUI (UILabel.mTrueTypeFont / UILabel.mFont → UIFont.mDynamicFont)
and Unity built-in Text components.
"""

import sys
import io
import UnityPy
from collections import defaultdict

# Force UTF-8 output so Unicode characters in GO names don't crash on Windows cp1252
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')

BUNDLE_PATH = r"H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\Scenes"

print(f"Loading: {BUNDLE_PATH}", flush=True)
env = UnityPy.load(BUNDLE_PATH)
print("Loaded.", flush=True)

# ── Collect raw objects ────────────────────────────────────────────────────────
fonts        = {}   # path_id -> name  (local bundle fonts)
game_objects = {}   # path_id -> read GameObject
transforms   = {}   # path_id -> read Transform/RectTransform
mono_raw     = {}   # path_id -> unread MonoBehaviour obj

for obj in env.objects:
    t = obj.type.name
    try:
        if t == "Font":
            d = obj.read()
            fonts[obj.path_id] = d.m_Name
        elif t == "GameObject":
            d = obj.read()
            game_objects[obj.path_id] = d
        elif t in ("Transform", "RectTransform"):
            d = obj.read()
            transforms[obj.path_id] = d
        elif t == "MonoBehaviour":
            mono_raw[obj.path_id] = obj
    except Exception:
        pass

print(f"Fonts (local): {fonts}", flush=True)
print(f"GameObjects: {len(game_objects)}  Transforms: {len(transforms)}  MonoBehaviours: {len(mono_raw)}", flush=True)

# ── Build transform ↔ GameObject maps ─────────────────────────────────────────
# m_Component is list of (classID_int, PPtr) tuples
# Class 4 = Transform, Class 224 = RectTransform

transform_to_go = {}   # transform path_id -> go path_id
go_to_transform = {}   # go path_id -> transform path_id
go_component_pids = defaultdict(list)  # go path_id -> [component path_ids]

for go_pid, go_obj in game_objects.items():
    for item in go_obj.m_Component:
        try:
            class_id, pptr = item
            comp_pid = pptr.path_id
            go_component_pids[go_pid].append(comp_pid)
            if class_id in (4, 224):   # Transform / RectTransform
                transform_to_go[comp_pid] = go_pid
                go_to_transform[go_pid] = comp_pid
        except Exception:
            pass

# Also set up via transform's m_GameObject (redundant but robust)
for t_pid, t_obj in transforms.items():
    try:
        go_pid = t_obj.m_GameObject.path_id
        transform_to_go[t_pid] = go_pid
        go_to_transform[go_pid] = t_pid
    except Exception:
        pass


def get_go_path(go_pid):
    """Walk parent chain; return full slash-separated path."""
    parts = []
    visited = set()
    cur = go_pid
    while cur and cur not in visited:
        visited.add(cur)
        go_obj = game_objects.get(cur)
        if go_obj is None:
            parts.append(f"<go:{cur}>")
            break
        parts.append(go_obj.m_Name)
        t_pid = go_to_transform.get(cur)
        if t_pid is None:
            break
        t_obj = transforms.get(t_pid)
        if t_obj is None:
            break
        try:
            father_pid = t_obj.m_Father.path_id
            if father_pid == 0:
                break
            parent_go_pid = transform_to_go.get(father_pid)
            if parent_go_pid is None:
                break
            cur = parent_go_pid
        except Exception:
            break
    parts.reverse()
    return "/".join(parts) if parts else f"<go:{go_pid}>"


def get_go_for_mono(mb_read):
    """Return GO path_id for a read MonoBehaviour."""
    try:
        return mb_read.m_GameObject.path_id
    except Exception:
        return None


def font_label(pid, file_id=0):
    """Return a human-readable font label for a (file_id, path_id) pair."""
    if file_id == 0:
        return fonts.get(pid, f"local_pid={pid}")
    else:
        # External bundle reference — look up by path_id in our fonts dict
        # (path_id values from external refs often match local ones in depot builds)
        return fonts.get(pid, f"ext_file={file_id}_pid={pid}")


# ── Read all MonoBehaviours and categorise ─────────────────────────────────────
print("Reading MonoBehaviours...", flush=True)

uilabels   = []   # list of read UILabel dicts
uifonts    = {}   # path_id -> read UIFont dict  (NGUI bitmap/truetype font objects)
unity_texts = []  # list of dicts for UnityEngine.UI.Text
mreplace   = []   # MReplaceTool entries

ALL_SCRIPT_NAMES = defaultdict(int)

for pid, obj in mono_raw.items():
    try:
        d = obj.read()
        sname = ''
        if hasattr(d, 'm_Script') and d.m_Script:
            try:
                sc = d.m_Script.read()
                sname = getattr(sc, 'm_ClassName', '') or ''
            except Exception:
                pass
        ALL_SCRIPT_NAMES[sname] += 1

        if sname == 'UILabel':
            go_pid = get_go_for_mono(d)
            # mTrueTypeFont: direct Font PPtr
            ttr = None
            try:
                ref = d.mTrueTypeFont
                if ref and ref.path_id != 0:
                    ttr = (ref.file_id, ref.path_id)
            except Exception:
                pass
            # mFont: PPtr to UIFont MonoBehaviour
            mfont_pid = None
            try:
                ref = d.mFont
                if ref and ref.path_id != 0:
                    mfont_pid = ref.path_id
            except Exception:
                pass
            uilabels.append({
                'pid': pid,
                'go_pid': go_pid,
                'ttr': ttr,          # (file_id, path_id) or None
                'mfont_pid': mfont_pid,
                'text': getattr(d, 'mText', '') or ''
            })

        elif sname == 'UIFont':
            go_pid = get_go_for_mono(d)
            dyn_font = None
            try:
                ref = d.mDynamicFont
                if ref and ref.path_id != 0:
                    dyn_font = (ref.file_id, ref.path_id)
            except Exception:
                pass
            replacement = None
            try:
                ref = d.mReplacement
                if ref and ref.path_id != 0:
                    replacement = ref.path_id
            except Exception:
                pass
            uifonts[pid] = {
                'go_pid': go_pid,
                'dyn_font': dyn_font,
                'replacement_pid': replacement
            }

        elif sname == 'Text':   # UnityEngine.UI.Text
            go_pid = get_go_for_mono(d)
            font_ref = None
            try:
                ref = d.m_Font
                if ref and ref.path_id != 0:
                    font_ref = (ref.file_id, ref.path_id)
            except Exception:
                pass
            unity_texts.append({
                'pid': pid,
                'go_pid': go_pid,
                'font_ref': font_ref,
                'text': getattr(d, 'm_Text', '') or ''
            })

        elif sname == 'MReplaceTool':
            go_pid = get_go_for_mono(d)
            mreplace.append({'pid': pid, 'go_pid': go_pid, 'd': d})

    except Exception:
        pass

print(f"UILabels: {len(uilabels)}  UIFonts: {len(uifonts)}  UnityTexts: {len(unity_texts)}", flush=True)


def resolve_uifont_font(uifont_pid):
    """Follow UIFont replacement chain, then return the actual Font (file_id, path_id)."""
    visited = set()
    cur = uifont_pid
    while cur and cur not in visited:
        visited.add(cur)
        info = uifonts.get(cur)
        if info is None:
            return None
        if info['dyn_font']:
            return info['dyn_font']
        if info['replacement_pid']:
            cur = info['replacement_pid']
        else:
            return None
    return None


# ── Build font_users mapping ───────────────────────────────────────────────────
# font_users: (file_id, path_id) -> list of (go_path, via_desc, sample_text)
font_users = defaultdict(list)

for entry in uilabels:
    go_path = get_go_path(entry['go_pid']) if entry['go_pid'] else "<unknown>"
    sample  = entry['text'][:60].replace('\n', ' ')

    if entry['ttr']:
        fid, fpid = entry['ttr']
        font_users[(fid, fpid)].append((go_path, "UILabel.mTrueTypeFont", sample))

    if entry['mfont_pid']:
        # Resolve UIFont → actual Font
        resolved = resolve_uifont_font(entry['mfont_pid'])
        if resolved:
            font_users[resolved].append((go_path, "UILabel.mFont→UIFont.mDynamicFont", sample))
        else:
            # Record the UIFont's go path for info
            uf_info = uifonts.get(entry['mfont_pid'], {})
            font_users[('uifont', entry['mfont_pid'])].append(
                (go_path, f"UILabel.mFont→UIFont(pid={entry['mfont_pid']},no_dyn_font)", sample))

for entry in unity_texts:
    if entry['font_ref']:
        go_path = get_go_path(entry['go_pid']) if entry['go_pid'] else "<unknown>"
        sample  = entry['text'][:60].replace('\n', ' ')
        font_users[entry['font_ref']].append((go_path, "UnityEngine.UI.Text.m_Font", sample))


# ── Print the full report ──────────────────────────────────────────────────────
SEP = "=" * 80

print(f"\n{SEP}")
print("FULL FONT USAGE REPORT -- Block N Load Old Depot (depot_299361)")
print(f"{SEP}\n")

# Group by font name
# Build a reverse map: font_key -> name
def font_name_for_key(key):
    fid, fpid = key
    if fid == 'uifont':
        uf = uifonts.get(fpid, {})
        uf_go = get_go_path(uf.get('go_pid')) if uf.get('go_pid') else '?'
        return f"<UIFont(pid={fpid}, go={uf_go})>"
    return fonts.get(fpid, f"ext_file={fid}_pid={fpid}")

# Sort by resolved font name
all_keys = sorted(font_users.keys(), key=lambda k: font_name_for_key(k).lower())

for key in all_keys:
    fname = font_name_for_key(key)
    fid, fpid = key
    print(f'Font "{fname}"  (file_id={fid}, path_id={fpid})')
    entries = font_users[key]
    seen = set()
    for go_path, via, sample in sorted(entries, key=lambda x: x[0]):
        dedup = (go_path, via)
        if dedup in seen:
            continue
        seen.add(dedup)
        sample_str = f"  [text: {sample!r}]" if sample else ""
        print(f"  - {go_path}  [{via}]{sample_str}")
    print()

# ── Fonts with NO usage ────────────────────────────────────────────────────────
used_local_font_pids = {fpid for (fid, fpid) in font_users if isinstance(fid, int)}
unused = {pid: name for pid, name in fonts.items() if pid not in used_local_font_pids}
if unused:
    print(f"\n--- Fonts present but NOT directly referenced by any UILabel/Text ---")
    for pid, name in sorted(unused.items(), key=lambda x: x[1]):
        print(f"  path_id={pid}  {name!r}")
    print()

# ── UIFont objects ─────────────────────────────────────────────────────────────
print(f"\n{SEP}")
print("NGUI UIFont OBJECTS (bitmap/truetype font wrappers)")
print(SEP)
for pid, info in sorted(uifonts.items()):
    go_path = get_go_path(info['go_pid']) if info['go_pid'] else "<unknown>"
    dyn = font_name_for_key(info['dyn_font']) if info['dyn_font'] else "none"
    repl = info['replacement_pid']
    print(f"  UIFont pid={pid}  go={go_path!r}")
    print(f"    mDynamicFont -> {dyn!r}  mReplacement -> {repl}")

# ── MReplaceTool ──────────────────────────────────────────────────────────────
if mreplace:
    print(f"\n{SEP}")
    print("MReplaceTool COMPONENTS")
    print(SEP)
    for entry in mreplace:
        go_path = get_go_path(entry['go_pid']) if entry['go_pid'] else "<unknown>"
        d = entry['d']
        print(f"  go={go_path!r}")
        for attr in dir(d):
            if attr.startswith('_') or attr in ('save','set_object_reader','assets_file','object_reader','m_Script','m_Enabled','m_GameObject','m_Name'):
                continue
            try:
                v = getattr(d, attr)
                if not callable(v):
                    print(f"    {attr} = {repr(v)[:300]}")
            except:
                pass

# ── Interesting GO paths for the targets ──────────────────────────────────────
TARGETS = ['edosz', 'AvenirNextLTPro-Heavy', 'AvenirNextLTPro-HeavyCn', 'MyriadPro-Bold',
           'RobotoCondensed', 'AvenirNext Condensed', 'MyriadPro-Bold-Point']

print(f"\n{SEP}")
print("TARGET FONT SUMMARY")
print(SEP)
for target in TARGETS:
    # Find the path_id
    matches = [(pid, name) for pid, name in fonts.items() if name == target]
    if not matches:
        print(f"\n  Font {target!r} — NOT FOUND in bundle fonts list")
        continue
    for pid, name in matches:
        print(f"\n  Font {name!r} (path_id={pid})")
        # Collect all usages — check both (0,pid) and (3,pid) etc.
        all_usages = []
        for (fid, fpid), entries in font_users.items():
            if fpid == pid and isinstance(fid, int):
                all_usages.extend(entries)
        if all_usages:
            seen = set()
            for go_path, via, sample in sorted(all_usages, key=lambda x: x[0]):
                if go_path in seen:
                    continue
                seen.add(go_path)
                sample_str = f"  [text: {sample!r}]" if sample else ""
                print(f"    - {go_path}{sample_str}")
        else:
            print(f"    (no UILabel/Text references found)")

# ── Specific prefabs of interest ──────────────────────────────────────────────
PREFAB_KEYWORDS = ['activityscroll', 'killfeed', 'notices', 'hud', 'playerinfo',
                   'playerstat', 'killstreak', 'damage', 'respawn', 'scoreboard',
                   'chat', 'feed', 'toast', 'popup', 'notification']

print(f"\n{SEP}")
print("HUD/NOTIFICATION/ACTIVITY-RELATED GO PATHS WITH FONT REFERENCES")
print(SEP)
all_entries_flat = []
for key, entries in font_users.items():
    fname = font_name_for_key(key)
    for go_path, via, sample in entries:
        all_entries_flat.append((go_path.lower(), go_path, fname, via, sample))

for kw in PREFAB_KEYWORDS:
    hits = [(go_path, fname, via, sample) for (lp, go_path, fname, via, sample) in all_entries_flat if kw in lp]
    if hits:
        print(f"\n  [{kw}]")
        seen = set()
        for go_path, fname, via, sample in sorted(hits):
            key = (go_path, fname)
            if key in seen:
                continue
            seen.add(key)
            sample_str = f"  text={sample!r}" if sample else ""
            print(f"    {go_path}  → font={fname!r}{sample_str}")

print("\nDone.", flush=True)
