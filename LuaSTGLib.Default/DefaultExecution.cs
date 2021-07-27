﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using LuaSTGEditorSharp.Execution;

namespace LuaSTGEditorSharp.Plugin.Default
{
    class DefaultExecution : LSTGExecution
    {
        public override void BeforeRun(ExecutionConfig config) { }
        public override void Run(Logger logger, Action end) { logger("You cannot run without a target LuaSTG version."); }
        protected override string LogFileName => "log.txt";
        public override string ExecutableName => "LuaSTGPlus.dev.exe";
    }
}
