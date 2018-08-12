using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu.Mappers
{
    public class NROMMapper : Mapper
    {
        public NROMMapper(Cartridge cartridge) : base(cartridge) { }

        public override byte ReadPRG(ushort addr)
        {
            return cartridge.PRG[addr % cartridge.PRG.Length];
        }

        public override void WritePRG(ushort addr, byte data)
        {
            throw new NotImplementedException();
        }
    }
}
