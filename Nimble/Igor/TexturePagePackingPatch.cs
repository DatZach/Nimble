using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace Nimble.Igor
{
	/// <summary>
	/// PATCH FOR 2.3 AND 2.2.5
	/// 
	/// PATCH FOR TEXTURE PAGE PACKING
	///		This replacing the original packing algorithm with a simpler binary partitioning
	/// algorithm. Initial build times and build-from-cache when sprites are modified
	/// are *significantly* faster in my experience, at the cost of more lossy packing.
	/// One game jumped from 250 texture pages (4k) to 265 texture pages.
	/// Offering this as an alternative algorithm for non-release would be nice.
	/// </summary>
    public static class TexturePagePackingPatch
    {
		public static void Apply(Harmony harmony)
		{
			var type_Texture = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "Texture");
			var type_TexturePage = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePage");
			var type_TexturePageEntry = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePageEntry");

			MethodInfo method_PackTexture_orig = null;
			var methods = type_Texture.GetMethods(BindingFlags.Public | BindingFlags.Instance);
			foreach (var method in methods)
			{
				var parameters = method.GetParameters();
				if (parameters.Length == 3 && parameters[0].ParameterType == type_TexturePageEntry)
					method_PackTexture_orig = method;
			}

			MethodInfo method_CompileTextureSheets_orig = null;
			methods = type_TexturePage.GetMethods(BindingFlags.Public | BindingFlags.Instance);
			foreach (var method in methods)
			{
				var parameters = method.GetParameters();
				var methodBody = method.GetMethodBody();
				if (parameters.Length == 0 && methodBody?.LocalVariables.Count == 55)
					method_CompileTextureSheets_orig = method;
			}

			var method_PackTexture_patch = AccessTools.Method(typeof(TexturePagePackingPatch), nameof(PackTexture));
			var method_CompileTextureSheets_patch = AccessTools.Method(typeof(TexturePagePackingPatch), nameof(CompileTextureSheets));

			harmony.Patch(method_PackTexture_orig, transpiler: new HarmonyMethod(method_PackTexture_patch));
			harmony.Patch(method_CompileTextureSheets_orig, transpiler: new HarmonyMethod(method_CompileTextureSheets_patch));
		}

		// TexturePage.CompileTextureSheets
		public static IEnumerable<CodeInstruction> CompileTextureSheets(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator)
		{
			/* IMPLEMENTATION NOTES
			 * Mostly the same except that the second pass needs to be removed
			 */

			// TODO Insert sort for faster algorithm before inserting
			//textures.Sort((a, b) =>
			//{
			//	int aw = a.Size.Width, ah = a.Size.Height;
			//	int bw = b.Size.Width, bh = b.Size.Height;
			//	int aScore = aw * ah;
			//	int bScore = bw * bh;

			//	if (aScore == bScore)
			//	{
			//		if (ah == bh)
			//			return 0;

			//		return aw < bw ? 1 : -1;
			//	}

			//	return aScore < bScore ? 1 : -1;
			//});

			int i = 0;
			foreach (var _inst in instructions)
			{
				var inst = _inst;

				// nop out the second pass for texture packing
				if ((i >= 522 && i <= 642))
				{
					var nop = new CodeInstruction(OpCodes.Nop);
					inst.MoveLabelsTo(nop);
					inst = nop;
				}

				yield return inst;
				++i;
			}
		}

		// public bool method_1(TexturePageEntry texturePageEntry_0, out int int_7, out int int_8)
		public static IEnumerable<CodeInstruction> PackTexture(IEnumerable<CodeInstruction> instructions)
        {
			//0   0000    nop
			//1   0001    ldarg.0
			//2   0002    ldarg.1
			//3   0003    ldarg.2
			//4   0004    ldarg.3
			//5   0005    call instance bool Nimble.Igor.Texture::PackTexture(object, int32 &, int32 &)
			//6   000A	stloc.0
			//7   000B	br.s    8(000D) ldloc.0
			//8   000D    ldloc.0
			//9   000E    ret

			/* IMPLEMENTATION NOTES
			 * Refer to Nimble.Texture.Pack for body
			 */

			var method_Texture_PackTexture = AccessTools.Method(typeof(Texture), nameof(Texture.Pack));

			yield return new CodeInstruction(OpCodes.Ldarg_0);
			yield return new CodeInstruction(OpCodes.Ldarg_1);
			yield return new CodeInstruction(OpCodes.Ldarg_2);
			yield return new CodeInstruction(OpCodes.Ldarg_3);
			yield return new CodeInstruction(OpCodes.Call, method_Texture_PackTexture);
			yield return new CodeInstruction(OpCodes.Ret);
		}
    }

	/// <summary>
	/// Proxy class for GMAssetCompiler.Texture
	/// New fields added, they will be marked.
	/// </summary>
	internal class Texture
	{
		// NEW FIELD
		public List<Rect> Partitions { get; }

		// NEW FIELD
		public int Size { get; }

		// NOTE Just a lookup for Nimble<->GMAC texture proxy
		private readonly static Dictionary<object, Texture> textures;

		// Native GMAC fields
		private readonly static PropertyInfo prop_TPE_W;
		private readonly static PropertyInfo prop_TPE_H;
		private readonly static PropertyInfo prop_Texture_GridSizeX;
		private readonly static PropertyInfo prop_Texture_GridSizeY;
		private readonly static PropertyInfo prop_TPE_BorderWidthH;
		private readonly static PropertyInfo prop_TPE_BorderWidthV;
		private readonly static PropertyInfo prop_TPE_OriginalRepeatBorder;
		private readonly static PropertyInfo prop_TPE_RepeatBorder;
		private readonly static PropertyInfo prop_Texture_AreaFree;

		static Texture()
		{
			var type_TexturePageEntry = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePageEntry");
			prop_TPE_W = type_TexturePageEntry.GetProperty("W");
			prop_TPE_H = type_TexturePageEntry.GetProperty("H");
			
			prop_TPE_BorderWidthH = type_TexturePageEntry.GetProperty("BorderWidthH");
			prop_TPE_BorderWidthV = type_TexturePageEntry.GetProperty("BorderWidthV");
			prop_TPE_OriginalRepeatBorder = type_TexturePageEntry.GetProperty("OriginalRepeatBorder");
			prop_TPE_RepeatBorder = type_TexturePageEntry.GetProperty("RepeatBorder");

			var type_Texture = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "Texture");
			prop_Texture_AreaFree = type_Texture.GetProperty("AreaFree");
			prop_Texture_GridSizeX = type_Texture.GetProperty("GridSizeX");
			prop_Texture_GridSizeY = type_Texture.GetProperty("GridSizeY");

			textures = new Dictionary<object, Texture>();
		}

		public Texture(int size)
		{
			Size = size;
			Partitions = new List<Rect>
			{
				new Rect(0, 0, size, size)
			};
		}

		// public bool method_1(TexturePageEntry texturePageEntry_0, out int int_7, out int int_8)
		public static bool Pack(object texture, object tpe, out int x, out int y)
		{
			// NOTE This is to cache what should be a property in GMAssetCompiler.Texture
			//		textureMixin = this (Texture)
			Texture textureMixin;
			if (!textures.TryGetValue(texture, out textureMixin))
			{
				// HACK Little dicey, but we need the size of the Texture sheet and I'm too lazy to poke 
				//		into the yyRect list. As long as we get to the Texture before anything modifies it
				//		we'll get an accurate size for the sheet
				var size = (int)prop_Texture_AreaFree.GetValue(texture);
				size = (int)Math.Sqrt(size);

				textureMixin = new Texture(size);
				textures.Add(texture, textureMixin);
			}

			var partitions = textureMixin.Partitions;

			var texWidth = (int)prop_TPE_W.GetValue(tpe);
			var texHeight = (int)prop_TPE_H.GetValue(tpe);

			var flag1 = false;
			var flag2 = false;

			int borderWidth = (int)prop_Texture_GridSizeX.GetValue(texture);
			int borderHeight = (int)prop_Texture_GridSizeY.GetValue(texture);
			var originalRepeatBorder = (bool)prop_TPE_OriginalRepeatBorder.GetValue(tpe);
			if (originalRepeatBorder)
			{
				borderWidth += (int)prop_TPE_BorderWidthH.GetValue(tpe);
				borderHeight += (int)prop_TPE_BorderWidthV.GetValue(tpe);
			}

			if (texWidth != textureMixin.Size)
			{
				texWidth += borderWidth * 2;
				flag1 = true;
			}
			if (texHeight != textureMixin.Size)
			{
				texHeight += borderHeight * 2;
				flag2 = true;
			}

			prop_TPE_RepeatBorder.SetValue(tpe, originalRepeatBorder);
			if (!flag1 || !flag2)
			{
				prop_TPE_RepeatBorder.SetValue(tpe, false);
			}

			// Find a partition that honors our size, if none can be found this sheet is out of space
			Rect space = null;
			int i = partitions.Count;
			while (--i >= 0)
			{
				space = partitions[i];
				if (space.Width >= texWidth && space.Height >= texHeight)
					break;
			}

			if (i < 0)
			{
				x = -1;
				y = -1;
				return false;
			}

			// Remove old one since we've split it
			partitions.RemoveAt(i);

			// Create new partitions
			var foo = space.Height - texHeight;
			if (space.Width != 0 && foo != 0)
				partitions.AddSorted(new Rect(space.X, space.Y + texHeight, space.Width, foo));

			foo = space.Width - texWidth;
			if (foo != 0 && texHeight != 0)
				partitions.AddSorted(new Rect(space.X + texWidth, space.Y, foo, texHeight));

			// Keep track of what we pack
			x = space.X + borderWidth;
			y = space.Y + borderHeight;

			return true;
		}
	}

	public static class ListUtility
	{
		public static void AddSorted<T>(this List<T> list, T item)
			where T : IComparable<T>
		{
			if (list.Count == 0)
				list.Add(item);
			else if (list[list.Count - 1].CompareTo(item) <= 0)
				list.Add(item);
			else if (list[0].CompareTo(item) >= 0)
				list.Insert(0, item);
			else
			{
				int index = list.BinarySearch(item);
				if (index < 0)
					index = ~index;
				list.Insert(index, item);
			}
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
