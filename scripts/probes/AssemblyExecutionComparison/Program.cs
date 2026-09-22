using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 3) throw new ArgumentException("reference.dll candidate.dll output.json");
var reference = Read(args[0]);
var candidate = Read(args[1]);
var keys = reference.Values.Keys.Union(candidate.Values.Keys).Order().ToArray();
var changes = keys.Where(k => reference.Values.GetValueOrDefault(k) != candidate.Values.GetValueOrDefault(k))
    .Select(k => new { key = k, reference = reference.Values.GetValueOrDefault(k), candidate = candidate.Values.GetValueOrDefault(k) }).ToArray();
var output = new
{
    generatedUtc = DateTimeOffset.UtcNow,
    purpose = "Compare actual CLR executable methods and metadata inputs; no inference of throughput equality.",
    reference = reference.Summary,
    candidate = candidate.Summary,
    checkedRecords = keys.Length,
    executableDifferences = changes.Where(x => !x.key.StartsWith("identity/") && !x.key.StartsWith("debug/") && !x.key.StartsWith("informational/")).ToArray(),
    identityDebugInformationalDifferences = changes.Where(x => x.key.StartsWith("identity/") || x.key.StartsWith("debug/") || x.key.StartsWith("informational/")).ToArray(),
    tableCounts = reference.TableCounts,
    matchingMethods = keys.Count(k => k.StartsWith("method/") && !changes.Any(x => x.key == k)),
    matchingMethodBodies = keys.Count(k => k.StartsWith("body/") && !changes.Any(x => x.key == k)),
    matchingFieldInitializers = keys.Count(k => k.StartsWith("fieldData/") && !changes.Any(x => x.key == k)),
    matchingUserStrings = keys.Count(k => k.StartsWith("userString/") && !changes.Any(x => x.key == k)),
    matchingTypes = keys.Count(k => k.StartsWith("type/") && !changes.Any(x => x.key == k)),
    matchingFields = keys.Count(k => k.StartsWith("field/") && !changes.Any(x => x.key == k)),
    limitations = new[]
    {
        "Executable metadata equality is not a performance measurement; assembly image and JIT code addresses can still differ.",
        "Portable PDB and Source Link content are not used for CLR execution and are not compared; PE debug-directory identity and data are reported.",
        "AssemblyInformationalVersionAttribute value and module MVID are explicitly classified as identity/informational differences; all other custom attributes are compared.",
        "User-string offsets are compared because raw IL tokens depend on heap offsets; signatures and metadata token identities are compared by row.",
        "Field initializers are sized from their CLI signatures and explicit type layouts; an unsupported size causes this helper to fail rather than omit comparison."
    }
};
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2], JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { output = Path.GetFullPath(args[2]), output.checkedRecords, output.matchingMethods, output.matchingMethodBodies, output.matchingFieldInitializers, executableDifferenceCount = output.executableDifferences.Length, informationalDifferenceCount = output.identityDebugInformationalDifferences.Length, output.executableDifferences, output.identityDebugInformationalDifferences }, new JsonSerializerOptions { WriteIndented = true }));

static Snapshot Read(string path)
{
    var bytes = File.ReadAllBytes(path);
    using var stream = new MemoryStream(bytes, false);
    using var pe = new PEReader(stream);
    var r = pe.GetMetadataReader();
    var values = new SortedDictionary<string,string>(StringComparer.Ordinal);
    void Add(string key, object value) => values.Add(key, JsonSerializer.Serialize(value));
    string S(StringHandle h) => r.GetString(h);
    string B(BlobHandle h) => Convert.ToHexString(r.GetBlobBytes(h));
    static int T(EntityHandle h) => MetadataTokens.GetToken(h);
    static int[] Ts<THandle>(IEnumerable<THandle> handles) where THandle:struct => handles.Select(h => MetadataTokens.GetToken((EntityHandle)(dynamic)h)).ToArray();
    static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data));
    string AttributeName(EntityHandle constructor)
    {
        EntityHandle type = constructor.Kind switch
        {
            HandleKind.MemberReference => r.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => r.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => throw new InvalidOperationException("Unknown custom attribute constructor")
        };
        return type.Kind switch
        {
            HandleKind.TypeReference => S(r.GetTypeReference((TypeReferenceHandle)type).Namespace) + "." + S(r.GetTypeReference((TypeReferenceHandle)type).Name),
            HandleKind.TypeDefinition => S(r.GetTypeDefinition((TypeDefinitionHandle)type).Namespace) + "." + S(r.GetTypeDefinition((TypeDefinitionHandle)type).Name),
            _ => type.Kind.ToString()
        };
    }
    var tableCounts = Enum.GetValues<TableIndex>().ToDictionary(t => t.ToString(), t => r.GetTableRowCount(t));
    foreach (var pair in tableCounts) Add("table/" + pair.Key, pair.Value);
    var module = r.GetModuleDefinition();
    Add("module", new { name = S(module.Name), module.Generation, generationId = r.GetGuid(module.GenerationId), baseGenerationId = r.GetGuid(module.BaseGenerationId) });
    Add("identity/mvid", r.GetGuid(module.Mvid));
    var assembly = r.GetAssemblyDefinition();
    Add("assembly", new { name = S(assembly.Name), culture = S(assembly.Culture), assembly.Version, assembly.Flags, assembly.HashAlgorithm, publicKey = B(assembly.PublicKey) });
    Add("clr", new { pe.PEHeaders.CorHeader!.MajorRuntimeVersion, pe.PEHeaders.CorHeader.MinorRuntimeVersion, pe.PEHeaders.CorHeader.Flags, pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress, nativeHeaderSize = pe.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size, metadataVersion = r.MetadataVersion, kind = r.MetadataKind });
    Add("image", new { pe.PEHeaders.CoffHeader.Machine, pe.PEHeaders.CoffHeader.Characteristics, pe.PEHeaders.PEHeader!.Magic, pe.PEHeaders.PEHeader.ImageBase, pe.PEHeaders.PEHeader.SectionAlignment, pe.PEHeaders.PEHeader.FileAlignment, pe.PEHeaders.PEHeader.DllCharacteristics, pe.PEHeaders.PEHeader.Subsystem });
    Add("identity/peTimestamp", pe.PEHeaders.CoffHeader.TimeDateStamp);
    int debugId = 0;
    foreach (var d in pe.ReadDebugDirectory())
        Add("debug/" + debugId++, new { d.Type, d.Stamp, d.MajorVersion, d.MinorVersion, d.IsPortableCodeView, data = Convert.ToHexString(bytes.AsSpan(d.DataPointer, d.DataSize)) });

    foreach (var h in r.AssemblyReferences)
    {
        var x = r.GetAssemblyReference(h);
        Add("assemblyRef/" + T(h), new { name = S(x.Name), culture = S(x.Culture), x.Version, x.Flags, key = B(x.PublicKeyOrToken), hash = B(x.HashValue) });
    }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.ModuleRef); i++)
    {
        var h = MetadataTokens.ModuleReferenceHandle(i);
        Add("moduleRef/" + T(h), S(r.GetModuleReference(h).Name));
    }
    foreach (var h in r.TypeReferences)
    {
        var x = r.GetTypeReference(h);
        Add("typeRef/" + T(h), new { name = S(x.Name), ns = S(x.Namespace), scope = T(x.ResolutionScope) });
    }
    foreach (var h in r.TypeDefinitions)
    {
        var x = r.GetTypeDefinition(h);
        var l = x.GetLayout();
        Add("type/" + T(h), new { name = S(x.Name), ns = S(x.Namespace), x.Attributes, baseType = T(x.BaseType), declaringType = T(x.GetDeclaringType()), fields = Ts(x.GetFields()), methods = Ts(x.GetMethods()), interfaces = Ts(x.GetInterfaceImplementations()), nested = Ts(x.GetNestedTypes()), properties = Ts(x.GetProperties()), events = Ts(x.GetEvents()), methodImplementations = Ts(x.GetMethodImplementations()), genericParameters = Ts(x.GetGenericParameters()), packing = l.PackingSize, size = l.Size, l.IsDefault });
    }
    foreach (var h in r.MethodDefinitions)
    {
        var x = r.GetMethodDefinition(h);
        var import = x.GetImport();
        Add("method/" + T(h), new { name = S(x.Name), x.Attributes, x.ImplAttributes, signature = B(x.Signature), owner = T(x.GetDeclaringType()), parameters = Ts(x.GetParameters()), genericParameters = Ts(x.GetGenericParameters()), importName = S(import.Name), importModule = T(import.Module), importAttributes = import.Attributes, x.RelativeVirtualAddress, hasBody = x.RelativeVirtualAddress != 0 });
        if (x.RelativeVirtualAddress == 0) continue;
        var body = pe.GetMethodBody(x.RelativeVirtualAddress);
        Add("body/" + T(h), new { body.Size, body.MaxStack, body.LocalVariablesInitialized, localSignature = T(body.LocalSignature), rawBodyIncludingHeader = Convert.ToHexString(pe.GetSectionData(x.RelativeVirtualAddress).GetContent(0, body.Size).AsSpan()), il = Convert.ToHexString(body.GetILBytes()!), regions = body.ExceptionRegions.Select(e => new { e.Kind, e.TryOffset, e.TryLength, e.HandlerOffset, e.HandlerLength, e.FilterOffset, catchType = T(e.CatchType) }).ToArray() });
    }
    foreach (var h in r.FieldDefinitions)
    {
        var x = r.GetFieldDefinition(h);
        int rva = x.GetRelativeVirtualAddress();
        Add("field/" + T(h), new { name = S(x.Name), x.Attributes, signature = B(x.Signature), offset = x.GetOffset(), owner = T(x.GetDeclaringType()), defaultValue = T(x.GetDefaultValue()), marshal = B(x.GetMarshallingDescriptor()), hasRva = rva != 0 });
        if (rva != 0)
        {
            var sig = r.GetBlobReader(x.Signature);
            if (sig.ReadByte() != 0x06) throw new InvalidOperationException("Not field signature");
            int size = ReadSize(ref sig);
            Add("fieldData/" + T(h), new { size, data = Convert.ToHexString(pe.GetSectionData(rva).GetContent(0, size).AsSpan()) });
        }
    }
    int ReadSize(ref BlobReader sig)
    {
        byte code = sig.ReadByte();
        return code switch
        {
            0x02 or 0x04 or 0x05 => 1,
            0x03 or 0x06 or 0x07 => 2,
            0x08 or 0x09 or 0x0c => 4,
            0x0a or 0x0b or 0x0d => 8,
            0x11 => TypeSize(sig.ReadTypeHandle()),
            _ => throw new InvalidOperationException($"Unknown field-data size signature 0x{code:X2}")
        };
    }
    int TypeSize(EntityHandle h)
    {
        if (h.Kind != HandleKind.TypeDefinition) throw new InvalidOperationException("External field-data layout");
        int size = r.GetTypeDefinition((TypeDefinitionHandle)h).GetLayout().Size;
        if (size <= 0) throw new InvalidOperationException("Missing field-data type size");
        return size;
    }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.Param); i++)
    {
        var h = MetadataTokens.ParameterHandle(i); var x = r.GetParameter(h);
        Add("parameter/" + T(h), new { name = S(x.Name), x.Attributes, x.SequenceNumber, constant = T(x.GetDefaultValue()), marshal = B(x.GetMarshallingDescriptor()) });
    }
    foreach (var h in r.MemberReferences)
    {
        var x = r.GetMemberReference(h);
        Add("memberRef/" + T(h), new { name = S(x.Name), parent = T(x.Parent), signature = B(x.Signature), kind = x.GetKind() });
    }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.TypeSpec); i++)
    { var h = MetadataTokens.TypeSpecificationHandle(i); Add("typeSpec/" + T(h), B(r.GetTypeSpecification(h).Signature)); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.MethodSpec); i++)
    { var h = MetadataTokens.MethodSpecificationHandle(i); var x = r.GetMethodSpecification(h); Add("methodSpec/" + T(h), new { method = T(x.Method), signature = B(x.Signature) }); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.StandAloneSig); i++)
    { var h = MetadataTokens.StandaloneSignatureHandle(i); Add("standaloneSignature/" + T(h), B(r.GetStandaloneSignature(h).Signature)); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.InterfaceImpl); i++)
    { var h = MetadataTokens.InterfaceImplementationHandle(i); Add("interfaceImpl/" + T(h), T(r.GetInterfaceImplementation(h).Interface)); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.MethodImpl); i++)
    { var h = MetadataTokens.MethodImplementationHandle(i); var x = r.GetMethodImplementation(h); Add("methodImpl/" + T(h), new { type = T(x.Type), body = T(x.MethodBody), declaration = T(x.MethodDeclaration) }); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.Constant); i++)
    { var h = MetadataTokens.ConstantHandle(i); var x = r.GetConstant(h); Add("constant/" + T(h), new { x.TypeCode, parent = T(x.Parent), value = B(x.Value) }); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.GenericParam); i++)
    { var h = MetadataTokens.GenericParameterHandle(i); var x = r.GetGenericParameter(h); Add("genericParameter/" + T(h), new { name = S(x.Name), x.Attributes, x.Index, parent = T(x.Parent), constraints = Ts(x.GetConstraints()) }); }
    for (int i = 1; i <= r.GetTableRowCount(TableIndex.GenericParamConstraint); i++)
    { var h = MetadataTokens.GenericParameterConstraintHandle(i); var x = r.GetGenericParameterConstraint(h); Add("genericConstraint/" + T(h), new { parameter = T(x.Parameter), type = T(x.Type) }); }
    foreach (var h in r.PropertyDefinitions)
    {
        var x = r.GetPropertyDefinition(h); var a = x.GetAccessors();
        Add("property/" + T(h), new { name = S(x.Name), x.Attributes, signature = B(x.Signature), constant = T(x.GetDefaultValue()), getter = T(a.Getter), setter = T(a.Setter), others = Ts(a.Others) });
    }
    foreach (var h in r.EventDefinitions)
    {
        var x = r.GetEventDefinition(h); var a = x.GetAccessors();
        Add("event/" + T(h), new { name = S(x.Name), x.Attributes, type = T(x.Type), adder = T(a.Adder), remover = T(a.Remover), raiser = T(a.Raiser), others = Ts(a.Others) });
    }
    foreach (var h in r.CustomAttributes)
    {
        var x = r.GetCustomAttribute(h); var name = AttributeName(x.Constructor);
        bool informational = x.Parent.Kind == HandleKind.AssemblyDefinition && name == "System.Reflection.AssemblyInformationalVersionAttribute";
        Add((informational ? "informational/" : "attribute/") + T(h), new { parent = T(x.Parent), constructor = T(x.Constructor), name, value = B(x.Value) });
    }
    foreach (var h in r.DeclarativeSecurityAttributes)
    { var x = r.GetDeclarativeSecurityAttribute(h); Add("security/" + T(h), new { x.Action, parent = T(x.Parent), permissionSet = B(x.PermissionSet) }); }
    foreach (var h in r.ExportedTypes)
    { var x = r.GetExportedType(h); Add("exportedType/" + T(h), new { name = S(x.Name), ns = S(x.Namespace), x.Attributes, implementation = T(x.Implementation), x.IsForwarder }); }
    foreach (var h in r.AssemblyFiles)
    { var x = r.GetAssemblyFile(h); Add("assemblyFile/" + T(h), new { name = S(x.Name), hash = B(x.HashValue), x.ContainsMetadata }); }
    foreach (var h in r.ManifestResources)
    {
        var x = r.GetManifestResource(h);
        Add("resource/" + T(h), new { name = S(x.Name), x.Attributes, x.Offset, implementation = T(x.Implementation) });
    }
    if (pe.PEHeaders.CorHeader.ResourcesDirectory.Size > 0)
        Add("embeddedResources", Convert.ToHexString(pe.GetSectionData(pe.PEHeaders.CorHeader.ResourcesDirectory.RelativeVirtualAddress).GetContent(0, pe.PEHeaders.CorHeader.ResourcesDirectory.Size).AsSpan()));
    var userString = MetadataTokens.UserStringHandle(0);
    while (!(userString = r.GetNextHandle(userString)).IsNil)
        Add("userString/" + MetadataTokens.GetHeapOffset(userString), r.GetUserString(userString));
    foreach (var h in r.CustomDebugInformation)
    { var x = r.GetCustomDebugInformation(h); Add("debug/custom/" + MetadataTokens.GetToken(h), new { parent = T(x.Parent), kind = r.GetGuid(x.Kind), value = B(x.Value) }); }
    // These tables must be absent or accounted for above; fail closed on mixed-mode/EnC metadata.
    foreach (var table in new[] { TableIndex.FieldPtr, TableIndex.MethodPtr, TableIndex.ParamPtr, TableIndex.EventPtr, TableIndex.PropertyPtr, TableIndex.EncLog, TableIndex.EncMap, TableIndex.AssemblyProcessor, TableIndex.AssemblyOS, TableIndex.AssemblyRefProcessor, TableIndex.AssemblyRefOS, TableIndex.Document, TableIndex.MethodDebugInformation, TableIndex.LocalScope, TableIndex.LocalVariable, TableIndex.LocalConstant, TableIndex.ImportScope, TableIndex.StateMachineMethod })
        if (r.GetTableRowCount(table) != 0) throw new InvalidOperationException("Unaccounted nonempty table: " + table);
    if (pe.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size != 0) throw new InvalidOperationException("Native precompiled code unsupported");
    return new Snapshot(values, new { path = Path.GetFullPath(path), byteLength = bytes.Length, sha256 = Hash(bytes), mvid = r.GetGuid(module.Mvid), methodDefinitions = r.MethodDefinitions.Count, fieldDefinitions = r.FieldDefinitions.Count, typeDefinitions = r.TypeDefinitions.Count }, tableCounts);
}

sealed record Snapshot(SortedDictionary<string,string> Values, object Summary, Dictionary<string,int> TableCounts);
