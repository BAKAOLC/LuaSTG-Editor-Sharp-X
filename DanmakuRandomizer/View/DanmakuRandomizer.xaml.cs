﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using DanmakuRandomizer.Model;

namespace DanmakuRandomizer.View
{
    public partial class DanmakuRandomizer : Window
    {
        public LuaSTGEditorSharp.EditorData.DocumentData Document { get; set; }

        public LuaSTGEditorSharp.EditorData.TreeNode Nodes { get; private set; }

        public DanmakuRandomizer()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Nodes = new RandomDanmaku() { Depth = 10 }.Randomize().GetTreeNodes(null);
            this.Close();
        }
    }
}
