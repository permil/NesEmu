using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NesEmu.Mappers;

namespace NesEmu
{
    public class Console
    {
        public CPU CPU { get; private set; }
        public PPU PPU { get; private set; }
        public Controller Controller { get; private set; }
        public Mapper Mapper { get; private set; }

        public Console(Mapper mapper)
        {
            Mapper = mapper;
            CPU = new CPU(this);
            PPU = new PPU(this);
            Controller = new Controller();
        }

        public void Step()
        {
            CPU.Step();
        }
    }
}
