// Decompiles selected types (or a whole assembly) to .cs files, for reading the game's code.
// Usage: Decomp <assembly.dll> <outDir> [typeFullName ...]
//        Decomp <assembly.dll> <outDir> --all
using System;
using System.IO;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

var asmPath = args[0];
var outDir = args[1];
Directory.CreateDirectory(outDir);

var file = new PEFile(asmPath);
var resolver = new UniversalAssemblyResolver(asmPath, false, file.DetectTargetFrameworkId());
resolver.AddSearchDirectory(Path.GetDirectoryName(asmPath));
var decompiler = new CSharpDecompiler(file, resolver, new DecompilerSettings { ThrowOnAssemblyResolveErrors = false });

var names = args.Skip(2).ToList();
var types = names.Count == 1 && names[0] == "--all"
    ? decompiler.TypeSystem.MainModule.TypeDefinitions.Where(t => t.DeclaringType == null && !t.Name.StartsWith("<")).Select(t => t.FullName).ToList()
    : names;

int ok = 0;
foreach (var name in types)
{
    try
    {
        var code = decompiler.DecompileTypeAsString(new FullTypeName(name));
        File.WriteAllText(Path.Combine(outDir, name.Replace('`', '_') + ".cs"), code);
        ok++;
    }
    catch (Exception e)
    {
        Console.Error.WriteLine($"{name}: {e.Message}");
    }
}
Console.WriteLine($"Decompiled {ok}/{types.Count} types to {outDir}");
