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

        class VRAMAddressRegister
        {
            public ushort Value { get; set; }

            // for $2005
            public byte NameTableSelect { private get { return (byte)((Value >> 10) & 0b11); } set { Value = (ushort)((Value & ~(0b11 << 10)) | (value << 10)); } }
            public ushort BaseNameTableAddress { get { return (ushort)(0x2000 + NameTableSelect * 0x0400); } }
            public ushort CoarseXScroll { get { return (ushort)(Value & 0b11111); } set { Value = (ushort)((Value >> 5 << 5) | value); } }
            public ushort CoarseYScroll { get { return (ushort)((Value >> 5) & 0b11111); } set { Value = (ushort)((Value & ~(0b11111 << 5)) | ((value & 0b11111) << 5)); } }
            public byte FineYScroll { get { return (byte)((Value >> 12) & 0b111); } set { Value = (ushort)((Value & ~(0b111 << 12)) | ((value & 0b111) << 12)); } }
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

        // $2001
        // 7  bit  0
        // ---- ----
        // BGRs bMmG
        // |||| ||||
        // |||| |||+- Greyscale(0: normal color, 1: produce a greyscale display)
        // |||| ||+-- 1: Show background in leftmost 8 pixels of screen, 0: Hide
        // |||| |+--- 1: Show sprites in leftmost 8 pixels of screen, 0: Hide
        // |||| +---- 1: Show background
        // |||+------ 1: Show sprites
        // ||+------- Emphasize red*
        // |+-------- Emphasize green*
        // +--------- Emphasize blue*
        byte PPUMASK
        {
            set
            {
                showBG = (((value >> 3) & 0b1) == 1);
                showSprites = (((value >> 4) & 0b1) == 1);
            }
        }
        bool showBG, showSprites;

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

        ulong tileShiftReg;
        byte nameTableByte, attributeTableByte, tileBitfieldLo, tileBitfieldHi;
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

            bool renderingEnabled = showSprites || showBG;
            if (renderingEnabled)
            {
                if (cycle == 257)
                {
                    // TODO: eval sprites
                }
                if (cycle > 0 && cycle <= 256 && scanline >= 0 && scanline < 240)
                {
                    RenderPixel();
                }

                if ((scanline >= 0 && scanline < 240) || scanline == 261) // prerender or render scanline
                {
                    if (((cycle > 0 && cycle <= 256) || (cycle >= 321 && cycle <= 336)))
                    {
                        tileShiftReg >>= 4;
                        switch (cycle % 8)
                        {
                            case 1: nameTableByte      = memory.Read((ushort)(0x2000 | (v.Value & 0x0FFF)));    break;
                            case 3: attributeTableByte = memory.Read((ushort)(0x23C0 | (v.Value & 0x0C00) | ((v.CoarseYScroll >> 2) << 3) | (v.CoarseXScroll >> 2)));   break;
                            case 5: tileBitfieldLo     = memory.Read((ushort)(BGPatternTableAddress + (nameTableByte * 16) + v.FineYScroll));       break;
                            case 7: tileBitfieldHi     = memory.Read((ushort)(BGPatternTableAddress + (nameTableByte * 16) + v.FineYScroll + 8));   break;
                            case 0:
                                {
                                    byte palette = (byte)((attributeTableByte >> ((v.CoarseXScroll & 0x2) | ((v.CoarseYScroll & 0x2) << 1))) & 0x3);

                                    ulong data = 0; // Upper 32 bits to add to tileShiftReg
                                    for (int i = 0; i < 8; i++)
                                    {
                                        // Get color number
                                        byte loColorBit = (byte)((tileBitfieldLo >> (7 - i)) & 1);
                                        byte hiColorBit = (byte)((tileBitfieldHi >> (7 - i)) & 1);
                                        byte colorNum = (byte)((hiColorBit << 1) | (loColorBit) & 0x03);

                                        // Add palette number
                                        byte fullPixelData = (byte)(((palette << 2) | colorNum) & 0xF);

                                        data |= (uint)(fullPixelData << (4 * i));
                                    }

                                    tileShiftReg &= 0xFFFFFFFF;
                                    tileShiftReg |= (data << 32);

                                    // Coarse X increment: https://wiki.nesdev.com/w/index.php/PPU_scrolling#Coarse_X_increment
                                    v.CoarseXScroll = (ushort)((v.CoarseXScroll + 1) % 32);
                                    if (v.CoarseXScroll == 0) { /* TODO: switch horizontal nametable */ }

                                    if (cycle == 256) // Y increment: https://wiki.nesdev.com/w/index.php/PPU_scrolling#Y_increment
                                    {
                                        if (v.FineYScroll != 7)
                                        {
                                            v.FineYScroll++; // increment Fine Y
                                        }
                                        else
                                        {
                                            v.FineYScroll = 0; // reset Fine Y
                                            switch (v.CoarseYScroll)
                                            {
                                                case 29:
                                                    v.CoarseYScroll = 0;
                                                    // TODO: switch vertical nametable
                                                    break;
                                                case 31:
                                                    v.CoarseYScroll = 0;
                                                    break;
                                                default:
                                                    v.CoarseYScroll = (ushort)((v.CoarseYScroll + 1) % 32); // increment Coarse Y
                                                    break;
                                            }
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    if (cycle > 257 && cycle <= 320)
                    {
                        OAMAddr = 0;
                    }
                    if (cycle == 257)
                    {
                        // copy horizontal position data from t to v
                        v.CoarseXScroll = t.CoarseXScroll;
                        // TODO: copy nametable select
                    }
                }
                if (cycle >= 280 && cycle <= 304 && scanline == 261)
                {
                    // copy vertical position data from t to v
                    v.CoarseYScroll = t.CoarseYScroll;
                    v.FineYScroll = t.FineYScroll;
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
        byte[] pixels = new byte[(WIDTH * 8) * (HEIGHT * 8)];
        [Obsolete("Dummy Implementation")]
        public byte[] GetPixels()
        {
            return pixels;
        }

        void RenderPixel()
        {
            // BG
            if (showBG)
            {
                byte bgPixelData = (byte)((tileShiftReg >> (FineXScroll * 4)) & 0b1111);
                byte colorNum = (byte)(bgPixelData & 0b11);
                byte paletteNum = (byte)((bgPixelData >> 2) & 0b11);
                pixels[scanline * (WIDTH * 8) + (cycle - 1)] = LookupBGColor(paletteNum, colorNum);
            }

            // FIXME: Sprite
            if (showSprites)
            {
                for (int i = 0; i < OAM.Length / 4; i += 4)
                {
                    byte y = OAM[i];
                    byte tile = OAM[i + 1];
                    byte attrs = OAM[i + 2];
                    byte x = OAM[i + 3];

                    byte paletteNum = 0;
                    bool flipX = ((attrs & 0x40) != 0);
                    bool flipY = ((attrs & 0x80) != 0);

                    PutTile(pixels, x, y, tile, paletteNum, flipX, flipY);
                }
            }
        }
        [Obsolete("Dummy Implementation")]
        public void PutTile(byte[] pixels, byte x, byte y, byte tile, byte paletteNum, bool flipX, bool flipY)
        {
            for (int j = 0; j < 8; j++)
            {
                int yOffset = flipY ? 7 - j : j;
                ushort yAddr = (ushort)(spritePatternTableAddress + tile * 16 + yOffset);

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

                    if (colorNum != 0)
                    {
                        int pixelIndex = (WIDTH * 8) * ((y + j) % (HEIGHT * 8)) + ((x + k) % (WIDTH * 8));
                        pixels[pixelIndex] = LookupSpriteColor(paletteNum, colorNum);
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
                case 0x2001:    PPUMASK = data;     break;
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
