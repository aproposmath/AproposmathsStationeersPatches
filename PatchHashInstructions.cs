using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Assets.Scripts.Objects.Electrical;
using IC10_Extender;
using UnityEngine;

namespace AproposmathsStationeersPatches
{
    public static class Crc32
    {
        private static readonly uint[] Table = CreateTable();
        private static readonly byte[][] Numbers = CreateNumbersTable();

        private static uint[] CreateTable()
        {
            const uint poly = 0xEDB88320u;
            var table = new uint[256];
            for (uint i = 0; i < table.Length; i++)
            {
                uint crc = i;
                for (int j = 0; j < 8; j++)
                    crc = (crc & 1) != 0 ? (poly ^ (crc >> 1)) : (crc >> 1);
                table[i] = crc;
            }
            return table;
        }

        public static byte[][] CreateNumbersTable()
        {
            var numbers = new byte[512][];
            for (int i = -256; i < 256; i++)
            {
                numbers[i + 256] = System.Text.Encoding.UTF8.GetBytes(i.ToString());
            }
            return numbers;
        }

        public static uint Compute(byte[] bytes, int start = 0)
        {
            uint crc = unchecked((uint)~start);

            foreach (var b in bytes)
            {
                byte index = (byte)((crc ^ b) & 0xFF);
                crc = (crc >> 8) ^ Table[index];
            }
            return ~crc;
        }

        public static int ComputeSigned(byte[] bytes, int start = 0)
        {
            return unchecked((int)Compute(bytes, start));
        }

        public static int HashN(int start, int n)
        {
            if (n >= -256 && n < 256)
            {
                var data = Numbers[n + 256];
                return ComputeSigned(data, start);
            }
            else
            {
                var stringn = n.ToString();
                var data = System.Text.Encoding.UTF8.GetBytes(stringn);
                // return 0;
                return ComputeSigned(data, start);
            }
        }
    }

    public static class IC10HashInstructions
    {
        private static bool? _enabled = null;

        public static bool Enabled
        {
            [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
            get
            {
                if (_enabled == null)
                {
                    Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                    bool found = false;
                    foreach (var assembly in assemblies)
                    {
                        string message = assembly.GetName().Name;
                        if (message.Equals("IC10Extender"))
                        {
                            found = true;
                            break;
                        }
                    }

                    L.Info($"IC10HashInstructions Enabled={found}");

                    _enabled = found;
                }

                return (bool)_enabled;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        public static void Register()
        {
            IC10Extender.Register(new HashOp());
            IC10Extender.Register(new HashOp2());
            IC10Extender.Register(new HashOpN());
            IC10Extender.Register(new Itoa());
        }

        public class HashOp : ExtendedOpCode
        {
            public class Instance : Operation
            {
                protected readonly IndexVariable Store;
                protected readonly DoubleValueVariable StringValue;

                public Instance(ChipWrapper chip, int lineNumber, string register, string hashString) : base(chip, lineNumber)
                {
                    Store = new IndexVariable(chip.chip, lineNumber, register, InstructionInclude.MaskStoreIndex, false);
                    StringValue = new DoubleValueVariable(chip.chip, lineNumber, hashString, InstructionInclude.MaskDoubleValue, false);
                }

                public override int Execute(int index)
                {
                    int variableIndex = Store.GetVariableIndex(AliasTarget.Register);
                    double str = StringValue.GetVariableValue(AliasTarget.Register);
                    string hashString = ProgrammableChip.UnpackAscii6(str, true);
                    Chip.Registers[variableIndex] = Animator.StringToHash(hashString);
                    return index + 1;
                }

            }

            public HashOp(string opname = "hash") : base(opname) { }

            public override void Accept(int lineNumber, string[] source)
            {
                if (source.Length != 2) throw new ProgrammableChipException(ProgrammableChipException.ICExceptionType.IncorrectArgumentCount, lineNumber);
            }

            public override Operation Create(ChipWrapper chip, int lineNumber, string[] source)
            {
                return new Instance(chip, lineNumber, source[1], source[2]);
            }

            public override HelpString[] Params()
            {
                return new HelpString[] { ProgrammableChip.REGISTER, (ProgrammableChip.REGISTER + ProgrammableChip.NUMBER).Var("a") };
            }
        }

        public class HashOp2 : HashOp
        {

            new public class Instance : HashOp.Instance
            {
                protected readonly DoubleValueVariable StringValue2;

                public Instance(ChipWrapper chip, int lineNumber, string register, string hashString, string hashString2) : base(chip, lineNumber, register, hashString)
                {
                    StringValue2 = new DoubleValueVariable(chip.chip, lineNumber, hashString2, InstructionInclude.MaskDoubleValue, false);
                }

                public override int Execute(int index)
                {
                    int variableIndex = Store.GetVariableIndex(AliasTarget.Register);
                    double str = StringValue.GetVariableValue(AliasTarget.Register);
                    double str2 = StringValue2.GetVariableValue(AliasTarget.Register);
                    string hashString = ProgrammableChip.UnpackAscii6(str, true) + ProgrammableChip.UnpackAscii6(str2, true);
                    Chip.Registers[variableIndex] = Animator.StringToHash(hashString);
                    return index + 1;
                }
            }

            public HashOp2() : base("hash2") { }

            public override void Accept(int lineNumber, string[] source)
            {
                if (source.Length != 3) throw new ProgrammableChipException(ProgrammableChipException.ICExceptionType.IncorrectArgumentCount, lineNumber);
            }

            public override Operation Create(ChipWrapper chip, int lineNumber, string[] source)
            {
                return new Instance(chip, lineNumber, source[1], source[2], source[3]);
            }
            public override HelpString[] Params()
            {
                var arg = ProgrammableChip.REGISTER + ProgrammableChip.NUMBER;
                return new HelpString[] { ProgrammableChip.REGISTER, arg.Var("a"), arg.Var("b") };
            }
        }

        public class HashOpN : HashOp
        {

            new public class Instance : Operation
            {
                protected readonly IndexVariable Store;
                protected readonly IntValuedVariable StartHash;
                protected readonly IntValuedVariable Number;

                public Instance(ChipWrapper chip, int lineNumber, string register, string hashValue, string numberValue) : base(chip, lineNumber)
                {
                    Store = new IndexVariable(chip.chip, lineNumber, register, InstructionInclude.MaskStoreIndex, false);
                    StartHash = new IntValuedVariable(chip.chip, lineNumber, hashValue, InstructionInclude.MaskIntValue, false);
                    Number = new IntValuedVariable(chip.chip, lineNumber, numberValue, InstructionInclude.MaskIntValue, false);
                }

                public override int Execute(int index)
                {
                    int variableIndex = Store.GetVariableIndex(AliasTarget.Register);
                    int hash = StartHash.GetVariableValue(AliasTarget.Register);
                    int n = Number.GetVariableValue(AliasTarget.Register);
                    Chip.Registers[variableIndex] = Crc32.HashN(hash, n);
                    return index + 1;
                }
            }

            public HashOpN() : base("hashn") { }

            public override void Accept(int lineNumber, string[] source)
            {
                if (source.Length != 3) throw new ProgrammableChipException(ProgrammableChipException.ICExceptionType.IncorrectArgumentCount, lineNumber);
            }

            public override Operation Create(ChipWrapper chip, int lineNumber, string[] source)
            {
                return new Instance(chip, lineNumber, source[1], source[2], source[3]);
            }
            public override HelpString[] Params()
            {
                var arg = ProgrammableChip.REGISTER + ProgrammableChip.NUMBER;
                return new HelpString[] { ProgrammableChip.REGISTER, arg.Var("a"), arg.Var("b") };
            }

            public override string Description()
            {
                return "Takes a hash value and and integer N, computes the hash of the concatenation of the original string and the string representation of N.\n\tExample: hashn r0 HASH(\"PUMP \") 123 # computes the hash of \"PUMP 123\" and stores it in r0.";
            }
        }

        public class Itoa : HashOp
        {

            new public class Instance : Operation
            {
                protected readonly IndexVariable Store;
                protected readonly IntValuedVariable IntValue;

                public Instance(ChipWrapper chip, int lineNumber, string register, string intValue) : base(chip, lineNumber)
                {
                    Store = new IndexVariable(chip.chip, lineNumber, register, InstructionInclude.MaskStoreIndex, false);
                    IntValue = new IntValuedVariable(chip.chip, lineNumber, intValue, InstructionInclude.MaskIntValue, false);
                }

                public override int Execute(int index)
                {
                    int variableIndex = Store.GetVariableIndex(AliasTarget.Register);
                    int value = IntValue.GetVariableValue(AliasTarget.Register);
                    Chip.Registers[variableIndex] = ProgrammableChip.PackAscii6(value.ToString(), index);
                    return index + 1;
                }
            }

            public Itoa() : base("itoa") { }

            public override Operation Create(ChipWrapper chip, int lineNumber, string[] source)
            {
                return new Instance(chip, lineNumber, source[1], source[2]);
            }
            public override HelpString[] Params()
            {
                var arg = ProgrammableChip.INTEGER;
                return new HelpString[] { ProgrammableChip.REGISTER, arg.Var("a") };
            }
        }
    }
}
