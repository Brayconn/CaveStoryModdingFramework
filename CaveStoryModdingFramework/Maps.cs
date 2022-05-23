using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PixelModdingFramework;

namespace CaveStoryModdingFramework.Maps
{
    public enum ResizeModes
    {
        Buffer,
        Logical
    }
    public class Map : IMap<short, List<byte?>, byte?>
    {
        public const string DefaultExtension = "pxm";

        public event EventHandler MapResized;
        public event EventHandler MapResizing;

        private void NotifyMapResized()
        {
            MapResized?.Invoke(this, new EventArgs());
        }
        private void NotifyMapResizing()
        {
            MapResizing?.Invoke(this, new EventArgs());
        }

        public short Width { get; private set; }
        public short Height { get; private set; }
        public List<byte?> Tiles { get; set; }

        public int CurrentBufferSize => Tiles.Count;
        public int PrefferedBufferSize => Width * Height;

        public Map(short w, short h, byte? initialValue = null)
        {
            Width = w;
            Height = h;
            Tiles = new List<byte?>(new byte?[w * h]);
            if (initialValue != null)
                for (int i = 0; i < Tiles.Count; i++)
                    Tiles[i] = initialValue;
        }

        public static readonly byte[] DefaultHeader = new byte[] { (byte)'P', (byte)'X', (byte)'M' };
        public static readonly byte[] DefaultPostHeader = new byte[] { 0x10 };

        public Map(string path) : this(path, DefaultHeader, DefaultPostHeader) { }
        public Map(string path, byte[] header, byte[] postHeader)
        {
            using (var br = new BinaryReader(new FileStream(path, FileMode.Open, FileAccess.Read)))
            {
                var actual = br.ReadBytes(header.Length);
                if(!actual.SequenceEqual(header))
                    throw new FileLoadException("Invalid header"); //TODO add error message
                _ = br.ReadBytes(postHeader.Length);
                //TODO maybe put some kind of a warning if the post header is wrong?
                //actually should also put a warning when loading layers mode maps that change the post header
                Width = br.ReadInt16();
                Height = br.ReadInt16();
                Tiles = new List<byte?>(Width * Height);
                while (br.BaseStream.Position < br.BaseStream.Length)
                    Tiles.Add(br.ReadByte());
            }
        }

        public void Resize(short width, short height, ResizeModes mode, byte? newVal = 0, bool shrinkBuffer = true)
        {
            NotifyMapResizing();
            int newBufferLength;
            switch (mode)
            {
                case ResizeModes.Buffer:
                    newBufferLength = width * height;
                    if (CurrentBufferSize != newBufferLength)
                    {
                        if (CurrentBufferSize < newBufferLength)
                        {
                            for (int i = CurrentBufferSize; i < newBufferLength; i++)
                                Tiles.Add(newVal);
                        }
                        else if (shrinkBuffer)
                        {
                            Tiles.RemoveRange(newBufferLength, CurrentBufferSize - newBufferLength);
                        }
                    }
                    break;
                case ResizeModes.Logical:
                    if (width != Width)
                    {
                        if (width < Width)
                        {
                            for (int row = 0; row < Height; row++)
                                Tiles.RemoveRange((row * width) + width, Width - width);
                        }
                        else
                        {
                            var diff = new byte?[width - Width];
                            for (int i = 0; i < diff.Length; i++)
                                diff[i] = newVal;
                            for (int row = 0; row < Height; row++)
                                Tiles.InsertRange((row * width) + Width, diff);
                        }
                    }
                    if (height != Height)
                    {
                        newBufferLength = width * height;
                        //any bytes after this point were not visible before this resize (or don't exist yet)
                        var hiddenStart = width * Height;
                        //any bytes after this point are still not visible (or don't exist yet)
                        var hiddenEnd = Math.Min(newBufferLength, CurrentBufferSize);

                        //if the buffer size needs to change...
                        if (newBufferLength != CurrentBufferSize)
                        {
                            //add visible bytes
                            if (CurrentBufferSize < newBufferLength)
                            {
                                for (int i = CurrentBufferSize; i < newBufferLength; i++)
                                    Tiles.Add(newVal);
                            }
                            //remove non-visible bytes
                            else if (shrinkBuffer)
                            {
                                Tiles.RemoveRange(newBufferLength, CurrentBufferSize - newBufferLength);
                            }
                        }
                        //clear any previously hidden bytes
                        for (int i = hiddenStart; i < hiddenEnd; i++)
                            Tiles[i] = 0x00;
                    }
                    break;
            }
            Width = width;
            Height = height;
            NotifyMapResized();
        }

        public void Save(string path)
        {
            Save(path, DefaultHeader, DefaultPostHeader);
        }
        public void Save(string path, byte[] header, byte[] postHeader)
        {
            for (int i = 0; i < Tiles.Count; i++)
                if (Tiles[i] == null)
                    throw new ArgumentNullException("Can't save null");

            using (var bw = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write)))
            {
                bw.Write(header);
                bw.Write(postHeader);
                bw.Write(Width);
                bw.Write(Height);
                for (int i = 0; i < Tiles.Count; i++)
                    bw.Write((byte)Tiles[i]);
            }
        }
    }
}
