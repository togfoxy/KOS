﻿using System;
using System.Collections.Generic;
using kOS.Debug;

namespace kOS.Context
{
    public sealed class ImmediateMode : ExecutionContext
    {
        private int cursor;
        private int baseLineY;
        private const int CMD_BACKLOG = 20;
        private readonly List<String> previousCommands = new List<String>();
        private int prevCmdIndex = -1;
        private String inputBuffer = "";
        private String commandBuffer = "";
        private int cursorX;
        private int cursorY;
        private readonly Queue<Command.Command> queue = new Queue<Command.Command>();

        private readonly char[,] buffer = new char[COLUMNS, ROWS];

        public ImmediateMode(ExecutionContext parent) : base(parent) 
        {
            StdOut("kOS Operating System");
            StdOut("KerboScript v" + Core.VersionInfo);
            StdOut("");
            StdOut("Proceed.");
        }

        public void Add(string cmdString)
        {
            commandBuffer += cmdString;
            string nextCmd;

            var line = 0;
            int comandLineStart;
            while (ParseNext(ref commandBuffer, out nextCmd, ref line, out comandLineStart))
            {
                try
                {
                    var cmd = Command.Command.Get(nextCmd, this, comandLineStart);
                    queue.Enqueue(cmd);
                }
                catch (kOSException e)
                {
                    StdOut(e.Message);
                    queue.Clear(); // HALT!!
                }
            }
        }

        public override int GetCursorX()
        {
            return ChildContext != null ? ChildContext.GetCursorX() : cursorX;
        }

        public override int GetCursorY()
        {
            return ChildContext != null ? ChildContext.GetCursorY() : cursorY;
        }

        public override bool Type(char ch)
        {
            if (base.Type(ch)) return true;

            switch (ch)
            {
                case (char)8:
                    if (cursor > 0)
                    {
                        inputBuffer = inputBuffer.Remove(cursor - 1, 1);
                        cursor--;
                    }
                    break;

                case (char)13:
                    Enter();
                    break;

                default:
                    inputBuffer = inputBuffer.Insert(cursor, new String(ch, 1));
                    cursor++;
                    break;
            }

            UpdateCursorXY();
            return true;
        }

        public override char[,] GetBuffer()
        {
            var childBuffer = (ChildContext != null) ? ChildContext.GetBuffer() : null;

            return childBuffer ?? buffer;
        }

        public void UpdateCursorXY()
        {
            cursorX = cursor % buffer.GetLength(0);
            cursorY = (cursor / buffer.GetLength(0)) + baseLineY;
        }

        public void ShiftUp()
        {
            for (var y = 0; y < buffer.GetLength(1); y++)
            {
                for (var x = 0; x < buffer.GetLength(0); x++)
                {
                    if (y + 1 < buffer.GetLength(1))
                    {
                        buffer[x, y] = buffer[x, y + 1];
                    }
                    else
                    {
                        buffer[x, y] = (char)0;
                    }
                }
            }

            for (int x = 0; x < buffer.GetLength(0); x++)
            {
                buffer[x, buffer.GetLength(1) - 1] = (char)0;
            }

            if (baseLineY > 0) baseLineY--;

            UpdateCursorXY();
        }

        public override void Put(string text, int x, int y)
        {
            foreach (char c in text)
            {
                if (x >= buffer.GetLength(0) || y >= buffer.GetLength(1)) return;

                buffer[x, y] = c;
                x++;
            }
        }

        public override void StdOut(string line)
        {
            int linesWritten = WriteLine(line);
            baseLineY += linesWritten;
            UpdateCursorXY();
        }

        public void ClearScreen()
        {
            baseLineY = 0;
            cursor = 0;

            for (int y = 0; y < buffer.GetLength(1); y++)
            for (int x = 0; x < buffer.GetLength(0); x++)
            {
                buffer[x, y] = (char)0;
            }

            UpdateCursorXY();
        }

        public int WriteLine(string line)
        {
            int lineCount = (line.Length / buffer.GetLength(0)) + 1;

            while (baseLineY + lineCount > buffer.GetLength(1))
            {
                ShiftUp();
            }

            for (int y = baseLineY; y < buffer.GetLength(1); y++)
            for (int x = 0; x < buffer.GetLength(0); x++)
            {
                buffer[x, y] = (char)0;
            }

            char[] inputChars = line.ToCharArray();

            int writeX = 0;
            int writeY = baseLineY;
            foreach (char c in inputChars)
            {
                buffer[writeX, writeY] = c;

                writeX++;
                if (writeX >= buffer.GetLength(0)) { writeX = 0; writeY++; }
            }

            return lineCount;
        }

        public override void Update(float time)
        {
            if (ChildContext == null)
            {
                if (queue.Count > 0)
                {
                    var currentCmd = queue.Dequeue();

                    try
                    {
                        Push(currentCmd);
                        currentCmd.Evaluate();
                    }
                    catch (kOSException e)
                    {
                        StdOut(e.Message);
                        queue.Clear();          // Halt all pending instructions
                        ChildContext = null;    //
                    }
                    catch (Exception e)
                    {
                        // Non-kos exception! This is a bug, but no reason to kill the OS
                        StdOut("Flagrant error occured, logging");

                        if (CPU.RunType == CPU.kOSRunType.KSP)
                        {
                            UnityEngine.Debug.Log("Immediate mode error");
                            UnityEngine.Debug.Log(e);
                        }

                        queue.Clear();
                        ChildContext = null;
                    }
                }
                else
                {
                    WriteLine(inputBuffer);
                }
            }

            try
            {
                base.Update(time);
            }
            catch (kOSException e)
            {
                StdOut(e.Message);
                ChildContext = null;
                queue.Clear();          // Halt all pending instructions
            }
            catch (Exception e)
            {
                // Non-kos exception! This is a bug, but no reason to kill the OS
                StdOut("Flagrant error occured, logging");

                if (CPU.RunType == CPU.kOSRunType.KSP)
                {
                    UnityEngine.Debug.Log("Immediate mode error");
                    UnityEngine.Debug.Log(e);
                }

                queue.Clear();
                ChildContext = null;
            }
        }

        private void Enter()
        {
            baseLineY += WriteLine(inputBuffer);

            while (baseLineY > buffer.GetLength(1) - 1) ShiftUp();

            
            previousCommands.Add(inputBuffer);
            if (previousCommands.Count > CMD_BACKLOG)
            {
                var overflow = previousCommands.Count - CMD_BACKLOG;
                previousCommands.RemoveRange(0, overflow);
            }

            prevCmdIndex = -1;

            Add(inputBuffer += "\n");

            inputBuffer = "";
            cursor = 0;

            UpdateCursorXY();
        }

        public override void SendMessage(SystemMessage message)
        {
            switch (message)
            {
                case SystemMessage.CLEARSCREEN:
                    ClearScreen();
                    break;

                default:
                    base.SendMessage(message);
                    break;
            }
        }

        public void PreviousCommand(int direction)
        {
            if (previousCommands.Count == 0) return;

            inputBuffer = "";
            cursor = 0;

            prevCmdIndex += direction;
            if (prevCmdIndex <= -1)
            {
                inputBuffer = "";
                prevCmdIndex = -1;
                cursor = 0;
                UpdateCursorXY();
                return;
            }
            if (prevCmdIndex > previousCommands.Count-1)
            {
                prevCmdIndex = previousCommands.Count - 1;
            }
            
            inputBuffer = previousCommands[(previousCommands.Count-1) - prevCmdIndex];
            cursor = inputBuffer.Length;
            UpdateCursorXY();
        }

        public override bool SpecialKey(kOSKeys key)
        {
            if (base.SpecialKey(key)) return true;

            switch (key)
            {
                case kOSKeys.UP:
                    PreviousCommand(1);
                    return true;

                case kOSKeys.DOWN:
                    PreviousCommand(-1);
                    return true;

                case kOSKeys.LEFT:
                    if (cursor > 0)
                    {
                        cursor--;
                        UpdateCursorXY();
                    }
                    return true;

                case kOSKeys.RIGHT:
                    if (cursor < inputBuffer.Length)
                    {
                        cursor++;
                        UpdateCursorXY();
                    }
                    return true;
            }

            return false;
        }
    }
}
