using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace KizhiPart3
{
    [TestFixture]
    public class DebuggerTests
    {
        private readonly string[] _commandsSeparator = {"\r\n"};

        void TestDebugger(string[] commands, string[] expectedOutput)
        {
            string[] actualOutput;
            using (var sw = new StringWriter())
            {
                var debugger = new Debugger(sw);

                foreach (var command in commands)
                {
                    debugger.ExecuteLine(command);
                }

                actualOutput = sw.ToString().Split(_commandsSeparator, StringSplitOptions.RemoveEmptyEntries);
            }

            Assert.AreEqual(expectedOutput.Length, actualOutput.Length);
            Assert.AreEqual(expectedOutput, actualOutput);
        }

        [Test]
        public void SeveralCommandsAndRun()
        {
            TestDebugger(new[]
            {
                "set code",

                "set a 10\n" +
                "print a\n" +
                "print a\n" +
                "sub a 4\n" +
                "print a\n" +
                "rem a",

                "end code",
                "run"
            }, new[] {"10", "10", "6"});
        }

        [Test]
        public void SeveralCommandsAndSteps()
        {
            TestDebugger(new[]
                {
                    "set code",
    
                    "set a 10\n" +
                    "print a\n" +
                    "print a\n" +
                    "sub a 4\n" +
                    "print a\n" +
                    "rem a",

                    "end code",
                }.Concat(Enumerable.Repeat("step", 6)).ToArray(),
                new[] {"10", "10", "6"});
        }

        [Test]
        public void DontExecuteAfterNotFound()
        {
            TestDebugger(new[]
            {
                "set code",

                "print a\n" +
                "set a 5\n" +
                "print a",

                "end code",
                "run"
            }, new[] {"Переменная отсутствует в памяти"});
        }

        [Test]
        public void SimpleFunction()
        {
            TestDebugger(new[]
            {
                "set code",

                "def myfunc\n" +
                "    set a 10\n" +
                "    print a\n" +
                "call myfunc\n" +
                "print a",

                "end code",
                "run"
            }, new[] {"10", "10"});
        }

        [Test]
        public void FunctionFromExample()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 10\n" +
                "    sub a 3\n" +
                "    print b\n" +
                "set b 7\n" +
                "call test",

                "end code",
                "run"
            }, new[] {"7"});
        }

        [Test]
        public void FunctionCallBeforeDefinition()
        {
            TestDebugger(new[]
            {
                "set code",

                "call test\n" +
                "print a\n" +
                "def test\n" +
                "    set a 5",

                "end code",
                "run"
            }, new[] {"5"});
        }

        [Test]
        public void VariableValueIsZero()
        {
            Assert.Throws<ArgumentException>(() =>
                TestDebugger(new[]
                {
                    "set code",

                    "set m 0",

                    "end code",
                    "run"
                }, new string[0])
            );
        }

        [Test]
        public void FunctionCallFunction()
        {
            TestDebugger(new[]
            {
                "set code",
                "def one\n" +
                "    set a 20\n" +
                "    sub a 5\n" +
                "    call two\n" +
                "    print a\n" +
                "def two\n" +
                "    sub a 5\n" +
                "    sub a 5\n" +
                "call one",
                "end code",
                "run"
            }, new[] {"5"});
        }

        [Test]
        public void RunAndRunAfterResetBuffers()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 5\n" +
                "    sub a 3\n" +
                "    print a\n" +
                "call test",

                "end code",
                "run",
                "run"
            }, new[] {"2", "2"});
        }

        [Test]
        public void RunCodeWithErrorAndRunAfterResetBuffers()
        {
            TestDebugger(new[]
                {
                    "set code",

                    "sub a\n" +
                    "set a 4\n" +
                    "print a",

                    "end code",
                    "run",
                    "run"
                },
                new[] {"Переменная отсутствует в памяти", "Переменная отсутствует в памяти"});
        }

        [Test]
        public void RunAndRunAfterResetBuffersWithPrintMem()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 5\n" +
                "    sub a 3\n" +
                "    print a\n" +
                "call test",

                "end code",

                "add break 2",

                "run",
                "print mem",
                "run",

                "run",
                "print mem",
                "run"
            }, new[] {"a 5 1", "2", "a 5 1", "2"});
        }

        [Test]
        public void BreakPointStopsExecution()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 5\n" +
                "    print a\n" +
                "    sub a 3\n" +
                "    print a\n" +
                "call test",

                "end code",

                "add break 3",

                "run"
            }, new[] {"5"});
        }

        [Test]
        public void PrintMem()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 5\n" +
                "    sub a 3\n" +
                "    print a\n" +
                "call test",

                "end code",

                "add break 2",

                "run",

                "print mem"
            }, new[] {"a 5 1"});
        }

        [Test]
        public void PrintMemOnBreakPoint()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 5\n" +
                "    sub a 3\n" +
                "    print a\n" +
                "call test",

                "end code",

                "add break 2",

                "run",

                "print mem",

                "run"
            }, new[] {"a 5 1", "2"});
        }

        [Test]
        public void PrintTraceOfTwoFunctions()
        {
            TestDebugger(new[]
            {
                "set code",

                "def one\n" +
                "    set a 15\n" +
                "    sub a 5\n" +
                "    call two\n" +
                "    print a\n" +
                "def two\n" +
                "    sub a 5\n" +
                "    sub a 5\n" +
                "call one",

                "end code",

                "add break 7",

                "run",

                "print trace"
            }, new[] {"3 two", "8 one"});
        }

        [Test]
        public void PrintTraceOnEveryFunctionLine()
        {
            TestDebugger(new[]
            {
                "set code",
                "def trace\n" +
                "    set a 5\n" +
                "    sub a 3\n" +
                "    print a\n" +
                "call trace\n",

                "end code",

                "add break 0", "add break 1", "add break 2", "add break 3",

                "run", "print trace",
                "run", "print trace",
                "run", "print trace",
                "run", "print trace",

                "run",
            }, new[] {"4 trace", "4 trace", "4 trace", "4 trace", "2"});
        }


        [Test]
        public void StepOverSimpleFunc()
        {
            TestDebugger(new[]
            {
                "set code",

                "def myfunc\n" +
                "    set a 10\n" +
                "    print a\n" +
                "call myfunc\n" +
                "end code",

                "add break 3", "run",

                "step over"
            }, new[] {"10"});
        }

        [Test]
        public void StepOverInFuncBody()
        {
            TestDebugger(new[]
            {
                "set code",

                "def one\n" +
                "    set a 20\n" +
                "    sub a 5\n" +
                "    call two\n" +
                "    print a\n" +
                "def two\n" +
                "    sub a 5\n" +
                "    print a\n" +
                "    sub a 5\n" +
                "call one",

                "end code",

                "add break 3", "run",

                "step over", "print trace",

                "run"
            }, new[] {"10", "9 one", "5"});
        }

        //В перешагиваемой функции брейкпоинты игнорируются
        [Test]
        public void StepOverAndBreakPoint()
        {
            TestDebugger(new[]
            {
                "set code",

                "def one\n" +
                "    set a 20\n" +
                "    sub a 5\n" +
                "    call two\n" +
                "    print a\n" +
                "def two\n" +
                "    sub a 5\n" +
                "    print a\n" +
                "    sub a 5\n" +
                "call one",

                "end code",

                "add break 3", "run",

                "add break 7", "step over", "print trace"
            }, new[] {"10", "9 one"});
        }


        [Test]
        public void CallOneFunctionOnDifferentLines()
        {
            TestDebugger(new[]
            {
                "set code",

                "def test\n" +
                "    set a 4\n" +
                "set b 5\n" +
                "call test\n" +
                "sub a 3\n" +
                "call test\n" +
                "print a\n",

                "end code",

                "add break 1", "run",

                "print trace", "run",
                "print trace", "run"
            }, new[] {"3 test", "5 test", "4"});
        }

        [Test]
        public void NullCommand()
        {
            TestDebugger(new[]
            {
                "set code",

                null,

                "end code",
            }, new string[0]);
        }

        //WORKS
        // [Test]
        // public void Recursive()
        // {
        //     TestDebugger(new[]
        //     {
        //         "set code",
        //         
        //         "def printfunc\n" +
        //         "    print a\n" +
        //         "    call printfunc\n" +
        //         "set a 5\n" +
        //         "call printfunc\n",
        //         
        //         "end code",
        //         "run"
        //     }, new string[0]);
        // }
    }
}