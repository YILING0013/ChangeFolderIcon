using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChangeFolderIcon.Models
{
    public class IconInfo
    {
        public string Name { get; set; } = "";
        public string FullPath { get; set; } = "";
        public BitmapImage IconSource { get; set; } = new BitmapImage();

        public static IconInfo FromPath(string path)
        {
            return new IconInfo
            {
                Name = System.IO.Path.GetFileNameWithoutExtension(path),
                FullPath = path,
                IconSource = new BitmapImage(new Uri(path))
            };
        }
    }
}
