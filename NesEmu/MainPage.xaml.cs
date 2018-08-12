using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;
using System.Threading.Tasks;
using NesEmu.Mappers;

namespace NesEmu
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            Start();
        }

        async void Start()
        {
            Cartridge cartridge = await LoadNESFileAsync();
            Debug.WriteLine(cartridge.PRG.Length);
            Debug.WriteLine(cartridge.CHR.Length);
            Debug.WriteLine(cartridge.Mapper);

            Mapper mapper = null;
            switch (cartridge.Mapper)
            {
                case 0:
                    mapper = new NROMMapper(cartridge);
                    break;
            }

            if (mapper == null)
            {
                Debug.Assert(false, "mapper is not implemented yet: " + cartridge.Mapper);
                return;
            }

            Console console = new Console(mapper);


            int width = 256;
            int height = 240;
            WriteableBitmap bitmap = new WriteableBitmap(width, height);
            this.image.Source = bitmap;

//            while (true)
            for (int loop = 0; loop < 300; loop++) // FIXME:
            {
                console.Step();
            }

            /*----- FIXME: dummy implementation -----*/
            byte[] rawPixels = console.PPU.GetPixels();
            byte[] pixels = new byte[rawPixels.Length * 4];

            for (int i = 0; i < rawPixels.Length; i++)
            {
                pixels[i * 4]     = (byte)(rawPixels[i] * 64);
                pixels[i * 4 + 1] = (byte)(rawPixels[i] * 64);
                pixels[i * 4 + 2] = (byte)(rawPixels[i] * 64);
                pixels[i * 4 + 3] = 255;
            }

            using (var pixelStream = bitmap.PixelBuffer.AsStream())
            {
                pixelStream.Seek(0, SeekOrigin.Begin);
                pixelStream.Write(pixels, 0, pixels.Length);
            }

            bitmap.Invalidate(); // Redraw the WriteableBitmap
            /*----- FIXME: dummy implementation -----*/
        }

        async Task<Cartridge> LoadNESFileAsync()
        {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
            filePicker.FileTypeFilter.Add(".nes");
            var file = await filePicker.PickSingleFileAsync();

            return await Cartridge.LoadFromNES(file);
        }
    }
}