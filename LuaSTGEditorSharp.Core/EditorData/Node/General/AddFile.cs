﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LuaSTGEditorSharp.EditorData;
using LuaSTGEditorSharp.EditorData.Document;
using LuaSTGEditorSharp.EditorData.Document.Meta;
using LuaSTGEditorSharp.EditorData.Node.NodeAttributes;
using Newtonsoft.Json;

namespace LuaSTGEditorSharp.EditorData.Node.General
{
    [Serializable, NodeIcon("pack.png")]
    [ClassNode]
    [LeafNode]
    [CreateInvoke(0), RCInvoke(0)]
    class AddFile : TreeNode
    {
        [JsonConstructor]
        private AddFile() : base() { }

        public AddFile(DocumentData workSpaceData)
            : this(workSpaceData, "") { }

        public AddFile(DocumentData workSpaceData, string path)
            : base(workSpaceData)
        {
            Path = path;
        }

        [JsonIgnore, NodeAttribute]
        public string Path {
            get => DoubleCheckAttr(0, "plainFile", isDependency: true).attrInput;
            set => DoubleCheckAttr(0, "plainFile", isDependency: true).attrInput = value;
        }

        public override IEnumerable<string> ToLua(int spacing)
        {
            yield break;
        }

        public override IEnumerable<Tuple<int, TreeNode>> GetLines()
        {
            yield break;
        }

        public override string ToString()
        {
            return $"Add file \"{NonMacrolize(0)}\" into pack";
        }

        protected override void AddCompileSettings()
        {
            string sk = parentWorkSpace.CompileProcess.archiveSpace + System.IO.Path.GetFileName(NonMacrolize(0));
            if (!parentWorkSpace.CompileProcess.resourceFilePath.ContainsKey(NonMacrolize(0)))
            {
                parentWorkSpace.CompileProcess.resourceFilePath.Add(sk, attributes[0].AttrInput);
            }
        }

        public override object Clone()
        {
            var n = new AddFile(parentWorkSpace);
            n.DeepCopyFrom(this);
            return n;
        }
    }
}
