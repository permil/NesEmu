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
        public Mapper Mapper { get; private set; }

        public Console(Mapper mapper)
        {
            Mapper = mapper;
            CPU = new CPU(this);
        }

        public void Step()
        {
            CPU.Step();
        }
    }
}
