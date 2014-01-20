﻿using System.Text.RegularExpressions;

namespace kOS.Command.BasicIO
{
    [Command("PRINT *")]
    public class CommandPrint : Command
    {
        public CommandPrint(Match regexMatch, ExecutionContext context) : base(regexMatch, context) { }

        public override void Evaluate()
        {
            var e = new Expression(RegexMatch.Groups[1].Value, ParentContext);

            if (e.IsNull())
            {
                StdOut("NULL");
                State = ExecutionState.DONE;
            }
            else
            {
                StdOut(e.ToString());
                State = ExecutionState.DONE;
            }
        }
    }
}