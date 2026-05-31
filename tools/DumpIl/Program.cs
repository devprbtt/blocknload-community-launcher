using Mono.Cecil;
using Mono.Cecil.Cil;
using BnlCommunityFixes.Core.Features.Build.Patching;
using BnlCommunityFixes.Core.Services;

var asmPath    = args[0];
var helperPath = args[1];
var patchingDir= args[2];

// Apply the FOV patcher and dump the result
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(Path.GetDirectoryName(asmPath)!);
resolver.AddSearchDirectory(Path.GetDirectoryName(helperPath)!);
var rp = new ReaderParameters { AssemblyResolver = resolver, ReadWrite = false, InMemory = true };

using var target = AssemblyDefinition.ReadAssembly(asmPath, rp);
using var helper = AssemblyDefinition.ReadAssembly(helperPath, rp);
var context = new ExperimentalPatchContext(target, helper, patchingDir);

var patcher = new FovFeaturePatcher();
patcher.Apply(context);

var module = target.MainModule;
var targets = new[] { ("CameraFov", "Update"), ("CameraArms", "Update"), ("MouseLook", "RotateByMouse") };

foreach (var (typeName, methodName) in targets)
{
    var type = module.Types.FirstOrDefault(t => t.Name == typeName);
    var method = type?.Methods.FirstOrDefault(m => m.Name == methodName && m.HasBody);
    if (method is null) { Console.WriteLine($"[MISSING] {typeName}.{methodName}"); continue; }
    Console.WriteLine($"\n=== {typeName}.{methodName} ===");
    foreach (var instr in method.Body.Instructions)
    {
        var op = instr.Operand switch {
            MethodReference mr => $"call {mr.DeclaringType.Name}::{mr.Name}",
            FieldReference fr  => $"field {fr.Name}",
            float f            => $"float({f})",
            int i              => $"int({i})",
            null               => "",
            _                  => instr.Operand.ToString() ?? ""
        };
        Console.WriteLine($"  IL_{instr.Offset:X4} {instr.OpCode.Name,-12} {op}");
    }
}
