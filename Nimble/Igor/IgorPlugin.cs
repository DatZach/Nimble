using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using HarmonyLib;

namespace Nimble.Igor
{
    public sealed class IgorPlugin
    {
        public Assembly GMAC { get; private set; }
        public Assembly YoYoImage { get; private set; }
        public Assembly CoreResources { get; private set; }

        /// <summary>
        /// Applys all patches to Igor and the GMAC
        /// Most of this is setting up environment for the individual patches
        /// </summary>
        public void Hook()
        {
            Console.WriteLine("Nimble Igor Hook");

            var args = Environment.GetCommandLineArgs();
            var config = Config.FromCommandLine(args);
            if (config == null)
            {
                Console.WriteLine("Unable to load Igor options");
                Environment.Exit(1);
                return;
            }

            var harmony = new Harmony("Nimble");

            GMAC = Assembly.LoadFrom("GMAssetCompiler.exe");

            // NOTE YoYoImage.dll only exists in GM2.3
            if (File.Exists("YoYoImage.dll"))
            {
                YoYoImage = Assembly.LoadFrom("YoYoImage.dll");
                CoreResources = Assembly.LoadFrom("CoreResources.dll");

                // 2.3 PATCHES
                TexturePageEntryCache23.RootDirectory = Path.GetDirectoryName(config.preferences);
                TexturePageEntryPatch23.Apply(harmony);
            }
            else
            {
                // 2.2.5 PATCHES
                TexturePageEntryCache225.RootDirectory = Path.GetDirectoryName(config.preferences);
                TexturePageEntryPatch225.Apply(harmony);
            }

            // PATCHES FOR 2.2.5 AND 2.3.1
            TexturePagePackingPatch.Apply(harmony);
            GMLCompilePatch.Apply(harmony);
        }

        private static IgorPlugin instance;
        public static IgorPlugin Instance => instance ?? (instance = new IgorPlugin());
    }

    public sealed class Config
    {
        public string runtimeLocation { get; set; }

        public string preferences { get; set; }

        public static Config FromCommandLine(string[] args)
        {
            var optionsArg = args.FirstOrDefault(x => x.StartsWith("-options"));
            if (optionsArg != null)
            {

                var optionsPath = optionsArg.Split('=')[1];
                optionsPath = optionsPath.Replace("\"", "");

                var contents = File.ReadAllText(optionsPath);
                var serializer = new JavaScriptSerializer();
                return serializer.Deserialize<Config>(contents);
            }

            var cdArg = args.FirstOrDefault(x => x.StartsWith("/cd"));
            if (cdArg != null)
            {
                var cdPath = cdArg.Split('=')[1];
                cdPath = cdPath.Replace("\"", "");

                return new Config
                {
                    preferences = cdPath
                };
            }

            return null;
        }
    }
}
