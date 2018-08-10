using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu.Mappers
{
    public abstract class Mapper
    {
        public abstract byte Read(ushort addr);
        public abstract void Write(ushort addr, byte data);

        protected Cartridge cartridge;

        public Mapper(Cartridge cartridge)
        {
            this.cartridge = cartridge;
        }
    }
}
