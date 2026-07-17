// Dumps Block N Load catalogue (CDB) cards by loading the game's own Assembly-CSharp
// to parse the Igor binary format. Usage: CdbDump <card-id-substring> [more substrings...]
using System.IO.Compression;
using System.Reflection;

var managed = @"I:\SteamLibrary\steamapps\common\BlockNLoad\Win64\BlockNLoad_Data\Managed";
var cdbPath = @"I:\SteamLibrary\steamapps\common\BlockNLoad\Cache\cdb";
var filters = args.Length > 0 ? args : new[] { "heal_station", "unit_pickup_medikit" };

AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
{
    var name = new AssemblyName(e.Name).Name + ".dll";
    var p = Path.Combine(managed, name);
    return File.Exists(p) ? Assembly.LoadFrom(p) : null;
};

var asm = Assembly.LoadFrom(Path.Combine(managed, "Assembly-CSharp.dll"));
var cardType = asm.GetType("Protocol.Card")!;

// cdb = zlib( [1 byte service fn id] + Igor list<Card> )
using var raw = File.OpenRead(cdbPath);
using var zlib = new ZLibStream(raw, CompressionMode.Decompress);
using var ms = new MemoryStream();
zlib.CopyTo(ms);
ms.Position = 0;
using var reader = new BinaryReader(ms);
reader.ReadByte();

var readVariant = cardType.GetMethod("ReadVariant", BindingFlags.Public | BindingFlags.Static)!;
var funcType = typeof(Func<,>).MakeGenericType(typeof(BinaryReader), cardType);
var readDelegate = Delegate.CreateDelegate(funcType, readVariant);
var igorRead = asm.GetType("Igor.Read")!;
var listMethod = igorRead.GetMethods(BindingFlags.Public | BindingFlags.Static)
    .First(m => m.Name == "List" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsGenericType)
    .MakeGenericMethod(cardType);
var listFunc = (Delegate)listMethod.Invoke(null, new object[] { readDelegate })!;
var cards = (System.Collections.IEnumerable)listFunc.DynamicInvoke(reader)!;

// Index by key for cross-reference lookups. Card.Key is the CRC32 of the Id, so
// index by a freshly constructed Key(id) — stored Key values read back as 0.
var keyType = asm.GetType("Key")!;
var keyCtor = keyType.GetConstructor(new[] { typeof(string) })!;
var byKey = new Dictionary<string, object>();
var all = new List<object>();
foreach (var card in cards)
{
    all.Add(card);
    var id = Get(card, "Id") as string;
    if (id != null)
        byKey[keyCtor.Invoke(new object[] { id }).ToString()!] = card;
}
Console.WriteLine($"Loaded {all.Count} cards.");

foreach (var card in all)
{
    var id = Get(card, "Id") as string;
    if (id == null || !filters.Any(f => id.Contains(f, StringComparison.OrdinalIgnoreCase)))
        continue;

    Console.WriteLine($"\n=== {card.GetType().Name} {id} ===");
    Dump(card, 1, 0);
}

object? Get(object o, string prop) => o.GetType().GetProperty(prop)?.GetValue(o)
    ?? o.GetType().GetField(prop)?.GetValue(o);

void Dump(object o, int indent, int depth)
{
    if (depth > 6) return;
    var pad = new string(' ', indent * 2);
    var t = o.GetType();

    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (p.GetIndexParameters().Length > 0) continue;
        object? v;
        try { v = p.GetValue(o); } catch { continue; }
        if (v == null) continue;

        var vt = v.GetType();
        if (vt.IsPrimitive || v is string || vt.IsEnum || v is decimal)
        {
            if (v is bool b && !b) continue;
            Console.WriteLine($"{pad}{p.Name} = {v}");
        }
        else if (p.Name is "Key" or "GroupKey")
        {
            Console.WriteLine($"{pad}{p.Name} = {v}");
        }
        else if (v is System.Collections.IDictionary dict)
        {
            if (dict.Count == 0) continue;
            Console.WriteLine($"{pad}{p.Name}:");
            foreach (System.Collections.DictionaryEntry e in dict)
                Console.WriteLine($"{pad}  {e.Key} = {e.Value}");
        }
        else if (v is System.Collections.IEnumerable list and not string)
        {
            var items = list.Cast<object>().ToList();
            if (items.Count == 0) continue;
            Console.WriteLine($"{pad}{p.Name} ({items.Count}):");
            foreach (var item in items)
            {
                if (item == null) continue;
                var it = item.GetType();
                if (it.IsPrimitive || item is string || it.IsEnum)
                {
                    Console.WriteLine($"{pad}  {item}");
                }
                else if (it.Name == "Key")
                {
                    // Cross-reference into the catalogue
                    var refCard = byKey.TryGetValue(item.ToString()!, out var rc) ? rc : null;
                    Console.WriteLine($"{pad}  -> {item} {(refCard != null ? refCard.GetType().Name + " " + Get(refCard, "Id") : "(unresolved)")}");
                    if (refCard != null && depth < 4)
                        Dump(refCard, indent + 2, depth + 1);
                }
                else
                {
                    Console.WriteLine($"{pad}  [{it.Name}]");
                    Dump(item, indent + 2, depth + 1);
                }
            }
        }
        else if (vt.Name == "Key")
        {
            var refCard = byKey.TryGetValue(v.ToString()!, out var rc) ? rc : null;
            Console.WriteLine($"{pad}{p.Name} = {v} {(refCard != null ? "-> " + refCard.GetType().Name + " " + Get(refCard, "Id") : "")}");
            if (refCard != null && p.Name != "Key" && p.Name != "GroupKey" && depth < 3)
                Dump(refCard, indent + 1, depth + 1);
        }
        else if (vt.Namespace == null || !vt.Namespace.StartsWith("UnityEngine"))
        {
            Console.WriteLine($"{pad}{p.Name} [{vt.Name}]:");
            Dump(v, indent + 1, depth + 1);
        }
    }
}
