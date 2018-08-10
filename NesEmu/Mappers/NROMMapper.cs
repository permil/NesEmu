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

        public override byte Read(ushort addr)
        {
            if (addr >= 0x8000)
            {
                return cartridge.PRG[(addr - 0x8000) % cartridge.PRG.Length];
            }
            else
            {
                Debug.Assert(false, "not implemented yet");
                return 0;
            }
        }

        public override void Write(ushort addr, byte data)
        {
            throw new NotImplementedException();
        }
    }
}
