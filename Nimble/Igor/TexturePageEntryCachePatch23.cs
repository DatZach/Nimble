using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using HarmonyLib;

namespace Nimble.Igor
{
    /// <summary>
    /// PATCH FOR 2.3
    /// 
    /// PATCH FOR TPE CACHE
    ///     Original code is slow, even on SSDs due to the significant amount of overhead
    /// that NTFS incurs for files larger than 140 bytes in a single directory. More time is
    /// spent opening the files than is spent reading data from them. In order to fix this
    /// we replace TexturePage.LoadEntry, TexturePage.SaveEntry, Program.IsTPECached, IIFSaver.WriteSPRT
    /// in order to replace loading from individual files.
    /// Instead we write everything the raw RGBA data to a BIN file. We track the TPEs and their offset
    /// and size of the raw RGBA data in the BIN file in an XML file, stored in the cache directory for TPEs.
    /// </summary>
    public static class TexturePageEntryPatch23
    {
        public static void Apply(Harmony harmony)
        {
            var yoyoImage = IgorPlugin.Instance.YoYoImage.DefinedTypes.FirstOrDefault(x => x.Name == "YoYoImage");
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

            var texturePage_LoadEntry_patch = AccessTools.Method(typeof(TexturePageEntryPatch23), nameof(LoadEntry));
            var texturePage_SaveEntry_patch = AccessTools.Method(typeof(TexturePageEntryPatch23), nameof(SaveEntry));
            var wadSaver_WriteSPRT_patch = AccessTools.Method(typeof(TexturePageEntryPatch23), nameof(WriteIIF));
            var program_IsTPECached_patch = AccessTools.Method(typeof(TexturePageEntryPatch23), nameof(IsTPECached));

            harmony.Patch(texturePage_LoadEntry_orig, transpiler: new HarmonyMethod(texturePage_LoadEntry_patch));
            harmony.Patch(texturePage_SaveEntry_orig, transpiler: new HarmonyMethod(texturePage_SaveEntry_patch));
            harmony.Patch(iffSaver_Save_orig, transpiler: new HarmonyMethod(wadSaver_WriteSPRT_patch));
            harmony.Patch(program_IsTPECached_orig, transpiler: new HarmonyMethod(program_IsTPECached_patch));
        }

        // public new void \u0002(GMAssets \u0002, string \u0003)
        // private void \u0001(IList<KeyValuePair<string, GMSprite>> \u0002, Stream \u0003, IFF \u0004)

        // Body for TexturePage.SaveEntry
        public static IEnumerable<CodeInstruction> SaveEntry(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            /*
            string text = Path.ChangeExtension(string_0, ".png");
	        TexturePageEntry texturePageEntry = this.method_9(yoYoImage_0, bool_0, bool_1, bool_2, bool_3, Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(string_0)), int_6, string_1);
	        if (texturePageEntry != null)
	        {
		        lock (padlock)
                {
                    texturePageEntry.CacheFileName = string_0;
                    texturePageEntry.IsDirty = true;
                    Entries[string_0] = texturePageEntry;
                }
	        }

	        return texturePageEntry;
            */

            var get_Entries = AccessTools.PropertyGetter(typeof(TexturePageEntryCache23), nameof(TexturePageEntryCache23.Entries));
            var method_Set = AccessTools.Method(typeof(TexturePageEntryCache23), nameof(TexturePageEntryCache23.Set));
            var ctor_TexturePageEntry = typeof(Igor.TexturePageEntry).GetConstructor(new[] { typeof(object) });
            var method_ChangeExtension = AccessTools.Method(typeof(System.IO.Path), nameof(Path.ChangeExtension));
            var method_GetFileNameWithoutExtension = AccessTools.Method(typeof(System.IO.Path), nameof(Path.GetFileNameWithoutExtension));
            MethodInfo method_9 = null;
            var yoyoImage = IgorPlugin.Instance.YoYoImage.DefinedTypes.FirstOrDefault(x => x.Name == "YoYoImage");
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

        // private TexturePageEntry TexturePage.LoadTPE(string filename)
        public static IEnumerable<CodeInstruction> LoadEntry(IEnumerable<CodeInstruction> instructions, ILGenerator gen)
        {
            /*
            lock (padlock)
            {
                var value = Entries[filename];

                activeBinStream.Position = value.BinOffset;

                var buffer = new byte[4];
                activeBinStream.Read(buffer, 0, 4);
                var width = BitConverter.ToInt32(buffer, 0);
                activeBinStream.Read(buffer, 0, 4);
                var height = BitConverter.ToInt32(buffer, 0);
                var yoyoImage = YoYoImage.New(width, height);
                var pixels = YoYoImage.GetPixels(yoyoImage);
                activeBinStream.Read(pixels, 0, pixels.Length);
                value.Bitmap = yoyoImage;
                value.BitmapFileName = Path.ChangeExtension(key, "png");

                return value;
            }
            */

            /* NOTE
             * There's some weird code here where we have to translate between the Nimble local cache
             * and the "Entries" dictionary that's local to GMAC. That's what the casting and call to
             * Add in "Entries" is below, translating the Nimble cache to the GMAC cache and GMAC types.
             */

            var method_TexturePageCache_Get = AccessTools.Method(typeof(TexturePageEntryCache23), nameof(TexturePageEntryCache23.Get));
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

        // IFFSaver.WriteIFF
        public static IEnumerable<CodeInstruction> WriteIIF(IEnumerable<CodeInstruction> instructions)
        {
            /* IMPLEMENTATION NOTES
             * Call TexturePageCache.Load() before doing anything else.
             * Call TexturePageCache.Save() after saving the IFF
             * 
             * These calls can be moved to the local WriteSPRT, the only reason I didn't is because you can't
             * patch generic methods which those are. The lambdas hide the body, so this is a workaround for that.
             */

            var method_TexturePageCache_Load = AccessTools.Method(typeof(TexturePageEntryCache23), nameof(TexturePageEntryCache23.Load));
            var method_TexturePageCache_Save = AccessTools.Method(typeof(TexturePageEntryCache23), nameof(TexturePageEntryCache23.Save));

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

        // bool Program.IsTPECached(string path);
        public static IEnumerable<CodeInstruction> IsTPECached(IEnumerable<CodeInstruction> instructions)
        {
            /* IMPLEMENTATION NOTES
             * Body is TexturePageEntryCache23.Has
             */

            var method_TexturePageCache_Has = AccessTools.Method(typeof(TexturePageEntryCache23), nameof(TexturePageEntryCache23.Has));

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, method_TexturePageCache_Has);
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }

    /// <summary>
    /// This singleton can live as its own static class in the GMAC.
    /// It encapsulates the TPE Cache
    /// </summary>
    public static class TexturePageEntryCache23
    {
        // BUG RootDirectory is set to the Cache root, not the TPE directory of our current build
        public static string RootDirectory { get; set; }

        // NOTE Key is the full path to the original XML file in the cache, could be improved
        public static Dictionary<string, TexturePageEntry> Entries { get; }

        private static readonly object padlock = new object();

        private static FileStream activeBinStream;

        static TexturePageEntryCache23()
        {
            Entries = new Dictionary<string, TexturePageEntry>();
        }

        // Body for Program.IsTPECached
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

        // Partial body for TexturePage.LoadEntry
        public static TexturePageEntry Get(string key)
        {
            var value = Entries[key];

            activeBinStream.Position = value.BinOffset;

            var buffer = new byte[4];
            activeBinStream.Read(buffer, 0, 4);
            var width = BitConverter.ToInt32(buffer, 0);
            activeBinStream.Read(buffer, 0, 4);
            var height = BitConverter.ToInt32(buffer, 0);
            var yoyoImage = YoYoImage.New(width, height);
            var pixels = YoYoImage.GetPixels(yoyoImage);
            activeBinStream.Read(pixels, 0, pixels.Length);
            value.Bitmap = yoyoImage;
            value.BitmapFileName = Path.ChangeExtension(key, "png");

            return value;
        }

        // Partial body for TexturePage.SaveEntry
        public static void Set(string key, TexturePageEntry entry)
        {
            lock (padlock)
            {
                entry.IsDirty = true;
                Entries[key] = entry;
            }
        }

        /// <summary>
        /// Serializes the TPEs to disk.
        /// Most of this is normal C# code, some of this has to do some weird reflection to interact with
        /// YoYoImage without actually referencing the assembly. This should be able to live in GMAC natively
        /// without weird reflection stuff.
        /// </summary>
        public static void Save()
        {
            var type_YoYoImage = IgorPlugin.Instance.YoYoImage.DefinedTypes.FirstOrDefault(x => x.Name == "YoYoImage");
            var prop_YoYoImage_Pixels = type_YoYoImage.GetProperty("Pixels");
            var prop_YoYoImage_Width = type_YoYoImage.GetProperty("Width");
            var prop_YoYoImage_Height = type_YoYoImage.GetProperty("Height");

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
                    
                    /*
                     * If this entry has been modified (IsDirty), then we need to overwrite the old entry
                     * in the blob and update the XML entry. If new_rgba_data_size <= old_rgba_data_size
                     * then we can reuse the old spot in the BIN. Otherwise we orphan it.
                     * TODO Track orphans so that we can reuse their space
                     */
                    if (entry.IsDirty && entry.Bitmap != null)
                    {
                        var pixels = (byte[])prop_YoYoImage_Pixels.GetValue(entry.Bitmap);
                        var width = (int)prop_YoYoImage_Width.GetValue(entry.Bitmap);
                        var height = (int)prop_YoYoImage_Height.GetValue(entry.Bitmap);

                        if (entry.BinSize == 0 || pixels.Length > entry.BinSize)
                            binStream.Position = binStream.Length;
                        else
                            binStream.Position = entry.BinOffset;

                        xml.WriteAttributeString("tsUtc", File.GetLastWriteTimeUtc(entry.BitmapFileName).ToBinary().ToString());
                        xml.WriteAttributeString("binOffset", binStream.Position.ToString());

                        binStream.Write(BitConverter.GetBytes(width), 0, 4);
                        binStream.Write(BitConverter.GetBytes(height), 0, 4);
                        binStream.Write(pixels, 0, pixels.Length);

                        xml.WriteAttributeString("binSize", Math.Max(entry.BinSize, pixels.Length).ToString());
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

    // GMAssetCompiler.TexturePageEntry
    /// <summary>
    /// This is a proxy class to make life in Nimble easier.
    /// IMPORTANT! This adds some new fields to TexturePageEntry which are required,
    /// they will be marked
    /// </summary>
    public sealed class TexturePageEntry
    {
        // GMAssetCompiler.TexturePageEntry that we're proxying
        public object This { get; }

        public int XOffset
        {
            get => (int)xoffset.GetValue(This);
            set => xoffset.SetValue(This, value);
        }

        public int YOffset
        {
            get => (int)yoffset.GetValue(This);
            set => yoffset.SetValue(This, value);
        }

        public int CropWidth
        {
            get => (int)cropWidth.GetValue(This);
            set => cropWidth.SetValue(This, value);
        }

        public int CropHeight
        {
            get => (int)cropHeight.GetValue(This);
            set => cropHeight.SetValue(This, value);
        }

        public int OW
        {
            get => (int)ow.GetValue(This);
            set => ow.SetValue(This, value);
        }

        public int OH
        {
            get => (int)oh.GetValue(This);
            set => oh.SetValue(This, value);
        }

        public int W
        {
            get => (int)w.GetValue(This);
            set => w.SetValue(This, value);
        }

        public int H
        {
            get => (int)h.GetValue(This);
            set => h.SetValue(This, value);
        }

        public string Name
        {
            get => (string)name.GetValue(This);
            set => name.SetValue(This, value);
        }

        public bool HasAlpha
        {
            get => (bool)hasAlpha.GetValue(This);
            set => hasAlpha.SetValue(This, value);
        }

        public uint Hash
        {
            get => (uint)hash.GetValue(This);
            set => hash.SetValue(This, value);
        }

        // YoYoImage
        public object Bitmap
        {
            get => bitmap.GetValue(This);
            set => bitmap.SetValue(This, value);
        }

        public string BitmapFileName
        {
            get => (string)bitmapFileName.GetValue(This);
            set => bitmapFileName.SetValue(This, value);
        }

        public string CacheFileName
        {
            get => (string)cacheFileName.GetValue(This);
            set => cacheFileName.SetValue(This, value);
        }

        // NEW PROPERTY!
        public uint BinOffset { get; set; }

        // NEW PROPERTY!
        public uint BinSize { get; set; }

        // NEW PROPERTY!
        public bool IsDirty { get; set; }

        // NEW PROPERTY!
        public DateTime TimestampUtc { get; set; }

        public static MethodInfo _set_CacheFileName => cacheFileName.GetSetMethod();

        private static readonly ConstructorInfo ctor;
        private static readonly PropertyInfo xoffset;
        private static readonly PropertyInfo yoffset;
        private static readonly PropertyInfo cropWidth;
        private static readonly PropertyInfo cropHeight;
        private static readonly PropertyInfo ow;
        private static readonly PropertyInfo oh;
        private static readonly PropertyInfo w;
        private static readonly PropertyInfo h;
        private static readonly PropertyInfo name;
        private static readonly PropertyInfo hasAlpha;
        private static readonly PropertyInfo hash;
        private static readonly PropertyInfo bitmap;
        private static readonly PropertyInfo bitmapFileName;
        private static readonly PropertyInfo cacheFileName;

        static TexturePageEntry()
        {
            var type = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "TexturePageEntry");
            ctor = type.GetConstructor(new Type[0]);
            xoffset = type.GetProperty("XOffset");
            yoffset = type.GetProperty("YOffset");
            cropWidth = type.GetProperty("CropWidth");
            cropHeight = type.GetProperty("CropHeight");
            ow = type.GetProperty("OW");
            oh = type.GetProperty("OH");
            w = type.GetProperty("W");
            h = type.GetProperty("H");
            name = type.GetProperty("Name");
            hasAlpha = type.GetProperty("HasAlpha");
            hash = type.GetProperty("Hash");
            bitmap = type.GetProperty("Bitmap");
            bitmapFileName = type.GetProperty("BitmapFileName");
            cacheFileName = type.GetProperty("CacheFileName");
        }

        public TexturePageEntry()
        {
            This = ctor.Invoke(new object[0]);
        }

        public TexturePageEntry(object _this)
        {
            This = _this;
        }
    }

    // GMAssetCompiler.YoYoImage
    // NOTE Just a proxy class
    public static class YoYoImage
    {
        private static readonly ConstructorInfo ctor_YoYoImage;
        private static readonly PropertyInfo prop_Pixels;
        private static readonly MethodInfo method_Save;
        private static readonly MethodInfo method_FromStream;

        static YoYoImage()
        {
            var type_YoYoImage = IgorPlugin.Instance.YoYoImage.DefinedTypes.FirstOrDefault(x => x.Name == "YoYoImage");
            ctor_YoYoImage = type_YoYoImage.GetConstructor(new[] { typeof(int), typeof(int) });
            prop_Pixels = type_YoYoImage.GetProperty("Pixels");
            method_Save = type_YoYoImage.GetMethods(BindingFlags.Public | BindingFlags.Instance).First(x => x.Name == "Save" && x.GetParameters()[0].ParameterType == typeof(Stream));
            method_FromStream = type_YoYoImage.GetMethod("FromStream", BindingFlags.Public | BindingFlags.Static);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object New(int width, int height)
        {
            return ctor_YoYoImage.Invoke(new object[] { width, height });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] GetPixels(object yoyoImage)
        {
            return (byte[])prop_Pixels.GetValue(yoyoImage);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Save(object _this, Stream _stream)
        {
            method_Save.Invoke(_this, new object[] { _stream });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object FromStream(Stream _stream)
        {
            return method_FromStream.Invoke(null, new[] { _stream });
        }
    }
}
