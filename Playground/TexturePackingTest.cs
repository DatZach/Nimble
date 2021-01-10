using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Playground
{
    public static class TexturePackingTest
    {
		public static void Execute()
		{
			var t1 = Stopwatch.StartNew();

			var rootDirectory = @"C:\Users\zreedy\AppData\Roaming\GameMakerStudio2-EA\Cache\GMS2CACHE\WrestleFes_5B01BEE6_3380BDA\WrestleFestRedux\default\TexturePageEntries";

			Console.WriteLine($"Discovering textures...");
			var paths = Directory.GetFiles(rootDirectory, "*.png", SearchOption.AllDirectories);

			Console.WriteLine($"Packing {paths.Length} textures...");
			var sheets = CompileTextureSheets(paths, 4096);

			int i = 0;
			foreach (var sheet in sheets)
			{
				sheet.Render();

				string filename = "sheet." + (i++).ToString("G") + ".png";
				string path = filename;

				using (var fs = File.OpenWrite(path))
					sheet.Sheet.Save(fs, ImageFormat.Png);
			}

			t1.Stop();

			Console.WriteLine($"Full pack in {t1.ElapsedMilliseconds}ms");
		}

		private static List<TextureSheet> CompileTextureSheets(IList<string> paths, int size)
		{
			var sheets = new List<TextureSheet>();

			Console.WriteLine("Loading images...");
			var textures = new List<Texture>();
			foreach (var path in paths)
				textures.Add(new Texture(path));

			Console.WriteLine("Packing...");
			var timer = Stopwatch.StartNew();
			textures.Sort((a, b) =>
			{
				int aw = a.Size.Width, ah = a.Size.Height;
				int bw = b.Size.Width, bh = b.Size.Height;
				int aScore = aw * ah;
				int bScore = bw * bh;

				if (aScore == bScore)
				{
					if (ah == bh)
						return 0;

					return aw < bw ? 1 : -1;
				}

				return aScore < bScore ? 1 : -1;
			});

			foreach (var texture in textures)
			{
				bool packed = false;

				foreach (TextureSheet sheet in sheets)
				{
					if (!sheet.PackTexture(texture))
						continue;

					packed = true;
					break;
				}

				if (packed)
					continue;

				TextureSheet newSheet = new TextureSheet(size);
				if (!newSheet.PackTexture(texture))
					throw new Exception($"Texture dimensions ({texture.Size.Width}x{texture.Size.Height}) exceed sheet's ({size}x{size}).");

				sheets.Add(newSheet);
			}

			timer.Stop();

			Console.WriteLine("Compiled {0} textures into {1} sheets ({3}x{3}) in {2}ms.",
							  textures.Count,
							  sheets.Count,
							  timer.ElapsedMilliseconds,
							  size);

			return sheets;
		}
	}

	internal class TextureSheet
	{
		public Bitmap Sheet { get; private set; }

		private readonly List<Texture> packedTextures;
		private readonly List<Rect> partitionTable;

		public TextureSheet(int size)
		{
			Sheet = new Bitmap(size, size);
			packedTextures = new List<Texture>();
			partitionTable = new List<Rect> { new Rect(0, 0, size, size) };
		}

		// public bool method_1(TexturePageEntry texturePageEntry_0, out int int_7, out int int_8)
		public bool PackTexture(Texture texture)
		{
			var texWidth = texture.Size.Width; // W
			var texHeight = texture.Size.Height; // H

			// Find a partition that honors our size, if none can be found this sheet is out of space
			Rect space = null;
			int i = partitionTable.Count;
			while (--i >= 0)
			{
				space = partitionTable[i];
				if (space.Width >= texWidth && space.Height >= texHeight)
					break;
			}

			if (i < 0)
				return false;

			// Remove old one since we've split it
			partitionTable.RemoveAt(i);

			// Create new partitions
			var foo = space.Height - texHeight;
			if (space.Width != 0 && foo != 0)
				partitionTable.AddSorted(new Rect(space.X, space.Y + texHeight, space.Width, foo));

			foo = space.Width - texWidth;
			if (foo != 0 && texHeight != 0)
				partitionTable.AddSorted(new Rect(space.X + texWidth, space.Y, foo, texHeight));

			// Keep track of what we pack
			texture.Position = new Vector2i(space.X, space.Y);
			packedTextures.Add(texture);

			return true;
		}

		public void Render()
		{
			using (var surface = Graphics.FromImage(Sheet))
			{
				foreach (var texture in packedTextures)
					surface.DrawImage(texture.Image, texture.Position.X, texture.Position.Y, texture.Image.Width, texture.Image.Height);
			}
		}
	}

	public static class ListExt
	{
		public static void AddSorted<T>(this List<T> @this, T item)
			where T : IComparable<T>
		{
			if (@this.Count == 0)
			{
				@this.Add(item);
				return;
			}
			if (@this[@this.Count - 1].CompareTo(item) <= 0)
			{
				@this.Add(item);
				return;
			}
			if (@this[0].CompareTo(item) >= 0)
			{
				@this.Insert(0, item);
				return;
			}
			int index = @this.BinarySearch(item);
			if (index < 0)
				index = ~index;
			@this.Insert(index, item);
		}
	}

	internal sealed class Texture
	{
		public Image Image { get; }

		public readonly Size Size;

		public Vector2i Position;

		public Texture(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));

			Image = Image.FromFile(path);
			Size = Image.Size; // Important to cache, the underlying property is expensive
			Position = null;
		}
	}

	internal sealed class Rect : IComparable<Rect>
	{
		public int Width;

		public int Height;

		public int X;

		public int Y;

		public Rect()
		{

		}

		public Rect(int x, int y, int width, int height)
		{
			X = x;
			Y = y;
			Width = width;
			Height = height;
		}

		public int CompareTo(Rect b)
		{
			int aScore = Width * Height;
			int bScore = b.Width * b.Height;

			if (aScore == bScore)
			{
				if (Width == b.Width)
					return 0;

				return Height > b.Height ? -1 : 1;
			}

			return aScore > bScore ? -1 : 1;
		}
	}

	internal sealed class Vector2i
	{
		public int X;

		public int Y;

		public Vector2i()
		{
			X = 0;
			Y = 0;
		}

		public Vector2i(int x, int y)
		{
			X = x;
			Y = y;
		}
	}
}
