using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu.Mappers
{
    public abstract class Mapper
    {
        protected Cartridge cartridge;

        public Mapper(Cartridge cartridge)
        {
            this.cartridge = cartridge;
        }

        public abstract byte ReadPRG(ushort addr);
        public abstract void WritePRG(ushort addr, byte data);

        public virtual byte ReadCHR(ushort addr)
        {
            return cartridge.CHR[addr];
        }

        public virtual ushort ConvertVRAMAddress(int addr)
        {
            if (cartridge.VerticalMirroring)
            {
                return (ushort)(addr < 0x2800 ? addr : addr - 0x0800);
            }
            else
            {
                return (ushort)((addr / 0x400) % 2 == 0 ? addr : addr - 0x400);
            }
        }
    }
}
