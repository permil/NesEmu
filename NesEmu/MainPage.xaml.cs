using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Threading.Tasks;

// 空白ページのアイテム テンプレートについては、http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 を参照してください

namespace NesEmu
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();


            // BitmapImageの準備
            int width = 240;
            int height = 240;
            WriteableBitmap bitmap = new WriteableBitmap(width, height);
            this.image.Source = bitmap;

            // 計算用のバイト列の準備
            int pixelsSize = (int)(width * height * 4);
            byte[] pixels = new byte[pixelsSize];

            // バイト列に色情報を入れる
            byte value = 0;
            for (int x = 0; x < width * height * 4; x = x + 4)
            {
                byte blue = value;
                byte green = value;
                byte red = value;
                byte alpha = 255;
                pixels[x] = blue;
                pixels[x + 1] = green;
                pixels[x + 2] = red;
                pixels[x + 3] = alpha;
                value = (byte)((value + 1) % 240);
            }

            // バイト列をBitmapImageに変換する
            using (var pixelStream = bitmap.PixelBuffer.AsStream())
            {
                pixelStream.Seek(0, SeekOrigin.Begin);
                pixelStream.Write(pixels, 0, pixels.Length);
            }

            // Redraw the WriteableBitmap
            bitmap.Invalidate();

            Start();
        }

        private async void Start()
        {
            Cartridge cartridge = await LoadNESFileAsync();
            Debug.WriteLine(cartridge.PRG.Length);
            Debug.WriteLine(cartridge.CHR.Length);
            Debug.WriteLine(cartridge.Mapper);
        }

        private async Task<Cartridge> LoadNESFileAsync()
        {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
            filePicker.FileTypeFilter.Add(".nes");
            var file = await filePicker.PickSingleFileAsync();

            return await Cartridge.LoadFromNES(file);
        }
    }
}