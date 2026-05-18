using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: IconGen <input.png> <output.ico>");
    return 1;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

using var source = Image.FromFile(inputPath);

var cropSize = Math.Min(source.Width, source.Height);
using var square = new Bitmap(cropSize, cropSize, PixelFormat.Format32bppArgb);
using (var g = Graphics.FromImage(square))
{
    g.Clear(Color.Transparent);
    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
    g.SmoothingMode = SmoothingMode.HighQuality;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.DrawImage(source, new Rectangle(0, 0, cropSize, cropSize), new Rectangle(0, 0, cropSize, cropSize), GraphicsUnit.Pixel);
}

var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
WritePngIcon(square, outputPath, sizes);
Console.WriteLine($"Created {outputPath} ({new FileInfo(outputPath).Length} bytes)");
return 0;

static void WritePngIcon(Bitmap source, string path, int[] sizes)
{
    using var fs = File.Create(path);
    using var writer = new BinaryWriter(fs);

    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)sizes.Length);

    var imageData = new List<byte[]>();
    foreach (var size in sizes)
    {
        using var resized = new Bitmap(source, size, size);
        using var ms = new MemoryStream();
        resized.Save(ms, ImageFormat.Png);
        imageData.Add(ms.ToArray());
    }

    var offset = 6 + 16 * sizes.Length;
    foreach (var (size, data) in sizes.Zip(imageData))
    {
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(data.Length);
        writer.Write(offset);
        offset += data.Length;
    }

    foreach (var data in imageData)
        writer.Write(data);
}
