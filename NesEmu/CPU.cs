using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu
{
    using NesEmu.Mappers;
    using System.Diagnostics;
    using static CPU.AddressMode;
    using static CPU.Mnemonic;

    public class CPU
    {
        readonly Console console;
        readonly Memory memory;

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
            { 0x20, (JSR, Absolute, 6) },
            { 0x29, (AND, Immediate, 2) },
            { 0x4C, (JMP, Absolute, 3) },
            { 0x60, (RTS, Implied, 6) },
            { 0x69, (ADC, Immediate, 2) },
            { 0x78, (SEI, Implied, 2) },
            { 0x88, (DEY, Implied, 2) },
            { 0x8A, (TXA, Implied, 2) },
            { 0x8D, (STA, Absolute, 4) },
            { 0x98, (TYA, Implied, 2) },
            { 0x9A, (TXS, Implied, 2) },
            { 0xA0, (LDY, Immediate, 2) },
            { 0xA2, (LDX, Immediate, 2) },
            { 0xA8, (TAY, Implied, 2) },
            { 0xA9, (LDA, Immediate, 2) },
            { 0xAA, (TAX, Implied, 2) },
            { 0xAD, (LDA, Absolute, 4) },
            { 0xBA, (TSX, Implied, 2) },
            { 0xBD, (LDA, AbsoluteX, 4) },
            { 0xC8, (INY, Implied, 2) },
            { 0xCA, (DEX, Implied, 2) },
            { 0xCE, (DEC, Absolute, 6) },
            { 0xD0, (BNE, Relative, 2) },
            { 0xE0, (CPX, Immediate, 2) },
            { 0xE8, (INX, Implied, 2) },
            { 0xEE, (INC, Absolute, 6) }
        };

        public class Memory
        {
            private readonly byte[] WRAM = new byte[0x0800];
            private readonly Console console;

            public Memory(Console console)
            {
                this.console = console;
            }

            public byte Read(ushort addr)
            {
                if (addr < 0x2000)
                {
                    return WRAM[addr % 0x800];
                }
                else if (addr <= 0x2007)
                {
                    return console.PPU.Read(addr);
                }
                else if (addr == 0x4016 || addr == 0x4017)
                {
                    return console.Controller.ReadState();
                }
                else if (addr >= 0x8000)
                {
                    return console.Mapper.ReadPRG((ushort)(addr - 0x8000));
                }
                else
                {
                    Debug.Assert(false, "TODO: 0x" + addr.ToString("x4"));
                    return 0;
                }
            }

            public void Write(ushort addr, byte data)
            {
                if (addr < 0x2000)
                {
                    WRAM[addr % 0x800] = data;
                }
                else if (addr <= 0x2007)
                {
                    console.PPU.Write(addr, data);
                }
                else if (addr == 0x4014)
                {
                    console.PPU.WriteOAMDMA(this, data);
                    // TODO: suspend 513 or 514 cycles
                }
                else if (addr == 0x4016)
                {
                    console.Controller.WriteState(data);
                }
                else
                {
                    Debug.Assert(false, "TODO: 0x" + addr.ToString("x4"));
                }
            }
        }

        public CPU(Console console)
        {
            this.console = console;
            memory = new Memory(console);

            PC = 0x8000;//memory.Read(0xFFFC);
            Cycles = 0;
            S = 0xFF;
        }

        public int Step()
        {
            byte opCode = memory.Read(PC);
            if (!instructions.ContainsKey(opCode))
            {
                Debug.WriteLine("opcode is not implemented yet: 0x" + opCode.ToString("x2"));
                return 0;
            }
            var inst = instructions[opCode];
            int cycles = inst.cycles;

            Debug.WriteLine(inst.mnemonic + ", PC:0x" + PC.ToString("x4"));

            ushort addr = 0;
            switch (inst.addrMode)
            {
                case Absolute:
                    {
                        ushort lo = memory.Read((ushort)(PC + 1));
                        ushort hi = memory.Read((ushort)(PC + 2));
                        addr = (ushort)(hi << 8 | lo);
                    }
                    break;
                case AbsoluteX:
                    {
                        ushort lo = memory.Read((ushort)(PC + 1));
                        ushort hi = memory.Read((ushort)(PC + 2));
                        addr = (ushort)((hi << 8 | lo) + X);
                    }
                    break;
                case Immediate:
                    addr = (ushort)(PC + 1);
                    break;
                case Implied:
                    break;
                case Relative:
                    addr = (ushort)(PC + (sbyte)memory.Read((ushort)(PC + 1)));
                    break;
                default:
                    Debug.WriteLine("address mode is not implemented yet: " + inst.addrMode);
                    break;
            }

            switch (inst.mnemonic)
            {
                case LDA:   Load(ref A, addr);      break;
                case LDX:   Load(ref X, addr);      break;
                case LDY:   Load(ref Y, addr);      break;
                case STA:   Store(ref A, addr);     break;
                case TAX:   Transfer(A, ref X);     break;
                case TAY:   Transfer(A, ref Y);     break;
                case TSX:   Transfer(S, ref X);     break;
                case TXA:   Transfer(X, ref A);     break;
                case TXS:   Transfer(X, ref S);     break;
                case TYA:   Transfer(Y, ref A);     break;
                case ADC:
                    {
                        byte data = memory.Read(addr);
                        int sum = A + data + (C ? 1 : 0);
                        C = sum > 0xFF;
                        V = (~(A ^ data) & (A ^ (byte)sum) & 0x80) != 0; // https://stackoverflow.com/questions/29193303/6502-emulation-proper-way-to-implement-adc-and-sbc
                        A = (byte)sum;
                        UpdateNZ(A);
                    }
                    break;
                case AND:
                    A &= memory.Read(addr);
                    UpdateNZ(A);
                    break;
                case CPX:   Compare(ref X, addr);   break;
                case DEC:
                    {
                        byte data = memory.Read(addr);
                        memory.Write(addr, --data);
                        UpdateNZ(data);
                    }
                    break;
                case DEX:   Decrement(ref X);       break;
                case DEY:   Decrement(ref Y);       break;
                case INC:
                    {
                        byte data = memory.Read(addr);
                        memory.Write(addr, ++data);
                        UpdateNZ(data);
                    }
                    break;
                case INX:   Increment(ref X);       break;
                case INY:   Increment(ref Y);       break;
                case JMP:
                    PC = (ushort)(addr - inst.addrMode.Length());
                    break;
                case JSR:
                    PushStack16((ushort)(PC - 1 + inst.addrMode.Length()));
                    PC = (ushort)(addr - inst.addrMode.Length());
                    break;
                case RTS:
                    PC = (ushort)(PullStack16() + 1 - inst.addrMode.Length());
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
                case SEI:
                    // TODO:
                    break;
                default:
                    Debug.WriteLine("mnemonic is not implemented yet: " + inst.mnemonic);
                    break;
            }

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

        void Transfer(byte srcReg, ref byte dstReg)
        {
            dstReg = srcReg;
            UpdateNZ(dstReg);
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

        void Decrement(ref byte reg)
        {
            reg--;
            UpdateNZ(reg);
        }

        #region Common utils
        void UpdateNZ(byte value)
        {
            N = ((value >> 7) & 1) == 1;
            Z = (value == 0);
        }

        void PushStack(byte data)
        {
            memory.Write((ushort)(0x100 | S), data);
            S--;
        }

        byte PullStack()
        {
            S++;
            return memory.Read((ushort)(0x0100 | S));
        }

        void PushStack16(ushort data)
        {
            byte lo = (byte)(data & 0xFF);
            byte hi = (byte)((data >> 8) & 0xFF);

            PushStack(hi);
            PushStack(lo);
        }

        ushort PullStack16()
        {
            byte lo = PullStack();
            byte hi = PullStack();
            return (ushort)((hi << 8) | lo);
        }
        #endregion
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
