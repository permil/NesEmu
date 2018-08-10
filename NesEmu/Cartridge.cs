using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;

namespace NesEmu
{
    class Cartridge
    {
        public byte[] PRG;
        public byte[] CHR; // includes the case of CHRRAM
        bool UsesCHRRAM { get; set; }

        // https://wiki.nesdev.com/w/index.php/INES#iNES_file_format

        // 76543210
        // ||||||||
        // |||||||+-Mirroring: 0: horizontal(vertical arrangement)(CIRAM A10 = PPU A11)
        // |||||||             1: vertical(horizontal arrangement)(CIRAM A10 = PPU A10)
        // ||||||+--1: Cartridge contains battery - backed PRG RAM ($6000 - 7FFF) or other persistent memory
        // |||||+---1: 512 - byte trainer at $7000 -$71FF(stored before PRG data)
        // ||||+----1: Ignore mirroring control or above mirroring bit; instead provide four - screen VRAM
        // ++++---- - Lower nybble of mapper number
        byte flag6;
        public bool VerticalMirroring { get { return (flag6 & 0x01) != 0; } }
        public bool HasBattery        { get { return (flag6 & 0x02) != 0; } }
        bool ContainsTrainer          { get { return (flag6 & 0x04) != 0; } }

        // 76543210
        // ||||||||
        // |||||||+-VS Unisystem
        // ||||||+--PlayChoice - 10(8KB of Hint Screen data stored after CHR data)
        // ||||++---If equal to 2, flags 8 - 15 are in NES 2.0 format
        // ++++---- - Upper nybble of mapper number
        byte flag7;
        public int Mapper { get { return flag7 & 0xF0 | (flag6 >> 4 & 0xF); } }

        public static async Task<Cartridge> LoadFromNES(StorageFile file)
        {
            Cartridge cartridge;
            using (Stream stream = (await file.OpenReadAsync()).AsStreamForRead())
            {
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    cartridge = Create(reader);
                }
            }

            return cartridge;
        }

        Cartridge() { }
        static Cartridge Create(BinaryReader reader)
        {
            if (reader.ReadUInt32() != 0x1A53454E)
            {
                return null; // validation error
            }

            Cartridge cartridge = new Cartridge();

            // read header
            int PRGROMSize = reader.ReadByte(); // 16KB * size
            int CHRROMSize = reader.ReadByte(); // 8KB * size, 0 means CHR RAM (unimplemented)
            cartridge.UsesCHRRAM = (CHRROMSize == 0);
            cartridge.flag6 = reader.ReadByte();
            cartridge.flag7 = reader.ReadByte();
            int PRGRAMSize = Math.Min((int)reader.ReadByte(), 1); // 8KB * size, 0 infers 8 KB for compatibility
            byte flag9 = reader.ReadByte();
            byte flag10 = reader.ReadByte();
            reader.ReadBytes(5); // 11-15: Zero filled

            // read trainer (ignore)
            if (cartridge.ContainsTrainer) { reader.ReadBytes(512); }

            // read PRGROM
            cartridge.PRG = new byte[PRGROMSize * 16384];
            reader.Read(cartridge.PRG, 0, cartridge.PRG.Length);

            // read CHRROM or init CHRRAM
            if (cartridge.UsesCHRRAM)
            {
                cartridge.CHR = new byte[8192];
            }
            else
            {
                cartridge.CHR = new byte[CHRROMSize * 8192];
                reader.Read(cartridge.CHR, 0, cartridge.CHR.Length);
            }

            return cartridge;
        }
    }
}
