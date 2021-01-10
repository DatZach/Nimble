using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace Nimble.Igor
{
    /// <summary>
    /// PATCH FOR 2.2.5 AND 2.3
    /// 
    /// PATCH FOR GMLCompiler
    ///     
    /// </summary>
    class GMLCompilePatch
    {
        private static PropertyInfo prop_GML2VM_Strings;
        private static List<string> GML2VM_Strings;

        public static void Apply(Harmony harmony)
        {
            // GMLCompile

            var type_GMLCompile = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "GMLCompile");

            MethodInfo method_GMLCompile_smethod_35_orig = null;
            var methods = type_GMLCompile.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(string)
                && parameters[1].ParameterType.FullName == "System.String&"
                && method.ReturnType == typeof(int))
                {
                    method_GMLCompile_smethod_35_orig = method;
                    break;
                }
            }

            //method_GMLCompile_smethod_35_orig = methods[15];
            //var method_GMLCompile_smethod_35_patch = AccessTools.Method(typeof(GMLCompilePatch), nameof(smethod_35));

            //harmony.Patch(method_GMLCompile_smethod_35_orig, transpiler: new HarmonyMethod(method_GMLCompile_smethod_35_patch));

            // GML2VM

            var type_GML2VM = IgorPlugin.Instance.GMAC.DefinedTypes.FirstOrDefault(x => x.Name == "GML2VM");
            prop_GML2VM_Strings = type_GML2VM.GetProperty("Strings", BindingFlags.Public | BindingFlags.Static);
            GML2VM_Strings = (List<string>)prop_GML2VM_Strings.GetValue(null);

            MethodInfo method_GML2VM_GetStringId_orig = null;
            methods = type_GML2VM.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in methods)
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(string)
                && method.ReturnType == typeof(int))
                {
                    method_GML2VM_GetStringId_orig = method;
                    break;
                }
            }

            var method_GML2VM_GetStringId_patch = AccessTools.Method(typeof(GMLCompilePatch), nameof(GetStringId));

            harmony.Patch(method_GML2VM_GetStringId_orig, transpiler: new HarmonyMethod(method_GML2VM_GetStringId_patch));
        }

        // GM2VM

        // int GM2VM.GetStringId(string value)
        public static IEnumerable<CodeInstruction> GetStringId(IEnumerable<CodeInstruction> instructions)
        {
            var method_GetStringIdInner = AccessTools.Method(typeof(GMLCompilePatch), nameof(GetStringIdInner));

            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Call, method_GetStringIdInner);
            yield return new CodeInstruction(OpCodes.Ret);
        }

        private readonly static Dictionary<string, int> stringIds = new Dictionary<string, int>();

        /*
         * IMPLEMENTATION NOTES
         * The index/id of a particular string is never going to change at this stage in compilation.
         * Instead of O(n), use a dictionary and cache the id so future operations are O(log n) as
         * per Dictionary's implementation.
         * 
         * This change reliably brought compile times down from 68s -> 51s
         */
        public static int GetStringIdInner(string value)
        {
            int result;
            if (stringIds.TryGetValue(value, out result))
                return result;

            result = GML2VM_Strings.Count;
            stringIds.Add(value, result);
            GML2VM_Strings.Add(value);

            return result;
        }

        // GMLCompile

        // NOTE This is whatever the GMLCompile.Method is for compiling calls to extension functions and scripts
        //      I cannot for the life of me get this patch to apply because the slow code is a generic function GMAC-local type
        // public static int smethod_35(string string_2, out string string_3)
        /*
         * Hotspots based on high-rate sample profiling
         * 
         * Do not use foreach, enumerators take up 30% of runtime
         * Use for statements manually indexing the underlying IList
         * 
         * Structs for GMExtension data should be classes
         * 
         * foreach (Class14 class2 in gmextensionInclude.Functions)
		   {
		       if (class2.Name == string_2)

           Functions should be a dictionary, 18% runtime is spent doing this string comparison
           Part of the 30% enumerator runtime is spent here too

         * int num4 = GMLCompile.smethod_29<GMScript>(GMLCompile.gmassets_0.Scripts, string_2);
           if (num4 >= 0)
           {
               return num4 + 100000;
           }

           smethod_29 needs to be optimized with a cache of name -> script index


           if (GMLCompile.list_4.Contains(string_2))
           {
               result2 = -7;
           }

            GMLCompile.list_4 needs to be a HashSet, it's only ever used to check if something is defined
         */

        
        private readonly static Dictionary<string, int> scriptIds = new Dictionary<string, int>();

        /* IMPLEMENTATION NOTES
         * You need to cache this once you find it.
         * IList<KeyValuePair<string : resourceName, T : GMResource>> is not optimal for finding the
         * resource via ID (Index, it seems). Instead use a Dictionary, or at the very least have a
         * Dictionary to act as a lookup table for GMResource -> ID.
         * This builds that lookup table dynamically, but it should be made ahead of time
         * 
         * THIS IS THE HOTTEST METHOD IN THE GMAC, if the above improvements are any indication this
         * should bring compiles of 51s down to 30s or less based on estimates from dotTrace.
         */
        public static int GetScriptId(IList<KeyValuePair<string, object>> assets, string name)
        {
            int i;
            if (scriptIds.TryGetValue(name, out i))
                return i;

            i = 0;
            while (i < assets.Count && !(assets[i].Key == name))
                i++;

            var result = i >= assets.Count ? -1 : i;
            scriptIds.Add(name, result);
            return result;
        }

        // public static int smethod_29<T>(IList<KeyValuePair<string, T>> ilist_0, string string_2)
        /*
         * This entire method needs to be tossed, it's algorithmically O(N) and appears on multiple
         * hotpaths. This is the single slowest method in the entire GMAC.
         * 
         * The above is a non-generic version of this method
         */
    }
}
