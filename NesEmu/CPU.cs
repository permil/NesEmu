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
                N = (value & (1 << 7)) != 0;
            }
        }

        public bool NMIInterrupt;

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
            { 0x00, (BRK, Implied, 7) },
            { 0x01, (ORA, IndirectX, 6) },
            { 0x05, (ORA, ZeroPage, 3) },
            { 0x06, (ASL, ZeroPage, 5) },
            { 0x08, (PHP, Implied, 3) },
            { 0x09, (ORA, Immediate, 2) },
            { 0x0A, (ASL, Accumulator, 2) },
            { 0x0D, (ORA, Absolute, 4) },
            { 0x0E, (ASL, Absolute, 6) },
            { 0x10, (BPL, Relative, 3) },
            { 0x11, (ORA, IndirectY, 5) },
            { 0x15, (ORA, ZeroPageX, 4) },
            { 0x16, (ASL, ZeroPageX, 6) },
            { 0x18, (CLC, Implied, 2) },
            { 0x19, (ORA, ZeroPageY, 4) },
            { 0x1D, (ORA, AbsoluteX, 4) },
            { 0x1E, (ASL, AbsoluteX, 7) },
            { 0x20, (JSR, Absolute, 6) },
            { 0x21, (AND, IndirectX, 6) },
            { 0x24, (BIT, ZeroPage, 3) },
            { 0x25, (AND, ZeroPage, 3) },
            { 0x26, (ROL, ZeroPage, 5) },
            { 0x28, (PLP, Implied, 4) },
            { 0x29, (AND, Immediate, 2) },
            { 0x2A, (ROL, Accumulator, 2) },
            { 0x2C, (BIT, Absolute, 4) },
            { 0x2D, (AND, Absolute, 4) },
            { 0x2E, (ROL, Absolute, 6) },
            { 0x30, (BMI, Relative, 2) },
            { 0x31, (AND, IndirectY, 5) },
            { 0x35, (AND, ZeroPageX, 4) },
            { 0x36, (ROL, ZeroPageX, 6) },
            { 0x38, (SEC, Implied, 2) },
            { 0x39, (AND, AbsoluteY, 4) },
            { 0x3D, (AND, AbsoluteX, 4) },
            { 0x3E, (ROL, AbsoluteX, 7) },
            { 0x40, (RTI, Implied, 6) },
            { 0x41, (EOR, IndirectX, 6) },
            { 0x45, (EOR, ZeroPage, 3) },
            { 0x46, (LSR, ZeroPage, 5) },
            { 0x48, (PHA, Implied, 3) },
            { 0x49, (EOR, Immediate, 2) },
            { 0x4A, (LSR, Accumulator, 2) },
            { 0x4C, (JMP, Absolute, 3) },
            { 0x4D, (EOR, Absolute, 4) },
            { 0x4E, (LSR, Absolute, 6) },
            { 0x50, (BVC, Relative, 2) },
            { 0x51, (EOR, IndirectY, 5) },
            { 0x55, (EOR, ZeroPageX, 4) },
            { 0x56, (LSR, ZeroPageX, 6) },
            { 0x58, (CLI, Implied, 2) },
            { 0x59, (EOR, AbsoluteY, 4) },
            { 0x5D, (EOR, AbsoluteX, 4) },
            { 0x5E, (LSR, AbsoluteX, 7) },
            { 0x60, (RTS, Implied, 6) },
            { 0x61, (ADC, IndirectX, 6) },
            { 0x65, (ADC, ZeroPage, 3) },
            { 0x66, (ROR, ZeroPage, 5) },
            { 0x68, (PLA, Implied, 4) },
            { 0x69, (ADC, Immediate, 2) },
            { 0x6A, (ROR, Accumulator, 2) },
            { 0x6C, (JMP, Indirect, 5) },
            { 0x6D, (ADC, Absolute, 4) },
            { 0x6E, (ROR, Absolute, 6) },
            { 0x70, (BVS, Relative, 2) },
            { 0x71, (ADC, IndirectY, 5) },
            { 0x75, (ADC, ZeroPageX, 4) },
            { 0x76, (ROR, ZeroPageX, 6) },
            { 0x78, (SEI, Implied, 2) },
            { 0x79, (ADC, AbsoluteY, 4) },
            { 0x7D, (ADC, AbsoluteX, 4) },
            { 0x7E, (ROR, AbsoluteX, 7) },
            { 0x81, (STA, IndirectX, 6) },
            { 0x84, (STY, ZeroPage, 3) },
            { 0x85, (STA, ZeroPage, 3) },
            { 0x86, (STX, ZeroPage, 3) },
            { 0x88, (DEY, Implied, 2) },
            { 0x8A, (TXA, Implied, 2) },
            { 0x8C, (STY, Absolute, 4) },
            { 0x8D, (STA, Absolute, 4) },
            { 0x8E, (STX, Absolute, 4) },
            { 0x90, (BCC, Relative, 2) },
            { 0x91, (STA, IndirectY, 2) },
            { 0x94, (STY, ZeroPageX, 4) },
            { 0x95, (STA, ZeroPageX, 4) },
            { 0x96, (STX, ZeroPageY, 4) },
            { 0x98, (TYA, Implied, 2) },
            { 0x99, (STA, AbsoluteY, 5) },
            { 0x9A, (TXS, Implied, 2) },
            { 0x9D, (STA, AbsoluteX, 5) },
            { 0xA0, (LDY, Immediate, 2) },
            { 0xA1, (LDA, IndirectX, 6) },
            { 0xA2, (LDX, Immediate, 2) },
            { 0xA4, (LDY, ZeroPage, 3) },
            { 0xA5, (LDA, ZeroPage, 3) },
            { 0xA6, (LDX, ZeroPage, 3) },
            { 0xA8, (TAY, Implied, 2) },
            { 0xA9, (LDA, Immediate, 2) },
            { 0xAA, (TAX, Implied, 2) },
            { 0xAC, (LDY, Absolute, 4) },
            { 0xAD, (LDA, Absolute, 4) },
            { 0xAE, (LDX, Absolute, 4) },
            { 0xB0, (BCS, Relative, 2) },
            { 0xB1, (LDA, IndirectY, 5) },
            { 0xB4, (LDY, ZeroPageX, 4) },
            { 0xB5, (LDA, ZeroPageX, 4) },
            { 0xB6, (LDX, ZeroPageY, 4) },
            { 0xB8, (CLV, Implied, 2) },
            { 0xB9, (LDA, AbsoluteY, 4) },
            { 0xBA, (TSX, Implied, 2) },
            { 0xBC, (LDY, AbsoluteX, 4) },
            { 0xBD, (LDA, AbsoluteX, 4) },
            { 0xBE, (LDX, AbsoluteY, 4) },
            { 0xC0, (CPY, Immediate, 2) },
            { 0xC1, (CMP, IndirectX, 6) },
            { 0xC4, (CPY, ZeroPage, 3) },
            { 0xC5, (CMP, ZeroPage, 3) },
            { 0xC6, (DEC, ZeroPage, 5) },
            { 0xC8, (INY, Implied, 2) },
            { 0xC9, (CMP, Immediate, 2) },
            { 0xCA, (DEX, Implied, 2) },
            { 0xCC, (CPY, Absolute, 4) },
            { 0xCD, (CMP, Absolute, 4) },
            { 0xCE, (DEC, Absolute, 6) },
            { 0xD0, (BNE, Relative, 2) },
            { 0xD1, (CMP, IndirectY, 5) },
            { 0xD5, (CMP, ZeroPageX, 4) },
            { 0xD6, (DEC, ZeroPageX, 6) },
            { 0xD8, (CLD, Implied, 2) },
            { 0xD9, (CMP, AbsoluteY, 4) },
            { 0xDD, (CMP, AbsoluteX, 4) },
            { 0xDE, (DEC, AbsoluteX, 7) },
            { 0xE0, (CPX, Immediate, 2) },
            { 0xE1, (SBC, IndirectX, 6) },
            { 0xE4, (CPX, ZeroPage, 3) },
            { 0xE5, (SBC, ZeroPage, 3) },
            { 0xE6, (INC, ZeroPage, 5) },
            { 0xE8, (INX, Implied, 2) },
            { 0xE9, (SBC, Immediate, 2) },
            { 0xEA, (NOP, Implied, 1) },
            { 0xEC, (CPX, Absolute, 4) },
            { 0xED, (SBC, Absolute, 4) },
            { 0xEE, (INC, Absolute, 6) },
            { 0xF0, (BEQ, Relative, 2) },
            { 0xF1, (SBC, IndirectY, 5) },
            { 0xF5, (SBC, ZeroPageX, 4) },
            { 0xF6, (INC, ZeroPageX, 6) },
            { 0xF8, (SED, Implied, 2) },
            { 0xF9, (SBC, AbsoluteY, 4) },
            { 0xFD, (SBC, AbsoluteX, 4) },
            { 0xFE, (INC, AbsoluteX, 7) },
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
                    return console.Controller[addr - 0x4016].ReadState();
                }
                else if (addr >= 0x4000 && addr <= 0x4015)
                {
                    // TODO: APU
                    return 0;
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
                    console.Controller[0].WriteState(data);
                    console.Controller[1].WriteState(data);
                }
                else if (addr >= 0x4000 && addr <= 0x4017)
                {
                    // TODO: APU
                }
                else
                {
                    Debug.Assert(false, "TODO: 0x" + addr.ToString("x4"));
                }
            }

            public ushort Read16(ushort address)
            {
                byte lo = Read(address);
                byte hi = Read((ushort)(address + 1));
                return (ushort)((hi << 8) | lo);
            }

            // due to 6502's indirect jmp bug, need to wrap page in case of the address ends 0xFF
            // https://everything2.com/title/6502+indirect+JMP+bug
            public ushort Read16WrapPage(ushort address)
            {
                if ((address & 0xFF) == 0xFF)
                {
                    byte lo = Read(address);
                    byte hi = Read((ushort)(address & (~0xFF))); // Wrap around to start of page eg. 0x02FF becomes 0x0200
                    return (ushort)((hi << 8) | lo);
                }
                else
                {
                    return Read16(address);
                }
            }
        }

        public CPU(Console console)
        {
            this.console = console;
            memory = new Memory(console);

            PC = memory.Read16(0xFFFC);
            Cycles = 0;
            S = 0xFF;
            P = 0b00100100;

            NMIInterrupt = false;
        }

        bool debug;
        public void ToggleDebugLog()
        {
            debug = !debug;
        }

        public int Step()
        {
            if (NMIInterrupt)
            {
                PushStack16(PC);
                PushStack(P);
                PC = memory.Read16(0xFFFA);
                I = true;
            }
            NMIInterrupt = false;

            byte opCode = memory.Read(PC);
            if (!instructions.ContainsKey(opCode))
            {
                Debug.WriteLine("opcode is not implemented yet: 0x" + opCode.ToString("x2") + ", PC: 0x" + Convert.ToString(PC, 16));
                return 0;
            }
            var inst = instructions[opCode];
            int cycles = inst.cycles;

            if (debug) Debug.WriteLine(inst.mnemonic + ", " + inst.addrMode + ", PC:0x" + PC.ToString("x4") + ", A:" + A + ", X:" + X + ", Y:" + Y + ", P:" + Convert.ToString(P, 2));

            ushort addr = 0;
            switch (inst.addrMode)
            {
                case Absolute:
                    addr = memory.Read16((ushort)(PC + 1));
                    break;
                case AbsoluteX:
                    addr = (ushort)(memory.Read16((ushort)(PC + 1)) + X);
                    break;
                case AbsoluteY:
                    addr = (ushort)(memory.Read16((ushort)(PC + 1)) + Y);
                    break;
                case Accumulator:
                    break;
                case Immediate:
                    addr = (ushort)(PC + 1);
                    break;
                case Implied:
                    break;
                case Indirect:
                    addr = memory.Read16WrapPage((ushort)memory.Read16((ushort)(PC + 1)));
                    break;
                case IndirectX:
                    addr = memory.Read16WrapPage((ushort)((memory.Read((ushort)(PC + 1)) + X) & 0xFF));
                    break;
                case IndirectY:
                    addr = (ushort)(memory.Read16WrapPage((ushort)(memory.Read((ushort)(PC + 1)))) + Y);
                    break;
                case Relative:
                    addr = (ushort)(PC + (sbyte)memory.Read((ushort)(PC + 1)) + 2);
                    break;
                case ZeroPage:
                    addr = memory.Read((ushort)(PC + 1));
                    break;
                case ZeroPageX:
                    addr = (ushort)((memory.Read((ushort)(PC + 1)) + X) & 0xFF);
                    break;
                case ZeroPageY:
                    addr = (ushort)((memory.Read((ushort)(PC + 1)) + Y) & 0xFF);
                    break;
                default:
                    Debug.WriteLine("address mode is not implemented yet: " + inst.addrMode);
                    break;
            }

            PC += inst.addrMode.Length();
            Cycles += cycles;

            switch (inst.mnemonic)
            {
                case LDA:   Load(ref A, addr);      break;
                case LDX:   Load(ref X, addr);      break;
                case LDY:   Load(ref Y, addr);      break;
                case STA:   Store(ref A, addr);     break;
                case STX:   Store(ref X, addr);     break;
                case STY:   Store(ref Y, addr);     break;
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
                case ASL:
                    {
                        byte data = (inst.addrMode == Accumulator) ? A : memory.Read(addr);
                        C = (data & (1 << 7)) != 0;
                        data <<= 1;
                        if (inst.addrMode == Accumulator)
                        {
                            A = data;
                        }
                        else
                        {
                            memory.Write(addr, data);
                        }
                        UpdateNZ(data);
                    }
                    break;
                case BIT:
                    {
                        byte data = memory.Read(addr);
                        N = (data & (1 << 7)) != 0;
                        V = (data & (1 << 6)) != 0;
                        Z = (data & A) == 0;
                    }
                    break;
                case CMP:   Compare(ref A, addr);   break;
                case CPX:   Compare(ref X, addr);   break;
                case CPY:   Compare(ref Y, addr);   break;
                case DEC:
                    {
                        byte data = memory.Read(addr);
                        memory.Write(addr, --data);
                        UpdateNZ(data);
                    }
                    break;
                case DEX:   Decrement(ref X);       break;
                case DEY:   Decrement(ref Y);       break;
                case EOR:
                    A ^= memory.Read(addr);
                    UpdateNZ(A);
                    break;
                case INC:
                    {
                        byte data = memory.Read(addr);
                        memory.Write(addr, ++data);
                        UpdateNZ(data);
                    }
                    break;
                case INX:   Increment(ref X);       break;
                case INY:   Increment(ref Y);       break;
                case LSR:
                    {
                        byte data = (inst.addrMode == Accumulator) ? A : memory.Read(addr);
                        C = (data & 1) != 0;
                        data >>= 1;
                        if (inst.addrMode == Accumulator)
                        {
                            A = data;
                        }
                        else
                        {
                            memory.Write(addr, data);
                        }
                        UpdateNZ(data);
                    }
                    break;
                case ORA:
                    A |= memory.Read(addr);
                    UpdateNZ(A);
                    break;
                case ROL:
                    {
                        int data = (inst.addrMode == Accumulator ? A : memory.Read(addr)) << 1 | (C ? 1 : 0);
                        C = (data & 1 << 8) != 0;
                        if (inst.addrMode == Accumulator)
                        {
                            A = (byte)data;
                        }
                        else
                        {
                            memory.Write(addr, (byte)data);
                        }
                        UpdateNZ((byte)data);
                    }
                    break;
                case ROR:
                    {
                        int data = (C ? 1 : 0) << 8 | (inst.addrMode == Accumulator ? A : memory.Read(addr));
                        C = (data & 1) != 0;
                        if (inst.addrMode == Accumulator)
                        {
                            A = (byte)(data >> 1);
                        }
                        else
                        {
                            memory.Write(addr, (byte)(data >> 1));
                        }
                        UpdateNZ((byte)(data >> 1));
                    }
                    break;
                case SBC:
                    {
                        byte data = memory.Read(addr);
                        int sub = A - data - (!C ? 1 : 0);
                        C = sub >= 0;
                        V = ((A ^ sub) & (A ^ (byte)sub) & 0x80) != 0; // https://stackoverflow.com/questions/29193303/6502-emulation-proper-way-to-implement-adc-and-sbc
                        A = (byte)sub;
                        UpdateNZ(A);
                    }
                    break;
                case PHA:   PushStack(A);           break;
                case PHP:   PushStack(P);           break;
                case PLA:   A = PullStack();    UpdateNZ(A);    break;
                case PLP:   P = PullStack();        break;
                case JMP:
                    PC = addr;
                    break;
                case JSR:
                    PushStack16((ushort)(PC - 1));
                    PC = addr;
                    break;
                case RTS:
                    PC = (ushort)(PullStack16() + 1);
                    break;
                case RTI:
                    P = PullStack();
                    PC = PullStack16();
                    break;
                case BCC:   Branch(!C, addr);       break;
                case BCS:   Branch( C, addr);       break;
                case BEQ:   Branch( Z, addr);       break;
                case BMI:   Branch( N, addr);       break;
                case BNE:   Branch(!Z, addr);       break;
                case BPL:   Branch(!N, addr);       break;
                case BVC:   Branch(!V, addr);       break;
                case BVS:   Branch( V, addr);       break;
                case CLC:   C = false;              break;
                case CLD:                           break;  // disable decimal mode, nothing happens on nes
                case CLI:   I = false;              break;
                case CLV:   V = false;              break;
                case SEC:   C = true;               break;
                case SED:                           break;  // enable decimal mode, nothing happens on nes
                case SEI:   I = true;               break;
                case BRK:
                    PushStack16(PC);
                    PushStack(P);
                    B = true;
                    PC = memory.Read16((ushort)0xFFFE);
                    break;
                case NOP:                           break;
                default:
                    Debug.WriteLine("mnemonic is not implemented yet: " + inst.mnemonic);
                    break;
            }

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

        void Branch(bool state, ushort addr)
        {
            if (state)
            {
                PC = addr;
            }
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
