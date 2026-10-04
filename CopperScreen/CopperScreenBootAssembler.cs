using System.Globalization;
using System.Text.RegularExpressions;

namespace CopperScreen;

// Deliberately small assembler for our original boot firmware. Runs at construction,
// before guest execution. No toolchain, compiled ROM or external CPU project.
internal sealed class CopperScreenBootAssembler
{
    private readonly Dictionary<string, uint> _symbols = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<byte> _bytes = [];
    private readonly uint _origin;
    private bool _resolving;
    internal CopperScreenBootAssembler(uint origin) => _origin = origin;
    internal uint Symbol(string name) => _symbols[name];
    internal byte[] Assemble(string source)
    {
        var lines = source.Replace("\r", "").Split('\n');
        for (var pass = 0; pass < 2; pass++)
        {
            _bytes.Clear();
            _resolving = pass != 0;
            foreach (var raw in lines)
            {
                var line = raw.Split(';')[0].Trim();
                if (line.Length == 0) continue;
                try { AssembleLine(line); }
                catch (Exception ex) { throw new InvalidOperationException($"Boot firmware: {line}: {ex.Message}", ex); }
            }
        }
        return _bytes.ToArray();
    }
    private void Word(uint value) { _bytes.Add((byte)(value >> 8)); _bytes.Add((byte)value); }
    private void Long(uint value) { Word(value >> 16); Word(value); }
    private uint Value(string expression)
    {
        expression = expression.Trim().TrimStart('#');
        var split = expression.IndexOfAny(['+', '-'], 1);
        if (split > 0) return expression[split] == '+' ? unchecked(Value(expression[..split]) + Value(expression[(split + 1)..])) : unchecked(Value(expression[..split]) - Value(expression[(split + 1)..]));
        if (expression.StartsWith('-')) return unchecked(0u - Value(expression[1..]));
        if (expression.StartsWith('$')) return uint.Parse(expression[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (uint.TryParse(expression, out var number)) return number;
        if (_symbols.TryGetValue(expression, out var symbol)) return symbol;
        return !_resolving ? 0u : throw new InvalidOperationException($"Unknown symbol {expression}");
    }
    private readonly record struct Operand(int Code, uint Extension = 0, int ExtensionSize = 0);
    private Operand Ea(string arg, int size)
    {
        arg = arg.ToLowerInvariant().Replace("sp", "a7");
        if (Regex.IsMatch(arg, "^[da][0-7]$")) return new((arg[0] == 'a' ? 8 : 0) + arg[1] - '0');
        if (arg.StartsWith('#')) return new(60, Value(arg), size == 2 ? 4 : 2);
        var match = Regex.Match(arg, @"^(-)?\(a([0-7])\)(\+)?$");
        if (match.Success) return new((match.Groups[1].Success ? 32 : match.Groups[3].Success ? 24 : 16) + int.Parse(match.Groups[2].Value));
        match = Regex.Match(arg, @"^(.+)\(a([0-7])\)$");
        if (match.Success) return new(40 + int.Parse(match.Groups[2].Value), Value(match.Groups[1].Value), 2);
        return new(57, Value(arg), 4);
    }
    private void Extension(Operand ea) { if (ea.ExtensionSize == 2) Word(ea.Extension); else if (ea.ExtensionSize == 4) Long(ea.Extension); }
    private void Op(uint opcode, Operand ea) { Word(opcode | (uint)ea.Code); Extension(ea); }
    private void AssembleLine(string line)
    {
        if (line.EndsWith(':')) { _symbols[line[..^1]] = _origin + (uint)_bytes.Count; return; }
        var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && parts[1].StartsWith("equ ", StringComparison.OrdinalIgnoreCase)) { _symbols[parts[0]] = Value(parts[1][4..]); return; }
        var name = parts[0].ToLowerInvariant();
        var args = parts.Length == 1 ? [] : parts[1].ToLowerInvariant().Split(',').Select(x => x.Trim()).ToArray();
        var size = name.EndsWith(".b") ? 0 : name.EndsWith(".l") ? 2 : 1;
        var mnemonic = name.Split('.')[0];
        if (mnemonic == "dc") { foreach (var arg in args) { if (size == 0) _bytes.Add((byte)Value(arg)); else if (size == 1) Word(Value(arg)); else Long(Value(arg)); } return; }
        if (mnemonic == "ds") { var count = (int)Value(args[0]) * (size == 0 ? 1 : size == 1 ? 2 : 4); _bytes.AddRange(new byte[count]); return; }
        if (mnemonic is "rts" or "rte" or "nop") { Word(mnemonic == "rts" ? 0x4e75u : mnemonic == "rte" ? 0x4e73u : 0x4e71u); return; }
        string[] conditions = ["bra", "bsr", "bhi", "bls", "bcc", "bcs", "bne", "beq", "bvc", "bvs", "bpl", "bmi", "bge", "blt", "bgt", "ble"];
        var condition = Array.IndexOf(conditions, mnemonic);
        if (condition >= 0)
        {
            var pc = _origin + (uint)_bytes.Count + 2;
            var displacement = unchecked((int)(Value(args[0]) - pc));
            if (_resolving && displacement is < short.MinValue or > short.MaxValue) throw new InvalidOperationException("Firmware branch exceeds its 16-bit range");
            Word((uint)(0x6000 | condition << 8)); Word(unchecked((uint)displacement)); return;
        }
        if (mnemonic == "dbra") { var pc = _origin + (uint)_bytes.Count + 2; Word((uint)(0x51c8 | args[0][1] - '0')); Word(unchecked(Value(args[1]) - pc)); return; }
        if (mnemonic == "moveq") { Word(0x7000u | (uint)(args[1][1] - '0') << 9 | Value(args[0]) & 255); return; }
        if (mnemonic == "move" && (args[0] == "usp" || args[1] == "usp")) { Word((uint)((args[0] == "usp" ? 0x4e68 : 0x4e60) | (args[0] == "usp" ? args[1][1] : args[0][1]) - '0')); return; }
        if (mnemonic == "move" && args[1] == "sr") { Op(0x46c0, Ea(args[0], 1)); return; }
        if (mnemonic == "move" && args[0] == "sr") { Op(0x40c0, Ea(args[1], 1)); return; }
        if (mnemonic == "move")
        {
            var src = Ea(args[0], size); var dst = Ea(args[1], size);
            Word((uint)((size == 0 ? 0x1000 : size == 1 ? 0x3000 : 0x2000) | (dst.Code & 7) << 9 | (dst.Code >> 3) << 6 | src.Code)); Extension(src); Extension(dst); return;
        }
        if (mnemonic == "lea") { Op(0x41c0u | (uint)(args[1][1] - '0') << 9, Ea(args[0], 2)); return; }
        if (mnemonic is "jmp" or "jsr" or "pea") { Op(mnemonic == "jmp" ? 0x4ec0u : mnemonic == "jsr" ? 0x4e80u : 0x4840u, Ea(args[0], 2)); return; }
        if (mnemonic == "movem")
        {
            var load = args[0].StartsWith('('); var list = load ? args[1] : args[0]; uint mask = 0;
            foreach (var group in list.Split('/'))
            {
                var range = group.Split('-'); var lo = (range[0][0] == 'a' ? 8 : 0) + range[0][1] - '0';
                var hi = range.Length == 1 ? lo : (range[1][0] == 'a' ? 8 : 0) + range[1][1] - '0';
                for (var r = lo; r <= hi; r++) mask |= 1u << r;
            }
            var ea = Ea(load ? args[0] : args[1], size);
            if (!load && ea.Code >> 3 == 4) { uint reverse = 0; for (var r = 0; r < 16; r++) if ((mask & 1u << r) != 0) reverse |= 1u << (15 - r); mask = reverse; }
            Word((load ? 0x4c80u : 0x4880u) | (size == 2 ? 64u : 0u) | (uint)ea.Code); Word(mask); Extension(ea); return;
        }
        if (mnemonic is "swap" or "ext") { Word((uint)((mnemonic == "swap" ? 0x4840 : size == 2 ? 0x48c0 : 0x4880) | args[0][1] - '0')); return; }
        if (mnemonic is "clr" or "tst" or "not" or "neg") { Op((mnemonic == "clr" ? 0x4200u : mnemonic == "tst" ? 0x4a00u : mnemonic == "not" ? 0x4600u : 0x4400u) | (uint)size << 6, Ea(args[0], size)); return; }
        if (mnemonic is "btst" or "bset" or "bclr")
        {
            var bitop = mnemonic == "btst" ? 0u : mnemonic == "bclr" ? 128u : 192u;
            var ea = Ea(args[1], 0);
            if (args[0].StartsWith('#')) { Word(0x0800u | bitop | (uint)ea.Code); Word(Value(args[0])); Extension(ea); }
            else Op(0x0100u | bitop | (uint)(args[0][1] - '0') << 9, ea);
            return;
        }
        if (mnemonic is "lsl" or "lsr" or "asr")
        {
            var registerCount = !args[0].StartsWith('#');
            var count = registerCount ? (uint)(args[0][1] - '0') : Value(args[0]) & 7;
            Word(0xe000u | count << 9 | (mnemonic == "lsl" ? 256u : 0u) | (uint)size << 6 | (registerCount ? 32u : 0u) | (mnemonic == "asr" ? 0u : 8u) | (uint)(args[1][1] - '0')); return;
        }
        if (mnemonic is "mulu" or "divu") { Op((mnemonic == "mulu" ? 0xc0c0u : 0x80c0u) | (uint)(args[1][1] - '0') << 9, Ea(args[0], 1)); return; }
        if (mnemonic.EndsWith('i'))
        {
            var opcode = mnemonic switch { "ori" => 0x0000u, "andi" => 0x0200u, "subi" => 0x0400u, "addi" => 0x0600u, "eori" => 0x0a00u, "cmpi" => 0x0c00u, _ => throw new InvalidOperationException(name) };
            var ea = args[1] == "sr" ? new Operand(60) : Ea(args[1], size);
            Word(opcode | (uint)size << 6 | (uint)ea.Code); Extension(Ea(args[0], size)); Extension(ea); return;
        }
        if (mnemonic is "add" or "sub" or "cmp" or "and" or "or" or "eor")
        {
            var address = args[1].Length == 2 && args[1][0] == 'a';
            var reverse = mnemonic == "eor" || !Regex.IsMatch(args[1], "^[da][0-7]$");
            var reg = reverse ? args[0] : args[1];
            var ea = Ea(reverse ? args[1] : args[0], size);
            var opcode = mnemonic switch { "add" => 0xd000u, "sub" => 0x9000u, "cmp" or "eor" => 0xb000u, "and" => 0xc000u, _ => 0x8000u };
            Op(opcode | (uint)(reg[1] - '0') << 9 | (uint)(address ? size == 2 ? 7 : 3 : size + (reverse ? 4 : 0)) << 6, ea); return;
        }
        throw new InvalidOperationException($"Unsupported firmware instruction {name}");
    }
}
