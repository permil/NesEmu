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
                    Debug.Assert(false, "Invalid PPU Memory Read: " + addr.ToString("x4"));
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
                    paletteRAM[(addr - 0x3F00) % paletteRAM.Length] = data;
                }
                else // Invalid Write
                {
                    Debug.Assert(false, "Invalid PPU Memory Write: " + addr.ToString("x4"));
                }
            }
        }

        public PPU(Console console)
        {
            this.console = console;
            memory = new Memory(console.Mapper);
        }

        byte LookupSpriteColor(byte data)
        {
            int colorNum = data & 0x3;
            int paletteNum = (data >> 2) & 0x3;

            return memory.Read((ushort)(colorNum == 0 ? 0x3F00 : 0x3F11 + 4 * paletteNum + colorNum - 1));
        }

        [Obsolete("Dummy Implementation")]
        public byte[] GetPixels()
        {
            byte[] pixels = new byte[256 * 240];
            for (int i = 0; i < OAM.Length / 4; i += 4)
            {
                int y     = OAM[i];
                int tile  = OAM[i + 1];
                int attrs = OAM[i + 2];
                int x     = OAM[i + 3];

                for (int yOffset = 0; yOffset < 8; yOffset++)
                {
                    ushort yAddr = (ushort)(0x1000 + tile * 16 + yOffset);

                    // https://wiki.nesdev.com/w/index.php/PPU_pattern_tables
                    byte[] pattern = new byte[2];
                    pattern[0] = memory.Read(yAddr);
                    pattern[1] = memory.Read((ushort)(yAddr + 8));
                    for (int xOffset = 0; xOffset < 8; xOffset++)
                    {
                        byte loBit = (byte)((pattern[0] >> (7 - xOffset)) & 1);
                        byte hiBit = (byte)((pattern[1] >> (7 - xOffset)) & 1);
                        byte colorNum = (byte)(((hiBit << 1) | loBit) & 0x03);
                        pixels[256 * (y + yOffset) + (x + xOffset)] = LookupSpriteColor(colorNum);
                    }
                }
            }

            return pixels;
        }

        public byte Read(ushort addr)
        {
            Debug.WriteLine("PPU Read - 0x" + addr.ToString("x4"));
            // TODO:
            switch (addr)
            {
                case 0x2002:
                    return 0xFF; // FIXME:
                default:
                    return 0; // TODO:
            }
        }

        public void Write(ushort addr, byte data)
        {
            Debug.WriteLine("PPU Write - 0x" + addr.ToString("x4") + " <- " + data);
            switch (addr)
            {
                case 0x2003:
                    OAMAddr = data;
                    break;
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
                    v += 1; // FIXME:
                    break;
                default:
                    // TODO:
                    break;
            }
        }
    }
}
