"""Debug phase 2: understand GameObject/Transform/MonoBehaviour linkage and UIFont/UILabel structure."""
import UnityPy, traceback

BUNDLE_PATH = r"H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\Scenes"
env = UnityPy.load(BUNDLE_PATH)

# ── Collect everything ─────────────────────────────────────────────────────────
fonts = {}
game_objects = {}   # pid -> read obj
transforms   = {}   # pid -> read obj
mono_raw     = {}   # pid -> unread obj

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
    except:
        pass

print(f"Fonts: {fonts}")
print()

# ── Inspect GameObject structure ───────────────────────────────────────────────
print("=== GameObject structure (first 2) ===")
for pid, d in list(game_objects.items())[:2]:
    print(f"\n  path_id={pid}  m_Name={d.m_Name!r}")
    print(f"  m_Component type={type(d.m_Component)}")
    comps = d.m_Component
    if comps:
        print(f"  m_Component[0] type={type(comps[0])}  repr={repr(comps[0])[:200]}")
        for i, c in enumerate(comps[:3]):
            print(f"    [{i}] type={type(c)}  dir={[x for x in dir(c) if not x.startswith('_')]}")
            try:
                # Try accessing as pptr
                print(f"    [{i}] path_id={c.path_id}  file_id={c.file_id}")
            except Exception as e:
                print(f"    [{i}] no path_id: {e}")
            try:
                print(f"    [{i}] component attr: {c.component}")
            except:
                pass
    print(f"  m_Transform type={type(d.m_Transform)}")
    try:
        print(f"  m_Transform.path_id={d.m_Transform.path_id}")
    except Exception as e:
        print(f"  m_Transform err: {e}")

# ── Inspect Transform structure ────────────────────────────────────────────────
print("\n=== Transform structure (first 2) ===")
for pid, d in list(transforms.items())[:2]:
    print(f"\n  path_id={pid}")
    print(f"  dir={[x for x in dir(d) if not x.startswith('_')]}")
    try:
        print(f"  m_GameObject.path_id={d.m_GameObject.path_id}")
    except Exception as e:
        print(f"  m_GameObject err: {e}")
    try:
        print(f"  m_Father.path_id={d.m_Father.path_id}")
    except Exception as e:
        print(f"  m_Father err: {e}")
    try:
        print(f"  m_Children count={len(d.m_Children)}")
    except Exception as e:
        print(f"  m_Children err: {e}")

# ── Inspect UIFont MonoBehaviour ───────────────────────────────────────────────
print("\n=== UIFont MonoBehaviour structure ===")
for pid, obj in mono_raw.items():
    try:
        d = obj.read()
        sname = ''
        if hasattr(d, 'm_Script') and d.m_Script:
            try:
                sc = d.m_Script.read()
                sname = getattr(sc, 'm_ClassName', '')
            except:
                pass
        if sname == 'UIFont':
            print(f"\n  path_id={pid}")
            print(f"  dir={[x for x in dir(d) if not x.startswith('_')]}")
            print(f"  m_GameObject.path_id={d.m_GameObject.path_id}")
            # Print all attributes
            for attr in dir(d):
                if attr.startswith('_') or attr in ('save', 'set_object_reader', 'assets_file', 'object_reader'):
                    continue
                try:
                    val = getattr(d, attr)
                    if callable(val):
                        continue
                    print(f"  {attr} = {repr(val)[:200]}")
                except:
                    pass
            break
    except:
        pass

# ── Inspect UILabel MonoBehaviour ──────────────────────────────────────────────
print("\n=== UILabel MonoBehaviour structure (first 2) ===")
count = 0
for pid, obj in mono_raw.items():
    try:
        d = obj.read()
        sname = ''
        if hasattr(d, 'm_Script') and d.m_Script:
            try:
                sc = d.m_Script.read()
                sname = getattr(sc, 'm_ClassName', '')
            except:
                pass
        if sname == 'UILabel':
            print(f"\n  path_id={pid}")
            print(f"  m_GameObject.path_id={d.m_GameObject.path_id}")
            for attr in dir(d):
                if attr.startswith('_') or attr in ('save', 'set_object_reader', 'assets_file', 'object_reader'):
                    continue
                try:
                    val = getattr(d, attr)
                    if callable(val):
                        continue
                    print(f"  {attr} = {repr(val)[:200]}")
                except:
                    pass
            count += 1
            if count >= 2:
                break
    except:
        pass

# ── Get all distinct script names ─────────────────────────────────────────────
print("\n=== All distinct script names ===")
script_names = set()
for pid, obj in mono_raw.items():
    try:
        d = obj.read()
        if hasattr(d, 'm_Script') and d.m_Script:
            sc = d.m_Script.read()
            sn = getattr(sc, 'm_ClassName', '')
            if sn:
                script_names.add(sn)
    except:
        pass
for sn in sorted(script_names):
    print(f"  {sn}")
