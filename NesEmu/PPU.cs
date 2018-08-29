﻿using NesEmu.Mappers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu
{
    public class PPU
    {
        readonly Console console;
        readonly Memory memory;

        class VRAMAddressRegister
        {
            public ushort Value { get; set; }

            // for $2005
            public byte NameTableSelect { private get { return (byte)((Value >> 10) & 0b11); } set { Value = (ushort)((Value & 0b1111001111111111) | (value << 10)); } }
            public ushort BaseNameTableAddress { get { return (ushort)(0x2000 + NameTableSelect * 0x0400); } }
            public ushort CoarseXScroll { get { return (ushort)(Value & 0b11111); } set { Value = (ushort)((Value >> 5 << 5) | value); } }
            public ushort CoarseYScroll { get { return (ushort)((Value >> 5) & 0b11111); } set { Value = (ushort)((Value & ~(0b11111 << 5)) | value); } }
            public byte FineYScroll { get { return (byte)((Value >> 12) & 0b111); } set { Value = (ushort)((Value & ~(0b111 << 12)) | value); } }
        }
        // PPU registers: https://wiki.nesdev.com/w/index.php/PPU_scrolling
        VRAMAddressRegister v = new VRAMAddressRegister();
        VRAMAddressRegister t = new VRAMAddressRegister();
        byte x;
        bool w;     // First or second write toggle

        // Object Attribute Memory: https://wiki.nesdev.com/w/index.php/PPU_OAM
        readonly byte[] OAM = new byte[0x100];
        byte OAMAddr;

        bool nmiOccurred;

        int scanline, cycle;

        // $2000
        // https://wiki.nesdev.com/w/index.php/PPU_registers#PPUCTRL
        // 7  bit  0
        // ---- ----
        // VPHB SINN
        // |||| ||||
        // |||| ||++- Base nametable address
        // |||| ||    (0 = $2000; 1 = $2400; 2 = $2800; 3 = $2C00)
        // |||| |+--- VRAM address increment per CPU read / write of PPUDATA
        // |||| |     (0: add 1, going across; 1: add 32, going down)
        // |||| +---- Sprite pattern table address for 8x8 sprites
        // ||||       (0: $0000; 1: $1000; ignored in 8x16 mode)
        // |||+------ Background pattern table address(0: $0000; 1: $1000)
        // ||+------- Sprite size(0: 8x8; 1: 8x16)
        // |+-------- PPU master / slave select
        // |          (0: read backdrop from EXT pins; 1: output color on EXT pins)
        // +--------- Generate an NMI at the start of the
        //            vertical blanking interval(0: off; 1: on)
        byte PPUCTRL {
            set {
                t.NameTableSelect = (byte)(value & 0b11);
                VRAMIncrement = (byte)(((value >> 2) & 1) == 0 ? 1 : 32);
                spritePatternTableAddress = (ushort)(((value >> 3) & 1) == 0 ? 0x0000 : 0x1000);
                BGPatternTableAddress = (ushort)(((value >> 4) & 1) == 0 ? 0x0000 : 0x1000);
                NMIEnabled = ((value >> 7) & 1) == 1;
            }
        }
        byte VRAMIncrement;
        ushort spritePatternTableAddress;
        ushort BGPatternTableAddress;
        // TODO: Sprite size
        // TODO: PPU master / slave
        bool NMIEnabled;

        // $2005
        byte PPUSCROLL
        {
            set
            {
                if (w)
                {
                    t.FineYScroll = (byte)(value & 0b111);
                    t.CoarseYScroll = (byte)(value >> 3);
                }
                else
                {
                    t.CoarseXScroll = (ushort)(value >> 3);
                    x = (byte)(value & 0b111);
                }
                w = !w;
            }
        }
        public byte FineXScroll { get { return (byte)(x & 0b111); } }

        // $2006
        byte PPUADDR {
            set {
                if (w)
                {
                    // t: ....... HGFEDCBA = d: HGFEDCBA
                    // v                   = t
                    t.Value = (ushort)((t.Value & 0xFF00) | value);
                    v.Value = t.Value;
                }
                else
                {
                    // t: .FEDCBA ........ = d: ..FEDCBA
                    // t: X...... ........ = 0
                    t.Value = (ushort)((t.Value & 0x00FF) | (value << 8));
                }
                w = !w;
            }
        }

        class Memory
        {
            private readonly Mapper mapper;
            private readonly byte[] VRAM = new byte[0x800];
            private readonly byte[] paletteRAM = new byte[0x20];

            public Memory(Mapper mapper)
            {
                this.mapper = mapper;
            }

            public byte Read(ushort addr)
            {
                if (addr < 0x2000)
                {
                    return mapper.ReadCHR(addr);
                }
                else if (addr < 0x3F00)
                {
                    return VRAM[(addr - 0x2000) % VRAM.Length]; // FIXME:
                }
                else if (addr < 0x4000)
                {
                    return paletteRAM[(addr - 0x3F00) % paletteRAM.Length];
                }
                else
                {
                    Debug.Assert(false, "Invalid PPU Memory Read: 0x" + addr.ToString("x4"));
                    return 0;
                }
            }

            public void Write(ushort addr, byte data)
            {
                if (addr < 0x2000)
                {
                    // TODO: write to CHR-RAM
                }
                else if (addr < 0x3F00)
                {
                    VRAM[(addr - 0x2000) % VRAM.Length] = data;
                }
                else if (addr < 0x4000)
                {
                    paletteRAM[GetPaletteRamIndex(addr)] = data;
                }
                else // Invalid Write
                {
                    Debug.Assert(false, "Invalid PPU Memory Write: 0x" + addr.ToString("x4"));
                }
            }

            byte GetPaletteRamIndex(ushort addr)
            {
                byte index = (byte)((addr - 0x3F00) % paletteRAM.Length);
                if (index >= 0x10 && index % 4 == 0)
                {
                    // https://wiki.nesdev.com/w/index.php/PPU_palettes#Memory_Map
                    index -= 0x10;
                }
                return index;
            }
        }

        public PPU(Console console)
        {
            this.console = console;
            memory = new Memory(console.Mapper);

            scanline = -1;
            cycle = 0;

            // $2000
            PPUCTRL = 0b00000000;
        }

        public void Step()
        {
            if (scanline == 241 && cycle == 1) // https://wiki.nesdev.com/w/index.php/PPU_rendering#Vertical_blanking_lines_.28241-260.29
            {
                nmiOccurred = true;
                if (NMIEnabled)
                {
                    console.CPU.NMIInterrupt = true;
                }
            }

            cycle++;
            if (cycle > 340)
            {
                scanline++;
                cycle = 0;
                if (scanline == 262)
                {
                    scanline = 0;
                }
            }

            bool renderingEnabled = true; // TODO: verify either of BG or Sprite rendering is enabled
            if (renderingEnabled)
            {
                if (cycle == 257)
                {
//                    v.CoarseXScroll = t.CoarseXScroll;
                    // TODO: copy nametable select
                }
                if (cycle >= 280 && cycle <= 304 && scanline == 261)
                {
 //                   v.CoarseYScroll = t.CoarseYScroll;
 //                   v.FineYScroll = t.FineYScroll;
                    // TODO: copy nametable select
                }
            }
        }

        byte LookupBGColor(byte paletteNum, byte colorNum)
        {
            return memory.Read((ushort)(colorNum == 0 ? 0x3F00 : (0x3F00 | (paletteNum << 2) | colorNum)));
        }

        byte LookupSpriteColor(byte paletteNum, byte colorNum)
        {
            return memory.Read((ushort)(colorNum == 0 ? 0x3F00 : (0x3F10 | (paletteNum << 2) | colorNum)));
        }

        const int HEIGHT = 30;
        const int WIDTH = 32;
        [Obsolete("Dummy Implementation")]
        public byte[] GetPixels()
        {
            byte[] pixels = new byte[(WIDTH * 8) * (HEIGHT * 8)];

            // BG
            int coarseX = t.CoarseXScroll; // FIXME: should refer v register
            int coarseY = t.CoarseYScroll; // FIXME: should refer v register
            for (int y = 0; y < HEIGHT; y++)
            {
                for (int x = 0; x < WIDTH; x++)
                {
                    byte tile  = memory.Read((ushort)(0x2000 + WIDTH * y + x)); // FIXME: refer correct name table
                    byte attrs = memory.Read((ushort)(0x23C0 + (WIDTH / 4) * (y / 4) + (x / 4))); // FIXME: refer correct attr table

                    byte paletteNum = (byte)((attrs >> (((((y / 2) & 0x1) << 1) | ((x / 2) & 0x1)) * 2)) & 0b11);

                    int xPos = (x - coarseX) % WIDTH;  // FIXME: should take account of fineX
                    int yPos = (y - coarseY) % HEIGHT; // FIXME: should take account of fineY
                    PutTile(pixels, (byte)(xPos * 8), (byte)(yPos * 8), tile, paletteNum, false, false, true);
                }
            }

            // Sprite
            for (int i = 0; i < OAM.Length / 4; i += 4)
            {
                byte y     = OAM[i];
                byte tile  = OAM[i + 1];
                byte attrs = OAM[i + 2];
                byte x     = OAM[i + 3];

                byte paletteNum = 0;
                bool flipX = ((attrs & 0x40) != 0);
                bool flipY = ((attrs & 0x80) != 0);

                PutTile(pixels, x, y, tile, paletteNum, flipX, flipY, false);
            }

            return pixels;
        }
        [Obsolete("Dummy Implementation")]
        public void PutTile(byte[] pixels, byte x, byte y, byte tile, byte paletteNum, bool flipX, bool flipY, bool bg)
        {
            for (int j = 0; j < 8; j++)
            {
                int yOffset = flipY ? 7 - j : j;
                ushort yAddr = (ushort)((bg ? BGPatternTableAddress : spritePatternTableAddress) + tile * 16 + yOffset);

                // https://wiki.nesdev.com/w/index.php/PPU_pattern_tables
                byte[] pattern = new byte[2];
                pattern[0] = memory.Read(yAddr);
                pattern[1] = memory.Read((ushort)(yAddr + 8));
                for (int k = 0; k < 8; k++)
                {
                    int xOffset = (flipX ? 7 - k : k);
                    byte loBit = (byte)((pattern[0] >> (7 - xOffset)) & 1);
                    byte hiBit = (byte)((pattern[1] >> (7 - xOffset)) & 1);
                    byte colorNum = (byte)((hiBit << 1) | loBit);

                    if (bg || colorNum != 0)
                    {
                        int pixelIndex = (WIDTH * 8) * ((y + j) % (HEIGHT * 8)) + ((x + k) % (WIDTH * 8));
                        pixels[pixelIndex] = (bg ? LookupBGColor(paletteNum, colorNum) : LookupSpriteColor(paletteNum, colorNum));
                    }
                }
            }
        }

        public byte Read(ushort addr)
        {
            // TODO:
            switch (addr)
            {
                case 0x2002:
                    {
                        byte data = 0;
                        data |= (byte)((nmiOccurred ? 1 : 0) << 7);
                        nmiOccurred = false;
                        // TODO: other statuses

                        w = false; // https://wiki.nesdev.com/w/index.php/PPU_scrolling#.242002_read

                        return data;
                    }
                case 0x2007:
                    {
                        byte data = memory.Read(v.Value);
                        v.Value += VRAMIncrement;
                        return data;
                    }
                default:
                    Debug.WriteLine("PPU Read - 0x" + addr.ToString("x4"));
                    return 0; // TODO:
            }
        }

        public void Write(ushort addr, byte data)
        {
            Debug.WriteLine("PPU Write - 0x" + addr.ToString("x4") + " <- " + data);
            switch (addr)
            {
                case 0x2000:    PPUCTRL = data;     break;
                case 0x2003:    OAMAddr = data;     break;
                case 0x2004:
                    OAM[OAMAddr] = data;
                    OAMAddr++;
                    break;
                case 0x2005:    PPUSCROLL = data;   break;
                case 0x2006:    PPUADDR = data;     break;
                case 0x2007:
                    memory.Write(v.Value, data);
                    v.Value += VRAMIncrement;
                    break;
                default:
                    // TODO:
                    break;
            }
        }

        public void WriteOAMDMA(CPU.Memory cpuMemory, byte data)
        {
            ushort addr = (ushort)(data << 8);
            for (int i = 0; i < 256; i++)
            {
                OAM[(OAMAddr + i) % OAM.Length] = cpuMemory.Read((ushort)(addr + i));
            }
        }
    }
}
