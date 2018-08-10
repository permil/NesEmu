using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu
{
    using System.Diagnostics;
    using static CPU.AddressMode;
    using static CPU.Mnemonic;

    class CPU
    {
        Memory memory = new Memory();
        public int Cycles { get; private set; }

        byte A, X, Y, S;
        ushort PC;

        bool C, Z, I, D, B, V, N;
        byte P {
            get
            {
                return (byte)((C ? (1 << 0) : 0) +
                              (Z ? (1 << 1) : 0) +
                              (I ? (1 << 2) : 0) +
                              (D ? (1 << 3) : 0) +
                              (B ? (1 << 4) : 0) +
                                   (1 << 5) + // reserved
                              (V ? (1 << 5) : 0) +
                              (N ? (1 << 6) : 0));
            }
            set
            {
                C = (value & (1 << 0)) != 0;
                Z = (value & (1 << 1)) != 0;
                I = (value & (1 << 2)) != 0;
                D = (value & (1 << 3)) != 0;
                B = (value & (1 << 4)) != 0;
                V = (value & (1 << 6)) != 0;
                Z = (value & (1 << 7)) != 0;
            }
        }

        public enum AddressMode
        {
            Absolute, AbsoluteX, AbsoluteY,
            Accumulator, Immediate, Implied,
            Indirect, IndirectX, IndirectY,
            Relative,
            ZeroPage, ZeroPageX, ZeroPageY
        };

        public enum Mnemonic
        {
            LDA, LDX, LDY, STA, STX, STY,
            TAX, TAY, TSX, TXA, TXS, TYA,
            ADC, AND, ASL, BIT, CMP, CPX, CPY,
            DEC, DEX, DEY, EOR, INC, INX, INY,
            LSR, ORA, ROL, ROR, SBC,
            PHA, PHP, PLA, PLP,
            JMP, JSR, RTS, RTI,
            BCC, BCS, BEQ, BMI, BNE, BPL, BVC, BVS,
            CLC, CLD, CLI, CLV, SEC, SED, SEI,
            BRK, NOP
        };
        Dictionary<int, (Mnemonic mnemonic, AddressMode addrMode, int cycles)> instructions = new Dictionary<int, (Mnemonic mnemonic, AddressMode addrMode, int cycles)>() {
            { 0x10, (BPL, Relative, 3) },
            { 0x4C, (JMP, Absolute, 3) },
            { 0x8D, (STA, Absolute, 4) },
            { 0xA2, (LDX, Immediate, 2) },
            { 0xA9, (LDA, Immediate, 2) },
            { 0xAD, (LDA, Absolute, 4) },
            { 0xBD, (LDA, AbsoluteX, 4) },
            { 0xD0, (BNE, Relative, 2) },
            { 0xE0, (CPX, Immediate, 2) },
            { 0xE8, (INX, Implied, 2) }
        };

        class Memory
        {
            private readonly byte[] WRAM = new byte[0x0800];

            public byte Read(ushort addr)
            {
                if (addr < 0x2000)
                {
                    return WRAM[addr % 0x800];
                }
                else
                {
                    // TODO:
                    return 0;
                }
            }

            public void Write(ushort addr, byte data)
            {
                if (addr < 0x2000)
                {
                    WRAM[addr % 0x800] = data;
                }
                else
                {
                    // TODO:
                }
            }
        }

        public CPU()
        {
            PC = memory.Read(0xFFFC);
            Cycles = 0;
            S = 0xFF;
        }

        public int Step()
        {
            byte opCode = memory.Read(PC);
            if (!instructions.ContainsKey(opCode))
            {
                Debug.WriteLine("opcode is not implemented yet: " + opCode);
                return 0;
            }
            var inst = instructions[opCode];
            int cycles = inst.cycles;

            ushort addr = 0;
            switch (inst.addrMode)
            {
                case Absolute:
                    ushort lo = memory.Read((ushort)(PC + 1));
                    ushort hi = memory.Read((ushort)(PC + 2));
                    addr = (ushort)(hi << 8 | lo);
                    break;
                case Immediate:
                    addr = (ushort)(PC + 1);
                    break;
                case Implied:
                    break;
                case Relative:
                    addr = (ushort)((PC + 2) + (sbyte)memory.Read((ushort)(PC + 1)));
                    break;
                default:
                    Debug.WriteLine("address mode is not implemented yet: " + inst.mnemonic);
                    break;
            }

            switch (inst.mnemonic)
            {
                case LDA:   Load(ref A, addr);      break;
                case LDX:   Load(ref X, addr);      break;
                case STA:   Store(ref A, addr);     break;
                case CPX:   Compare(ref X, addr);   break;
                case INX:   Increment(ref X);       break;
                case JMP:
                    PC = addr;
                    break;
                case BNE:
                    if (!Z)
                    {
                        PC = addr;
                    }
                    break;
                case BPL:
                    if (!N)
                    {
                        PC = addr;
                    }
                    break;
                default:
                    Debug.WriteLine("opcode is not implemented yet: " + inst.mnemonic);
                    break;
            }
            Debug.WriteLine(inst.mnemonic);

            PC += inst.addrMode.Length();
            Cycles += cycles;
            return cycles;
        }

        void Load(ref byte reg, ushort addr)
        {
            reg = memory.Read(addr);
            UpdateNZ(reg);
        }

        void Store(ref byte reg, ushort addr)
        {
            memory.Write(addr, reg);
        }

        void Compare(ref byte reg, ushort addr)
        {
            int diff = reg - memory.Read(addr);
            C = (diff >= 0);
            UpdateNZ((byte)diff);
        }

        void Increment(ref byte reg)
        {
            reg++;
            UpdateNZ(reg);
        }


        void UpdateNZ(byte value)
        {
            N = ((value >> 7) & 1) == 1;
            Z = (value == 0);
        }
    }

    static class AddressModeExt
    {
        public static ushort Length(this CPU.AddressMode param)
        {
            switch (param)
            {
                case Absolute: return 3;
                case AbsoluteX: return 3;
                case AbsoluteY: return 3;
                case Accumulator: return 1;
                case Immediate: return 2;
                case Implied: return 1;
                case Indirect: return 3;
                case IndirectX: return 2;
                case IndirectY: return 2;
                case Relative: return 2;
                case ZeroPage: return 2;
                case ZeroPageX: return 2;
                case ZeroPageY: return 2;
            }
            Debug.Assert(false);
            return 0;
        }
    }
}
