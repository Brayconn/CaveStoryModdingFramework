using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace CaveStoryModdingFramework.Compatability
{
    /// <summary>
    /// All the pixel formats used by the 3DS. Only three are used by Cave Story eShop: RGBA8 (0), RGB8 (1), and A8 (8)
    /// </summary>
    /// <see cref="http://www.3dbrew.org/wiki/CGFX#TXOB"/>
    public enum _3DSPixelFormats : byte
    {
        RGBA8 = 0,
        RGB8,
        RGBA5551,
        RGB565,
        RGBA4,
        LA8,
        HILO8,
        L8,
        A8,
        LA4,
        L4,
        //the original docs were unsure of this one
        A4,
        ETC1,
        //also this one
        ETC1A4,
    }
    //TODO this class is slow. Manual bit/byte manipulation may improve things???
    [DebuggerDisplay("{Width}x{Height} [{TextureWidth}x{TextureHeight}] - {PixelFormat}")]
    public class _3DSTex : IDisposable
    {
        public const int HeaderSize = 0x80;
        private static readonly byte[] TileOrder = new byte[64]
        {
            0,  1,  8,  9,  2,  3,  10, 11, 16, 17, 24, 25, 18, 19, 26, 27,
            4,  5,  12, 13, 6,  7,  14, 15, 20, 21, 28, 29, 22, 23, 30, 31,
            32, 33, 40, 41, 34, 35, 42, 43, 48, 49, 56, 57, 50, 51, 58, 59,
            36, 37, 44, 45, 38, 39, 46, 47, 52, 53, 60, 61, 54, 55, 62, 63,
        };

        public _3DSPixelFormats PixelFormat { get; set; }
        public byte Unknown1 { get; set; }
        public ushort Width { get; set; }
        public ushort Height { get; set; }
        public ushort TextureWidth { get; set; }
        public ushort TextureHeight { get; set; }
        public ushort Unknown2 { get; set; }
        public Color DefaultColor { get; set; }
        public byte[] Unknown3 { get; set; }
        public Bitmap Bitmap { get; set; }

        //TODO add constructor that creates a new .tex file from a bitmap object

        //TODO I would really like to put a constructor that takes a string/path argument
        //but I can't reuse the existing constructor because I can't dispose the stream...

        //Adapted from/expanded on untex.cpp by Alula
        public _3DSTex(Stream s, bool crop = true, bool flipY = true)
        {
            using var br = new BinaryReader(s, Encoding.Default, true);
            
            //reading header
            var pixelFormat = br.ReadByte();
            if(!Enum.IsDefined(typeof(_3DSPixelFormats), pixelFormat))
                throw new ArgumentOutOfRangeException(nameof(pixelFormat));
            PixelFormat = (_3DSPixelFormats)pixelFormat;

            Unknown1 = br.ReadByte();

            Width = br.ReadUInt16();
            Height = br.ReadUInt16();
            TextureWidth = br.ReadUInt16();
            TextureHeight = br.ReadUInt16();

            Unknown2 = br.ReadUInt16();

            {
                var b = br.ReadByte();
                var g = br.ReadByte();
                var r = br.ReadByte();
                var a = br.ReadByte();
                DefaultColor = Color.FromArgb(a,r,g,b);
            }
            Unknown3 = br.ReadBytes(0x70);
            Debug.Assert(br.BaseStream.Position == HeaderSize, "Failed to read full header");

            //processing the data
            Bitmap buffer = new Bitmap(TextureWidth, TextureHeight);
            for(int y = 0; y < TextureHeight; y += 8)
            {
                for(int x = 0; x < TextureWidth; x += 8)
                {
                    for(int i = 0; i < TileOrder.Length; i++)
                    {
                        var tx = x + (TileOrder[i] % 8);
                        var ty = y + (TileOrder[i] / 8);
                        if (flipY)
                            ty = buffer.Height - 1 - ty;
                        Debug.Assert(0 <= tx && tx < buffer.Width,  "X out of range!");
                        Debug.Assert(0 <= ty && ty < buffer.Height, "Y out of range!");
                        Color color;
                        byte r, g, b, a;
                        switch (PixelFormat)
                        {
                            case _3DSPixelFormats.RGBA8:
                                a = br.ReadByte();
                                b = br.ReadByte();
                                g = br.ReadByte();
                                r = br.ReadByte();
                                color = Color.FromArgb(a, r, g, b);
                                break;
                            case _3DSPixelFormats.RGB8:
                                b = br.ReadByte();
                                g = br.ReadByte();
                                r = br.ReadByte();
                                color = Color.FromArgb(r,g,b);
                                break;
                            case _3DSPixelFormats.A8:
                                color = Color.FromArgb(br.ReadByte(), DefaultColor);
                                break;
                            default:
                                throw new NotSupportedException(nameof(PixelFormat));
                        }

                        buffer.SetPixel(tx, ty, color);
                    }
                }
            }
            if(crop && (Width != TextureWidth || Height != TextureHeight))
            {
                int x = 0;
                int y = flipY ? 0 : TextureHeight - Height;
                Bitmap = buffer.Clone(new Rectangle(x, y, Width, Height), buffer.PixelFormat);
                buffer.Dispose();
            }
            else
            {
                Bitmap = buffer;
            }
        }

        const ushort LastPowerOf2 = 0x8000;
        static ushort RoundUpToPowerOf2(ushort x)
        {
#if NET6_0_OR_GREATER
            return (ushort)System.Numerics.BitOperations.RoundUpToPowerOf2(x);
#else
            x--;
            x |= (ushort)(x >> 1);
            x |= (ushort)(x >> 2);
            x |= (ushort)(x >> 4);
            x |= (ushort)(x >> 8);
            x++;
            return x;
#endif
        }
        public void UpdateSize()
        {
            if (Bitmap.Width > LastPowerOf2)
                throw new ArgumentOutOfRangeException(nameof(Bitmap.Width));
            Width = (ushort)Bitmap.Width;
            if (Bitmap.Height > LastPowerOf2)
                throw new ArgumentOutOfRangeException(nameof(Bitmap.Height));
            Height = (ushort)Bitmap.Height;

            TextureWidth = RoundUpToPowerOf2(Width);
            TextureHeight = RoundUpToPowerOf2(Height);
        }

        public void Save(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                Save(fs);
        }
        public void Save(Stream stream, bool flipY = true)
        {
            using var bw = new BinaryWriter(stream, Encoding.Default, true);

            bw.Write((byte)PixelFormat);
            bw.Write(Unknown1);
            bw.Write(Width);
            bw.Write(Height);
            bw.Write(TextureWidth);
            bw.Write(TextureHeight);
            bw.Write(Unknown2);
            bw.Write(DefaultColor.ToArgb());
            bw.Write(Unknown3);

            //originally, a temporary Bitmap object was created that was TextureWidth x TextureHeight in size
            //and then THAT was used for all the writing
            //but that caused this issue to occur: https://stackoverflow.com/questions/66104555/why-does-graphics-drawimage-change-the-colors-no-resizing-used
            //so instead, this just uses math(tm) to adjust where pixels should be written
            int bytesPerPixel;
            switch (PixelFormat)
            {
                case _3DSPixelFormats.RGBA8:
                    bytesPerPixel = 4;
                    break;
                case _3DSPixelFormats.RGB8:
                    bytesPerPixel = 3;
                    break;
                case _3DSPixelFormats.A8:
                    bytesPerPixel = 1;
                    break;
                default:
                    throw new NotSupportedException(nameof(PixelFormat));
            }
            int emptyRows = TextureHeight - Bitmap.Height;
            int pixelOffset = emptyRows & 7; //how many pixels are in the last row?
            emptyRows &= ~7; //round down to nearest multiple of 8 (since the loop working in 8x8 tiles)
            emptyRows *= TextureWidth * bytesPerPixel;
            bw.BaseStream.Position += emptyRows;
            for(int y = 0; y < TextureHeight; y += 8)
            {
                for(int x = 0; x < TextureWidth; x += 8)
                {
                    for(int i = 0; i < TileOrder.Length; i++)
                    {
                        var tx = x + (TileOrder[i] % 8);
                        var ty = y + (TileOrder[i] / 8) - pixelOffset;
                        if (flipY)
                            ty = Bitmap.Height - 1 - ty;
                        Color color;
                        if (0 <= tx && tx < Bitmap.Width && 0 <= ty && ty < Bitmap.Height)
                            color = Bitmap.GetPixel(tx, ty);
                        else
                            color = Color.Empty;
                        //Debug.Assert(0 <= tx && tx < buffer.Width, "X out of range!");
                        //Debug.Assert(0 <= ty && ty < buffer.Height, "Y out of range!");
                        switch (PixelFormat)
                        {
                            case _3DSPixelFormats.RGBA8:
                                bw.Write(color.A);
                                bw.Write(color.B);
                                bw.Write(color.G);
                                bw.Write(color.R);
                                break;
                            case _3DSPixelFormats.RGB8:
                                bw.Write(color.B);
                                bw.Write(color.G);
                                bw.Write(color.R);
                                break;
                            case _3DSPixelFormats.A8:
                                bw.Write(color.A);
                                break;
                            default:
                                throw new NotSupportedException(nameof(PixelFormat));
                        }
                    }
                }
            }
            //The above loop may not write enough bytes to fill out the file
            //Using SetLength() ensures that the file is padded to the right length with 0's
            bw.BaseStream.SetLength(HeaderSize + (TextureWidth * TextureHeight * bytesPerPixel));
        }

        public void Dispose()
        {
            Bitmap.Dispose();
        }
    }
}
