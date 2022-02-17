﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using LuaSTGEditorSharp.EditorData.Document;
using LuaSTGEditorSharp.EditorData.Node.NodeAttributes;
using LuaSTGEditorSharp.EditorData.Node.General;
using Newtonsoft.Json;

namespace LuaSTGEditorSharp.EditorData.Node
{
    [Serializable, NodeIcon("setting.png")]
    [CannotDelete, CannotBan]
    [LeafNode]
    [RCInvoke(0)]
    //[XmlType(TypeName = "ProjSettings")]
    public class ProjSettings : TreeNode
    {
        [JsonConstructor]
        private ProjSettings() : base() { }

        public ProjSettings(DocumentData workSpaceData) 
            : this(workSpaceData, "", "", "true", "true", "4096") { }

        public ProjSettings(DocumentData workSpaceData, string name, string auth, string pr, string scpr, string modv) 
            : base(workSpaceData)
        {
            /*
            attributes.Add(new AttrItem("Output Name", name, this));
            attributes.Add(new AttrItem("Author", auth, this));
            attributes.Add(new AttrItem("Allow practice", pr, this, "bool"));
            attributes.Add(new AttrItem("Allow sc practice", scpr, this, "bool"));
            attributes.Add(new AttrItem("Mod version", modv, this));
            */
            
            OutputName = name;
            AllowPractice = pr;
            AllowSCPractice = scpr;
            Author = auth;
            ModVer = modv;
            
        }

        [JsonIgnore, NodeAttribute, XmlAttribute("OutputName")]
        //[DefaultValue("")]
        public string OutputName
        {
            get => DoubleCheckAttr(0, name: "Output Name").attrInput;
            set => DoubleCheckAttr(0, name: "Output Name").attrInput = value;
        }

        [JsonIgnore, NodeAttribute, XmlAttribute("Author")]
        //[DefaultValue("")]
        public string Author
        {
            get => DoubleCheckAttr(1).attrInput;
            set => DoubleCheckAttr(1).attrInput = value;
        }

        [JsonIgnore, NodeAttribute, XmlAttribute("AllowPractice")]
        //[DefaultValue("true")]
        public string AllowPractice
        {
            get => DoubleCheckAttr(2, "bool", "Allow practice").attrInput;
            set => DoubleCheckAttr(2, "bool", "Allow practice").attrInput = value;
        }

        [JsonIgnore, NodeAttribute, XmlAttribute("AllowSCPractice")]
        //[DefaultValue("true")]
        public string AllowSCPractice
        {
            get => DoubleCheckAttr(3, "bool", "Allow sc practice").attrInput;
            set => DoubleCheckAttr(3, "bool", "Allow sc practice").attrInput = value;
        }

        [JsonIgnore, NodeAttribute, XmlAttribute("AllowSCPractice")]
        //[DefaultValue("4096")]
        public string ModVer
        {
            get => DoubleCheckAttr(4, name: "Mod version").attrInput;
            set => DoubleCheckAttr(4, name: "Mod version").attrInput = value;
        }

        public override string ToString()
        {
            return "Project Settings";
        }

        public override IEnumerable<string> ToLua(int spacing)
        {
            string modvera = Macrolize(4) == "" ? "4096" : Macrolize(4);
            yield return "-- Generated by LuaSTG Editor Sharp X 0.74.1\n-- Mod name: " + NonMacrolize(0) + "\n_author = \"" +
                Macrolize(1) + "\"\n_mod_version = " + modvera + "\n_allow_practice = " + Macrolize(2) +
                "\n_allow_sc_practice = " + Macrolize(3) + "\n";
        }

        public override IEnumerable<Tuple<int,TreeNode>> GetLines()
        {
            yield return new Tuple<int, TreeNode>(7, this);
        }

        public override object Clone()
        {
            string modvera = NonMacrolize(4) == "" ? "4096" : NonMacrolize(4);
            var n = new Comment(parentWorkSpace, "Mod name: " + attributes[0].AttrInput + "\nAuthor: " +
            attributes[1].AttrInput + "\nMod version: " + modvera + "\nAllow Practice: " + attributes[2].AttrInput +
            "\nAllow Spell Card Practice: " + attributes[3].AttrInput);
            n.FixAttrParent();
            n.FixChildrenParent();
            return n;
            //return MessageBox.Show("This node cannot be cloned.");
        }
    }
}
