"""Debug phase 3: verify Transform attributes and MonoBehaviour fields."""
import UnityPy, traceback

BUNDLE_PATH = r"H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\Scenes"
env = UnityPy.load(BUNDLE_PATH)

transforms = {}
game_objects = {}
mono_raw = {}
fonts = {}

for obj in env.objects:
    t = obj.type.name
    try:
        if t == "Font":
            d = obj.read(); fonts[obj.path_id] = d.m_Name
        elif t == "GameObject":
            d = obj.read(); game_objects[obj.path_id] = d
        elif t in ("Transform", "RectTransform"):
            d = obj.read(); transforms[obj.path_id] = d
        elif t == "MonoBehaviour":
            mono_raw[obj.path_id] = obj
    except:
        pass

# Inspect Transform
print("=== Transform (first 1) ===")
for pid, d in list(transforms.items())[:1]:
    print(f"  path_id={pid}  dir={[x for x in dir(d) if not x.startswith('_')]}")
    for attr in dir(d):
        if attr.startswith('_') or attr in ('save','set_object_reader','assets_file','object_reader'):
            continue
        try:
            v = getattr(d, attr)
            if not callable(v):
                print(f"  {attr} = {repr(v)[:200]}")
        except Exception as e:
            print(f"  {attr} => ERROR: {e}")

# Inspect m_Component format
print("\n=== GameObject m_Component format ===")
for pid, d in list(game_objects.items())[:1]:
    print(f"  GO pid={pid} name={d.m_Name!r}")
    for i, item in enumerate(d.m_Component[:5]):
        print(f"  [{i}] type={type(item)}  repr={repr(item)[:300]}")
        if isinstance(item, tuple):
            print(f"       tuple[0]={item[0]}  tuple[1]={repr(item[1])[:200]}")
            print(f"       pptr.path_id={item[1].path_id}  pptr.file_id={item[1].file_id}")

# Inspect UILabel
print("\n=== UILabel (first 1) ===")
for pid, obj in mono_raw.items():
    try:
        d = obj.read()
        sname = ''
        if hasattr(d, 'm_Script') and d.m_Script:
            sc = d.m_Script.read()
            sname = getattr(sc, 'm_ClassName', '')
        if sname != 'UILabel':
            continue
        print(f"  path_id={pid}")
        print(f"  m_GameObject={repr(d.m_GameObject)[:200]}")
        print(f"  m_GameObject.path_id={d.m_GameObject.path_id}")
        for attr in dir(d):
            if attr.startswith('_') or attr in ('save','set_object_reader','assets_file','object_reader'):
                continue
            try:
                v = getattr(d, attr)
                if not callable(v):
                    print(f"  {attr} = {repr(v)[:200]}")
            except:
                pass
        break
    except:
        pass

# UIFont details
print("\n=== UIFont (first 1) ===")
for pid, obj in mono_raw.items():
    try:
        d = obj.read()
        sname = ''
        if hasattr(d, 'm_Script') and d.m_Script:
            sc = d.m_Script.read()
            sname = getattr(sc, 'm_ClassName', '')
        if sname != 'UIFont':
            continue
        print(f"  path_id={pid}")
        for attr in dir(d):
            if attr.startswith('_') or attr in ('save','set_object_reader','assets_file','object_reader'):
                continue
            try:
                v = getattr(d, attr)
                if not callable(v):
                    print(f"  {attr} = {repr(v)[:300]}")
            except:
                pass
        break
    except:
        pass
