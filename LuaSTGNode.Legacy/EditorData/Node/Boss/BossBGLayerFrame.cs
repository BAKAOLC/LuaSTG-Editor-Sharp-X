﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LuaSTGEditorSharp.EditorData.Document;
using LuaSTGEditorSharp.EditorData.Document.Meta;
using LuaSTGEditorSharp.EditorData.Node.NodeAttributes;
using Newtonsoft.Json;

namespace LuaSTGEditorSharp.EditorData.Node.Boss
{
    [Serializable, NodeIcon("bgframe.png")]
    [CannotDelete, CannotBan]
    [RequireParent(typeof(BossBGLayer)), Uniqueness]
    public class BossBGLayerFrame : TreeNode
    {
        [JsonConstructor]
        private BossBGLayerFrame() : base() { }

        public BossBGLayerFrame(DocumentData workSpaceData) : base(workSpaceData) { }

        public override IEnumerable<string> ToLua(int spacing)
        {
            string sp = Indent(spacing);
            string s1 = Indent(1);
            yield return sp + "function(self)\n" 
                + sp + s1 + "task.Do(self)\n";
            foreach (var a in base.ToLua(spacing + 1))
            {
                yield return a;
            }
            yield return sp + "end,\n";
        }

        public override IEnumerable<Tuple<int, TreeNode>> GetLines()
        {
            yield return new Tuple<int, TreeNode>(2, this);
            foreach (Tuple<int, TreeNode> t in GetChildLines())
            {
                yield return t;
            }
            yield return new Tuple<int, TreeNode>(1, this);
        }

        public override string ToString()
        {
            return "on frame";
        }

        public override object Clone()
        {
            var n = new BossBGLayerFrame(parentWorkSpace);
            n.DeepCopyFrom(this);
            return n;
        }
    }
}
