using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace Nimble.Ide
{
    public sealed class IdePlugin
    {
        public IdePlugin()
        {
            // Ignore me, this was for the ramdisk implementation which isn't done yet
        }

        public void Hook()
        {
            Console.WriteLine("Nimble IDE Hook");
        }

        private static IdePlugin instance;
        public static IdePlugin Instance => instance ?? (instance = new IdePlugin());
    }
}
