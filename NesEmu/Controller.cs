using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NesEmu
{
    public class Controller
    {
        bool[] states = new bool[Enum.GetValues(typeof(Button)).Length];
        public enum Button { A = 0, B, Select, Start, Up, Down, Left, Right }

        public bool GetState(Button button)
        {
            return states[(int)button];
        }

        public void SetState(Button button, bool state)
        {
            states[(int)button] = state;
        }
    }
}
