"""Debug: inspect actual UnityPy object structure for fonts and GameObjects."""
import UnityPy, json, traceback

BUNDLE_PATH = r"H:\Programas\Steam\steamapps\content\app_299360\depot_299361\assetbundles\Scenes"
env = UnityPy.load(BUNDLE_PATH)

# Check the serialized file structure
print("=== SerializedFile info ===")
for sf in env.files.values():
    print(f"  name={sf.name}")
    print(f"  unity_version={getattr(sf, 'unity_version', '?')}")
    break

# Inspect a few Font objects
print("\n=== Font objects (first 3) ===")
count = 0
for obj in env.objects:
    if obj.type.name == "Font":
        print(f"\n  path_id={obj.path_id}")
        try:
            d = obj.read()
            print(f"  type(d) = {type(d)}")
            print(f"  dir(d) = {[x for x in dir(d) if not x.startswith('__')]}")
            try:
                dd = d.to_dict()
                print(f"  to_dict keys = {list(dd.keys())}")
                for k, v in list(dd.items())[:5]:
                    print(f"    {k}: {repr(v)[:120]}")
            except Exception as e:
                print(f"  to_dict failed: {e}")
            print(f"  d.name={getattr(d, 'name', 'N/A')}")
            print(f"  d.m_Name={getattr(d, 'm_Name', 'N/A')}")
        except Exception as e:
            print(f"  READ ERROR: {e}")
            traceback.print_exc()
        count += 1
        if count >= 3:
            break

# Inspect a few GameObject objects
print("\n=== GameObject objects (first 3) ===")
count = 0
for obj in env.objects:
    if obj.type.name == "GameObject":
        print(f"\n  path_id={obj.path_id}")
        try:
            d = obj.read()
            print(f"  type(d) = {type(d)}")
            print(f"  dir = {[x for x in dir(d) if not x.startswith('__')]}")
            try:
                dd = d.to_dict()
                print(f"  to_dict keys = {list(dd.keys())}")
                for k, v in list(dd.items())[:8]:
                    print(f"    {k}: {repr(v)[:200]}")
            except Exception as e:
                print(f"  to_dict failed: {e}")
        except Exception as e:
            print(f"  READ ERROR: {e}")
            traceback.print_exc()
        count += 1
        if count >= 3:
            break

# Inspect Transform / RectTransform
print("\n=== RectTransform objects (first 2) ===")
count = 0
for obj in env.objects:
    if obj.type.name in ("Transform", "RectTransform"):
        print(f"\n  path_id={obj.path_id} type={obj.type.name}")
        try:
            d = obj.read()
            dd = d.to_dict()
            print(f"  to_dict keys = {list(dd.keys())}")
            for k, v in list(dd.items())[:10]:
                print(f"    {k}: {repr(v)[:200]}")
        except Exception as e:
            print(f"  READ ERROR: {e}")
            traceback.print_exc()
        count += 1
        if count >= 2:
            break

# Inspect MonoBehaviours with UIFont script
print("\n=== UIFont MonoBehaviours ===")
for obj in env.objects:
    if obj.type.name == "MonoBehaviour":
        try:
            d = obj.read()
            script_name = ''
            if hasattr(d, 'm_Script') and d.m_Script:
                try:
                    sc = d.m_Script.read()
                    script_name = getattr(sc, 'm_ClassName', '') or getattr(sc, 'name', '')
                except:
                    pass
            if script_name == 'UIFont':
                print(f"\n  path_id={obj.path_id}")
                dd = d.to_dict() if hasattr(d, 'to_dict') else {}
                print(f"  keys = {list(dd.keys())}")
                for k, v in list(dd.items())[:15]:
                    print(f"    {k}: {repr(v)[:200]}")
        except:
            pass

# Sample a random MonoBehaviour to understand m_Script structure
print("\n=== MonoBehaviour sample (first 5 with script) ===")
count = 0
for obj in env.objects:
    if obj.type.name == "MonoBehaviour":
        try:
            d = obj.read()
            sname = ''
            if hasattr(d, 'm_Script') and d.m_Script:
                try:
                    sc = d.m_Script.read()
                    sname = getattr(sc, 'm_ClassName', '') or getattr(sc, 'name', '')
                    ns = getattr(sc, 'm_Namespace', '')
                    asm = getattr(sc, 'm_AssemblyName', '')
                    if sname:
                        print(f"  path_id={obj.path_id}  script={sname!r}  ns={ns!r}  asm={asm!r}")
                        count += 1
                except:
                    pass
        except:
            pass
        if count >= 20:
            break
