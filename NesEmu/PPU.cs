using NesEmu.Mappers;
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

        // PPU registers: https://wiki.nesdev.com/w/index.php/PPU_scrolling
        ushort v;   // Current VRAM address (15 bits)
        ushort t;   // Temporary VRAM address (15 bits); can also be thought of as the address of the top left onscreen tile.
        bool w;     // First or second write toggle

        // Object Attribute Memory: https://wiki.nesdev.com/w/index.php/PPU_OAM
        readonly byte[] OAM = new byte[0x100];
        byte OAMAddr;

        bool nmiOccurred;

        int scanline, cycle;

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
        byte PPUCTRL { get; set; }
        ushort BaseNameTableAddress { get { return (ushort)(0x2000 + (PPUCTRL & 0b11) * 0x0400); } }
        byte VRAMIncrement { get { return (byte)(((PPUCTRL >> 2) & 1) == 0 ? 1 : 32); } }
        ushort SpritePatternTableAddress { get { return (ushort)(((PPUCTRL >> 3) & 1) == 0 ? 0x0000 : 0x1000); } }
        ushort BGPatternTableAddress { get { return (ushort)(((PPUCTRL >> 4) & 1) == 0 ? 0x0000 : 0x1000); } }
        // TODO: Sprite size
        // TODO: PPU master / slave
        bool NMIEnabled { get { return ((PPUCTRL >> 7) & 1) == 1; } }

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
        }

        byte LookupBGColor(byte data)
        {
            int colorNum = data & 0x3;
            int paletteNum = (data >> 2) & 0x3;

            return memory.Read((ushort)(colorNum == 0 ? 0x3F00 : (0x3F01 + 4 * paletteNum + colorNum - 1)));
        }

        byte LookupSpriteColor(byte data)
        {
            int colorNum = data & 0x3;
            int paletteNum = (data >> 2) & 0x3;

            return memory.Read((ushort)(colorNum == 0 ? 0x3F00 : (0x3F11 + 4 * paletteNum + colorNum - 1)));
        }

        [Obsolete("Dummy Implementation")]
        public byte[] GetPixels()
        {
            byte[] pixels = new byte[256 * 240];

            // BG
            for (int y = 0; y < 240; y += 8)
            {
                for (int x = 0; x < 256; x += 8)
                {
                    byte tile  = memory.Read((ushort)(0x2000 + (256 / 8) * (y / 8) + (x / 8))); // FIXME: refer correct name table
                    byte attrs = memory.Read((ushort)(0x23C0 + (256 / 8) * (y / 8) + (x / 8))); // FIXME: refer correct attr table

                    PutTile(pixels, (byte)x, (byte)y, tile, attrs, true);
                }
            }

            // Sprite
            for (int i = 0; i < OAM.Length / 4; i += 4)
            {
                byte y     = OAM[i];
                byte tile  = OAM[i + 1];
                byte attrs = OAM[i + 2];
                byte x     = OAM[i + 3];

                PutTile(pixels, x, y, tile, attrs, false);
            }

            return pixels;
        }
        [Obsolete("Dummy Implementation")]
        public void PutTile(byte[] pixels, byte x, byte y, byte tile, byte attrs, bool bg)
        {
            bool flipX = ((attrs & 0x40) != 0);
            bool flipY = ((attrs & 0x80) != 0);

            for (int j = 0; j < 8; j++)
            {
                int yOffset = flipY ? 7 - j : j;
                ushort yAddr = (ushort)((bg ? BGPatternTableAddress : SpritePatternTableAddress) + tile * 16 + yOffset);

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

                    int pixelIndex = 256 * (y + j) + (x + k);
                    if (pixelIndex >= 0 && pixelIndex < pixels.Length)
                    {
                        pixels[pixelIndex] = (bg ? LookupBGColor(colorNum) : LookupSpriteColor(colorNum));
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
                        return data;
                    }
                case 0x2007:
                    {
                        byte data = memory.Read(v);
                        v += VRAMIncrement;
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
                case 0x2006:
                    t = w ? (ushort)((t & 0xFF00) | data) : (ushort)((t & 0x00FF) | (data << 8));
                    if (w) { v = t; }
                    w = !w;
                    break;
                case 0x2007:
                    memory.Write(v, data);
                    v += VRAMIncrement;
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
