﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;

namespace LuaSTGEditorSharp.Zip
{
    public abstract class ZipCompressor
    {
        public abstract void PackByDict(Dictionary<string, string> path, bool removeIfExists);
        public abstract IEnumerable<string> PackByDictReporting(Dictionary<string, string> path, bool removeIfExists);
    }
}
