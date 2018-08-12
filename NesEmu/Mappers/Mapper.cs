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
    }
}
