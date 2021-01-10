using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using HarmonyLib;

namespace Nimble.Igor
{
    /// <summary>
    /// Refer to TexturePageEntryPatch23 for documentation
    /// They are the same except this uses System.Drawing instead of YoYoImage as per 2.2.5's implementation
    /// </summary>
    class TexturePageEntryPatch225
    {
        public static void Apply(Harmony harmony)
        {
            var yoyoImage = typeof(System.Drawing.Bitmap);
            var texturePage = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePage");
            var texturePageEntry = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePageEntry");
            var wadSaver = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "WADSaver`1");
            var gmSprite = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "GMSprite");

            var gmAssets = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "GMAssets");
            var iffSaver = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "IFFSaver");

            MethodInfo texturePage_LoadEntry_orig = null;
            MethodInfo texturePage_SaveEntry_orig = null;
            var methods = texturePage.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 8 && parameters[3].ParameterType == yoyoImage)
                    texturePage_SaveEntry_orig = method;
                else if (parameters.Length == 1 && method.ReturnType == texturePageEntry)
                    texturePage_LoadEntry_orig = method;
            }

            var program = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "Program");

            MethodInfo program_IsTPECached_orig = null;
            methods = program.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2
                && parameters[0].ParameterType == typeof(string)
                && parameters[1].ParameterType == typeof(IList<string>))
                {
                    program_IsTPECached_orig = method;
                    break;
                }
            }

            MethodInfo iffSaver_Save_orig = null;
            methods = iffSaver.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length != 2)
                    continue;

                if (parameters[0].ParameterType != gmAssets || parameters[1].ParameterType != typeof(string))
                    continue;

                iffSaver_Save_orig = method;
                //break;
            }

            var texturePage_LoadEntry_patch = AccessTools.Method(typeof(TexturePageEntryPatch225), nameof(LoadEntry));
            var texturePage_SaveEntry_patch = AccessTools.Method(typeof(TexturePageEntryPatch225), nameof(SaveEntry));
            var wadSaver_WriteSPRT_patch = AccessTools.Method(typeof(TexturePageEntryPatch225), nameof(WriteIIF));
            var program_IsTPECached_patch = AccessTools.Method(typeof(TexturePageEntryPatch225), nameof(IsTPECached));

            harmony.Patch(texturePage_LoadEntry_orig, transpiler: new HarmonyMethod(texturePage_LoadEntry_patch));
            harmony.Patch(texturePage_SaveEntry_orig, transpiler: new HarmonyMethod(texturePage_SaveEntry_patch));
            harmony.Patch(iffSaver_Save_orig, transpiler: new HarmonyMethod(wadSaver_WriteSPRT_patch));
            harmony.Patch(program_IsTPECached_orig, transpiler: new HarmonyMethod(program_IsTPECached_patch));
        }

        // public new void \u0002(GMAssets \u0002, string \u0003)
        // private void \u0001(IList<KeyValuePair<string, GMSprite>> \u0002, Stream \u0003, IFF \u0004)

        public static IEnumerable<CodeInstruction> SaveEntry(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            var get_Entries = AccessTools.PropertyGetter(typeof(TexturePageEntryCache225), nameof(TexturePageEntryCache225.Entries));
            var method_Set = AccessTools.Method(typeof(TexturePageEntryCache225), nameof(TexturePageEntryCache225.Set));
            var ctor_TexturePageEntry = typeof(Igor.TexturePageEntry).GetConstructor(new[] { typeof(object) });
            var method_ChangeExtension = AccessTools.Method(typeof(System.IO.Path), nameof(Path.ChangeExtension));
            var method_GetFileNameWithoutExtension = AccessTools.Method(typeof(System.IO.Path), nameof(Path.GetFileNameWithoutExtension));
            MethodInfo method_9 = null;
            var yoyoImage = typeof(System.Drawing.Bitmap);
            var texturePage = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePage");
            var methods = texturePage.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 8 && parameters[0].ParameterType == yoyoImage)
                {
                    method_9 = method;
                    break;
                }
            }

            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldstr, ".png");
            yield return new CodeInstruction(OpCodes.Call, method_ChangeExtension);
            yield return new CodeInstruction(OpCodes.Stloc_0);

            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)4);
            yield return new CodeInstruction(OpCodes.Stloc_1);
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldloc_1);
            yield return new CodeInstruction(OpCodes.Ldarg_2);
            yield return new CodeInstruction(OpCodes.Ldarg_3);
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)5);
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)6);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, method_GetFileNameWithoutExtension);
            yield return new CodeInstruction(OpCodes.Call, method_GetFileNameWithoutExtension);
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)7);
            yield return new CodeInstruction(OpCodes.Ldarg_S, (byte)8);
            yield return new CodeInstruction(OpCodes.Call, method_9);
            yield return new CodeInstruction(OpCodes.Stloc_2);

            var label_ret = gen.DefineLabel();
            yield return new CodeInstruction(OpCodes.Ldloc_2);
            yield return new CodeInstruction(OpCodes.Brfalse, label_ret);

            yield return new CodeInstruction(OpCodes.Ldarg_1); // string_0
            yield return new CodeInstruction(OpCodes.Ldloc_2); // texturePageEntry
            yield return new CodeInstruction(OpCodes.Newobj, ctor_TexturePageEntry);
            yield return new CodeInstruction(OpCodes.Call, method_Set);

            yield return new CodeInstruction(OpCodes.Ldloc_2);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Callvirt, TexturePageEntry._set_CacheFileName);

            gen.MarkLabel(label_ret);
            var a = new CodeInstruction(OpCodes.Ldloc_2); // texturePageEntry
            a.labels.Add(label_ret);
            yield return a;
            yield return new CodeInstruction(OpCodes.Ret);
        }

        public static IEnumerable<CodeInstruction> LoadEntry(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            var method_TexturePageCache_Get = AccessTools.Method(typeof(TexturePageEntryCache225), nameof(TexturePageEntryCache225.Get));
            var get_This = typeof(TexturePageEntry).GetProperty("This").GetGetMethod();
            var gmac_TexturePageEntry = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePageEntry");
            var gmac_TexturePage = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePage");
            var prop_Entries = AccessTools.Property(gmac_TexturePage, "Entries");

            //251 035C ldarg.0
            //252 035D    call instance class [mscorlib] System.Collections.Generic.List`1<class GMAssetCompiler.TexturePageEntry> GMAssetCompiler.TexturePage::'\u0001'()
            //253	0362	stloc.s V_7(7)
            //254	0364	ldc.i4.0
            //255	0365	stloc.s V_8(8)
            //256	0367	ldloc.s V_7(7)
            //257	0369	ldloca.s V_8(8)
            //258	036B call    void[mscorlib] System.Threading.Monitor::Enter(object, bool&)
            //259	0370	ldarg.0
            //260	0371	call instance class [mscorlib] System.Collections.Generic.List`1<class GMAssetCompiler.TexturePageEntry> GMAssetCompiler.TexturePage::'\u0001'()
            //261	0376	ldloc.0
            //262	0377	callvirt instance void class [mscorlib] System.Collections.Generic.List`1<class GMAssetCompiler.TexturePageEntry>::Add(!0)
            //263	037C leave.s 269 (038A) ldloc.0 
            //264	037E	ldloc.s V_8(8)
            //265	0380	brfalse.s	268 (0389) endfinally 
            //266	0382	ldloc.s V_7(7)
            //267	0384	call void[mscorlib] System.Threading.Monitor::Exit(object)
            //268	0389	endfinally

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, prop_Entries.GetGetMethod());
            yield return new CodeInstruction(OpCodes.Stloc_S, (byte)7);
            yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            yield return new CodeInstruction(OpCodes.Stloc_S, (byte)8);
            yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)7);
            yield return new CodeInstruction(OpCodes.Ldloca_S, (byte)8);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(System.Threading.Monitor), "Enter", new[] { typeof(object), typeof(bool).MakeByRefType() }));

            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, method_TexturePageCache_Get);
            yield return new CodeInstruction(OpCodes.Callvirt, get_This);
            yield return new CodeInstruction(OpCodes.Castclass, gmac_TexturePageEntry);
            yield return new CodeInstruction(OpCodes.Stloc_0);

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, prop_Entries.GetGetMethod());
            yield return new CodeInstruction(OpCodes.Ldloc_0);
            yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(prop_Entries.PropertyType, "Add"));
            yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)8);
            var label_endfinally = gen.DefineLabel();
            yield return new CodeInstruction(OpCodes.Brfalse_S, label_endfinally);
            yield return new CodeInstruction(OpCodes.Ldloc_S, (byte)7);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(System.Threading.Monitor), "Exit"));
            gen.MarkLabel(label_endfinally);

            yield return new CodeInstruction(OpCodes.Ldloc_0);
            yield return new CodeInstruction(OpCodes.Ret);
        }


        public static IEnumerable<CodeInstruction> WriteIIF(IEnumerable<CodeInstruction> instructions)
        {
            var method_TexturePageCache_Load = AccessTools.Method(typeof(TexturePageEntryCache225), nameof(TexturePageEntryCache225.Load));
            var method_TexturePageCache_Save = AccessTools.Method(typeof(TexturePageEntryCache225), nameof(TexturePageEntryCache225.Save));

            yield return new CodeInstruction(OpCodes.Call, method_TexturePageCache_Load);

            foreach (var inst in instructions)
            {
                if (inst.opcode == OpCodes.Ret)
                {
                    var a = new CodeInstruction(OpCodes.Call, method_TexturePageCache_Save);
                    inst.MoveLabelsTo(a);
                    yield return a;
                }

                yield return inst;
            }
        }

        public static IEnumerable<CodeInstruction> IsTPECached(IEnumerable<CodeInstruction> instructions)
        {
            var method_TexturePageCache_Has = AccessTools.Method(typeof(TexturePageEntryCache225), nameof(TexturePageEntryCache225.Has));

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, method_TexturePageCache_Has);
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }

    public static class TexturePageEntryCache225
    {
        public static string RootDirectory { get; set; }

        public static Dictionary<string, TexturePageEntry> Entries { get; }

        private static readonly object padlock = new object();

        private static FileStream activeBinStream;

        static TexturePageEntryCache225()
        {
            Entries = new Dictionary<string, TexturePageEntry>();
        }

        public static bool Has(string key, IList<string> originalPaths)
        {
            TexturePageEntry entry;
            if (Entries.TryGetValue(key, out entry))
            {
                foreach (var path in originalPaths)
                {
                    if (path == "now")
                        return false;
                    else if (path.EndsWith(".png"))
                    {
                        var origTs = File.GetLastWriteTimeUtc(path);
                        if (entry.TimestampUtc >= origTs)
                            return true;
                    }
                }
            }

            return false;
        }

        public static TexturePageEntry Get(string key)
        {
            var value = Entries[key];

            activeBinStream.Position = value.BinOffset;

            var buffer = new byte[4];
            activeBinStream.Read(buffer, 0, 4);
            var width = BitConverter.ToInt32(buffer, 0);
            activeBinStream.Read(buffer, 0, 4);
            var height = BitConverter.ToInt32(buffer, 0);
            var yoyoImage = new Bitmap(width, height);

            var pixels = new byte[width * height * 4];
            var dst = yoyoImage.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            activeBinStream.Read(pixels, 0, pixels.Length);
            Marshal.Copy(pixels, 0, dst.Scan0, pixels.Length);
            yoyoImage.UnlockBits(dst);

            value.Bitmap = yoyoImage;
            value.BitmapFileName = Path.ChangeExtension(key, "png");

            return value;
        }

        public static void Set(string key, TexturePageEntry entry)
        {
            lock (padlock)
            {
                entry.IsDirty = true;
                Entries[key] = entry;
            }
        }

        public static void Save()
        {
            var t1 = Stopwatch.StartNew();
            Console.Write("Caching TPE... ");
            var xmlPath = Path.Combine(RootDirectory, "TexturePageEntries.xml");
            var binPath = Path.Combine(RootDirectory, "TexturePageEntries.bin");

            if (activeBinStream != null)
            {
                activeBinStream.Close();
                activeBinStream = null;
            }

            using (var binStream = new FileStream(binPath, FileMode.Append, FileAccess.Write, FileShare.Write))
            {
                var xml = new XmlTextWriter(xmlPath, Encoding.UTF8)
                {
                    Formatting = Formatting.Indented
                };

                xml.WriteStartDocument();
                xml.WriteStartElement("Entries");

                foreach (var kvp in Entries)
                {
                    var entry = kvp.Value;
                    xml.WriteStartElement("TPE");
                    xml.WriteAttributeString("key", kvp.Key);
                    xml.WriteAttributeString("name", entry.Name);
                    xml.WriteAttributeString("w", entry.W.ToString());
                    xml.WriteAttributeString("h", entry.H.ToString());
                    xml.WriteAttributeString("hasAlpha", entry.HasAlpha.ToString());
                    xml.WriteAttributeString("hash", entry.Hash.ToString());
                    xml.WriteAttributeString("xoffset", entry.XOffset.ToString());
                    xml.WriteAttributeString("yoffset", entry.YOffset.ToString());
                    xml.WriteAttributeString("cropWidth", entry.CropWidth.ToString());
                    xml.WriteAttributeString("cropHeight", entry.CropHeight.ToString());
                    xml.WriteAttributeString("ow", entry.OW.ToString());
                    xml.WriteAttributeString("oh", entry.OH.ToString());

                    if (entry.IsDirty && entry.Bitmap != null)
                    {
                        var bitmap = (Bitmap)entry.Bitmap;
                        var width = bitmap.Width;
                        var height = bitmap.Height;
                        var src = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                        var pixelsLength = width * height * 4;
                        var pixels = new byte[pixelsLength];
                        Marshal.Copy(src.Scan0, pixels, 0, pixelsLength);
                        bitmap.UnlockBits(src);

                        if (entry.BinSize == 0 || pixelsLength > entry.BinSize)
                            binStream.Position = binStream.Length;
                        else
                            binStream.Position = entry.BinOffset;

                        xml.WriteAttributeString("tsUtc", File.GetLastWriteTimeUtc(entry.BitmapFileName).ToBinary().ToString());
                        xml.WriteAttributeString("binOffset", binStream.Position.ToString());

                        binStream.Write(BitConverter.GetBytes(width), 0, 4);
                        binStream.Write(BitConverter.GetBytes(height), 0, 4);
                        binStream.Write(pixels, 0, pixelsLength);

                        xml.WriteAttributeString("binSize", Math.Max(entry.BinSize, pixelsLength).ToString());
                    }
                    else
                    {
                        xml.WriteAttributeString("binOffset", entry.BinOffset.ToString());
                        xml.WriteAttributeString("binSize", entry.BinSize.ToString());
                        xml.WriteAttributeString("tsUtc", entry.TimestampUtc.ToBinary().ToString());
                    }

                    xml.WriteEndElement();
                }

                xml.WriteEndElement();
                xml.WriteEndDocument();
                xml.Flush();
                xml.Close();
            }

            t1.Stop();
            Console.WriteLine("Done! {0}ms", t1.ElapsedMilliseconds);
        }

        public static void Load()
        {
            var xmlPath = Path.Combine(RootDirectory, "TexturePageEntries.xml");
            var binPath = Path.Combine(RootDirectory, "TexturePageEntries.bin");

            Entries.Clear();

            if (!File.Exists(xmlPath) || !File.Exists(binPath))
                return;

            Console.Write("Loading TPE cache... ");
            activeBinStream = File.OpenRead(binPath);

            var xml = new XmlDocument();
            xml.Load(xmlPath);

            foreach (XmlNode xmlTpe in xml.SelectNodes("/Entries/*"))
            {
                var entry = new TexturePageEntry();
                entry.Name = xmlTpe.Attributes["name"].Value;
                entry.XOffset = int.Parse(xmlTpe.Attributes["xoffset"].Value);
                entry.YOffset = int.Parse(xmlTpe.Attributes["yoffset"].Value);
                entry.CropWidth = int.Parse(xmlTpe.Attributes["cropWidth"].Value);
                entry.CropHeight = int.Parse(xmlTpe.Attributes["cropHeight"].Value);
                entry.OW = int.Parse(xmlTpe.Attributes["ow"].Value);
                entry.OH = int.Parse(xmlTpe.Attributes["oh"].Value);
                entry.W = int.Parse(xmlTpe.Attributes["w"].Value);
                entry.H = int.Parse(xmlTpe.Attributes["h"].Value);
                entry.HasAlpha = bool.Parse(xmlTpe.Attributes["hasAlpha"].Value);
                entry.Hash = uint.Parse(xmlTpe.Attributes["hash"].Value);
                entry.BinOffset = uint.Parse(xmlTpe.Attributes["binOffset"].Value);
                entry.BinSize = uint.Parse(xmlTpe.Attributes["binSize"].Value);
                entry.CacheFileName = xmlTpe.Attributes["key"].Value;
                entry.TimestampUtc = DateTime.FromBinary(long.Parse(xmlTpe.Attributes["tsUtc"].Value));
                Entries.Add(entry.CacheFileName, entry);
            }

            Console.WriteLine("Done!");
        }
    }
}
