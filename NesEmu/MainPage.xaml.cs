using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using System.Diagnostics;
using System.Threading.Tasks;
using NesEmu.Mappers;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Input;
using Windows.UI.Core;
using Windows.System;

namespace NesEmu
{
    public sealed partial class MainPage : Page
    {
        Console console;

        public MainPage()
        {
            this.InitializeComponent();
            Window.Current.CoreWindow.KeyDown += OnKeyDown;
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

            console = new Console(mapper);


            Palette palette = new Palette();
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
                Color color = palette.Colors[rawPixels[i]];
                pixels[i * 4]     = color.B;
                pixels[i * 4 + 1] = color.G;
                pixels[i * 4 + 2] = color.R;
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

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.VirtualKey)
            {
                case VirtualKey.Z:      console.Controller.SetState(Controller.Button.A, true);         break;
                case VirtualKey.X:      console.Controller.SetState(Controller.Button.B, true);         break;
                case VirtualKey.Q:      console.Controller.SetState(Controller.Button.Select, true);    break;
                case VirtualKey.W:      console.Controller.SetState(Controller.Button.Start, true);     break;
                case VirtualKey.Up:     console.Controller.SetState(Controller.Button.Up, true);        break;
                case VirtualKey.Down:   console.Controller.SetState(Controller.Button.Down, true);      break;
                case VirtualKey.Left:   console.Controller.SetState(Controller.Button.Left, true);      break;
                case VirtualKey.Right:  console.Controller.SetState(Controller.Button.Right, true);     break;
            }
        }
    }
}