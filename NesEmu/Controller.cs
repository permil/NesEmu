using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu
{
    public class Controller
    {
        public enum Button { A = 0, B, Select, Start, Up, Down, Left, Right }

        bool[] states = new bool[Enum.GetValues(typeof(Button)).Length];
        int currentIndex = 0;
        bool strobe; // https://wiki.nesdev.com/w/index.php/Standard_controller#Input_.28.244016_write.29

        public void SetState(Button button, bool state)
        {
            states[(int)button] = state;
            currentIndex = 0;
        }

        public byte ReadState()
        {
            if (currentIndex >= states.Length)
            {
                return 1;
            }

            bool state = states[currentIndex];
            if (!strobe)
            {
                currentIndex++;
            }
            return (byte)(state ? 1 : 0);
        }

        public void WriteState(byte input)
        {
            strobe = ((input & 1) == 1);
            if (strobe)
            {
                currentIndex = 0;
            }
        }
    }
}