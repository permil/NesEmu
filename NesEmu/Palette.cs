using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI;

namespace NesEmu
{
    public class Palette
    {
        public Color[] Colors { get; }  = new Color[0x40];

        public Palette()
        {
            Colors[0x0] = Color.FromArgb(255, 84, 84, 84);
            Colors[0x1] = Color.FromArgb(255, 0, 30, 116);
            Colors[0x2] = Color.FromArgb(255, 8, 16, 144);
            Colors[0x3] = Color.FromArgb(255, 48, 0, 136);
            Colors[0x4] = Color.FromArgb(255, 68, 0, 100);
            Colors[0x5] = Color.FromArgb(255, 92, 0, 48);
            Colors[0x6] = Color.FromArgb(255, 84, 4, 0);
            Colors[0x7] = Color.FromArgb(255, 60, 24, 0);
            Colors[0x8] = Color.FromArgb(255, 32, 42, 0);
            Colors[0x9] = Color.FromArgb(255, 8, 58, 0);
            Colors[0xa] = Color.FromArgb(255, 0, 64, 0);
            Colors[0xb] = Color.FromArgb(255, 0, 60, 0);
            Colors[0xc] = Color.FromArgb(255, 0, 50, 60);
            Colors[0xd] = Color.FromArgb(255, 0, 0, 0);
            Colors[0xe] = Color.FromArgb(255, 0, 0, 0);
            Colors[0xf] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x10] = Color.FromArgb(255, 152, 150, 152);
            Colors[0x11] = Color.FromArgb(255, 8, 76, 196);
            Colors[0x12] = Color.FromArgb(255, 48, 50, 236);
            Colors[0x13] = Color.FromArgb(255, 92, 30, 228);
            Colors[0x14] = Color.FromArgb(255, 136, 20, 176);
            Colors[0x15] = Color.FromArgb(255, 160, 20, 100);
            Colors[0x16] = Color.FromArgb(255, 152, 34, 32);
            Colors[0x17] = Color.FromArgb(255, 120, 60, 0);
            Colors[0x18] = Color.FromArgb(255, 84, 90, 0);
            Colors[0x19] = Color.FromArgb(255, 40, 114, 0);
            Colors[0x1a] = Color.FromArgb(255, 8, 124, 0);
            Colors[0x1b] = Color.FromArgb(255, 0, 118, 40);
            Colors[0x1c] = Color.FromArgb(255, 0, 102, 120);
            Colors[0x1d] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x1e] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x1f] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x20] = Color.FromArgb(255, 236, 238, 236);
            Colors[0x21] = Color.FromArgb(255, 76, 154, 236);
            Colors[0x22] = Color.FromArgb(255, 120, 124, 236);
            Colors[0x23] = Color.FromArgb(255, 176, 98, 236);
            Colors[0x24] = Color.FromArgb(255, 228, 84, 236);
            Colors[0x25] = Color.FromArgb(255, 236, 88, 180);
            Colors[0x26] = Color.FromArgb(255, 236, 106, 100);
            Colors[0x27] = Color.FromArgb(255, 212, 136, 32);
            Colors[0x28] = Color.FromArgb(255, 160, 170, 0);
            Colors[0x29] = Color.FromArgb(255, 116, 196, 0);
            Colors[0x2a] = Color.FromArgb(255, 76, 208, 32);
            Colors[0x2b] = Color.FromArgb(255, 56, 204, 108);
            Colors[0x2c] = Color.FromArgb(255, 56, 180, 204);
            Colors[0x2d] = Color.FromArgb(255, 60, 60, 60);
            Colors[0x2e] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x2f] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x30] = Color.FromArgb(255, 236, 238, 236);
            Colors[0x31] = Color.FromArgb(255, 168, 204, 236);
            Colors[0x32] = Color.FromArgb(255, 188, 188, 236);
            Colors[0x33] = Color.FromArgb(255, 212, 178, 236);
            Colors[0x34] = Color.FromArgb(255, 236, 174, 236);
            Colors[0x35] = Color.FromArgb(255, 236, 174, 212);
            Colors[0x36] = Color.FromArgb(255, 236, 180, 176);
            Colors[0x37] = Color.FromArgb(255, 228, 196, 144);
            Colors[0x38] = Color.FromArgb(255, 204, 210, 120);
            Colors[0x39] = Color.FromArgb(255, 180, 222, 120);
            Colors[0x3a] = Color.FromArgb(255, 168, 226, 144);
            Colors[0x3b] = Color.FromArgb(255, 152, 226, 180);
            Colors[0x3c] = Color.FromArgb(255, 160, 214, 228);
            Colors[0x3d] = Color.FromArgb(255, 160, 162, 160);
            Colors[0x3e] = Color.FromArgb(255, 0, 0, 0);
            Colors[0x3f] = Color.FromArgb(255, 0, 0, 0);
        }
    }
}
