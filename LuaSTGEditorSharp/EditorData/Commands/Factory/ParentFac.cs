﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LuaSTGEditorSharp.EditorData.Commands.Factory
{
    /// <summary>
    /// Class to store insert as parent state of insert commands.
    /// </summary>
    public class ParentFac : CommandTypeFac
    {
        /// <summary>
        /// Create a new <see cref="InsertAsParentCommand"/> of target and new <see cref="TreeNode"/>.
        /// </summary>
        /// <param name="toOp">The target <see cref="TreeNode"/>.</param>
        /// <param name="toIns">The <see cref="TreeNode"/> to insert.</param>
        /// <returns>A new <see cref="InsertCommand"/>.</returns>
        public override InsertCommand NewInsert(TreeNode toOp, TreeNode toIns)
        {
            return new InsertAsParentCommand(toOp, toIns);
        }

        /// <summary>
        /// Validate the feasibility of insert if new <see cref="TreeNode"/> is parent and old is child.
        /// </summary>
        /// <param name="toOp">The target <see cref="TreeNode"/>.</param>
        /// <param name="toIns">The <see cref="TreeNode"/> to insert.</param>
        /// <returns>A <see cref="bool"/> value, true for can.</returns>
        public override bool ValidateType(TreeNode toOp, TreeNode toIns)
        {
            TreeNode toInsP = TreeNode.TryLink(toIns, toOp);
            bool a = toIns.ValidateChild(toOp) && toIns.Parent.ValidateChild(toIns);
            TreeNode.TryUnlink(toIns, toOp, toInsP);
            return a;
        }
    }
}
