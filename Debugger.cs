using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace KizhiPart3
{
    public class Debugger
    {
        private bool _isSourceCodeStarts;

        private readonly Interpreter _interpreter;

        // Изменил List на HashSet, так как часто нужно проверять, есть ли брейкпоинт на текущей строке.
        // У HashSet метод Contains работает за O(1), у List работает за O(list.Count)
        private readonly HashSet<int> _breakPoints = new HashSet<int>();

        private readonly TextWriter _writer;

        public Debugger(TextWriter writer)
        {
            _writer = writer;
            _interpreter = new Interpreter(writer);
        }

        public void ExecuteLine(string command)
        {
            if (string.IsNullOrEmpty(command)) return;

            if (_isSourceCodeStarts)
            {
                _interpreter.SetCode(command);
                _isSourceCodeStarts = false;
            }

            switch (command)
            {
                case "set code":
                    _isSourceCodeStarts = true;
                    break;

                case "run":
                    Run();
                    break;
                case "step over":
                    StepOver();
                    break;
                case "step":
                    Step();
                    break;

                case "print mem":
                    _interpreter.PrintVarsToWriter();
                    break;
                case "print trace":
                    PrintStackTraceToWriter();
                    break;
            }

            if (command.StartsWith("add break"))
            {
                AddBreakPoint(command);
            }

            if (_interpreter.IsCodeEnd || !_interpreter.IsPreviousCommandExecuted)
                _interpreter.Reset();
        }

        private void Run()
        {
            do
            {
                Step();
            } while (_interpreter.IsPreviousCommandExecuted &&
                     !_interpreter.IsCodeEnd && !_breakPoints.Contains(_interpreter.CurrentLineNumber));
        }

        private void Step()
        {
            _interpreter.ParseCurrentLineOfCodeAndExecute();
        }

        private void StepOver()
        {
            var functionsCountBeforeStep = _interpreter.CallStack.Count;
            Step();
            var functionsCountAfterStep = _interpreter.CallStack.Count;

            if (functionsCountAfterStep > functionsCountBeforeStep)
            {
                StepOverCalledFunction();
            }
        }

        private void StepOverCalledFunction()
        {
            var currentFunction = _interpreter.CallStack.Peek();
            var lineAfterCallLine = currentFunction.CallLine + 1;

            // Чтобы StepOver останавливался на брейкпоинтах перешагиваемой функции, 
            // нужно в логическое выражение добавить "&& !_breakPoints.Contains(_interpreter.CurrentLineNumber)"
            while (_interpreter.IsPreviousCommandExecuted && _interpreter.CurrentLineNumber != lineAfterCallLine)
            {
                Step();
            }
        }

        public void PrintStackTraceToWriter()
        {
            foreach (var (functionName, callLine) in _interpreter.CallStack)
            {
                _writer.WriteLine($"{callLine} {functionName}");
            }
        }

        private void AddBreakPoint(string command)
        {
            var commandParts = command.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            var lineNumberOfBreakPoint = commandParts[2];
            _breakPoints.Add(int.Parse(lineNumberOfBreakPoint));
        }
    }

    public class Interpreter
    {
        public readonly Stack<(string FunctionName, int CallLine)> CallStack = new Stack<(string, int)>();

        public bool IsPreviousCommandExecuted => _commandExecutor.IsPreviousCommandExecuted;

        public int CurrentLineNumber { get; private set; }
        public bool IsCodeEnd => _codeLines != null && !IsCurrentLineInsideCode && CallStack.Count == 0;

        private string[] _codeLines;
        private bool IsCurrentLineInsideCode => CurrentLineNumber < _codeLines.Length;
        private string CurrentLineOfCode => _codeLines[CurrentLineNumber];

        private readonly Dictionary<string, int> _functionNameToDefinitionLine = new Dictionary<string, int>();

        private Command _commandForExecute;
        private readonly CommandExecutor _commandExecutor;

        public Interpreter(TextWriter writer)
        {
            _commandExecutor = new CommandExecutor(writer);
        }

        public void SetCode(string code)
        {
            _codeLines = code.Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
            FindAllFunctionDefinitions();


            void FindAllFunctionDefinitions()
            {
                while (IsCurrentLineInsideCode)
                {
                    if (CurrentLineOfCode.StartsWith("def"))
                    {
                        var currentLineParts =
                            CurrentLineOfCode.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
                        var functionName = currentLineParts[1];
                        _functionNameToDefinitionLine[functionName] = CurrentLineNumber;
                    }

                    CurrentLineNumber++;
                }

                CurrentLineNumber = 0;
            }
        }

        public void ParseCurrentLineOfCodeAndExecute()
        {
            ParseCurrentCodeLine();

            if (_commandForExecute == null) return;

            _commandExecutor.Execute(_commandForExecute);
            _commandForExecute = null;
        }

        private void ParseCurrentCodeLine()
        {
            if (CallStack.Count != 0)
                ParseCurrentLineOfFunction();
            else
                ParseCommandOfCurrentLine();
        }
        
        private void ParseCommandOfCurrentLine()
        {
            var currentLineParts = CurrentLineOfCode.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            var commandName = currentLineParts[0];

            switch (commandName)
            {
                case "def":
                    SkipFunctionDefinition();
                    break;
                case "call":
                    PushFunctionToStackAndGoToDefinition();
                    break;
                default:
                    _commandForExecute = CreateCommandFromCurrentLineParts();
                    CurrentLineNumber++;
                    break;
            }


            void SkipFunctionDefinition()
            {
                CurrentLineNumber++;

                while (IsCurrentLineInsideCode && CurrentLineOfCode.StartsWith("    "))
                    CurrentLineNumber++;
            }

            void PushFunctionToStackAndGoToDefinition()
            {
                var functionName = currentLineParts[1];
                CallStack.Push((functionName, CurrentLineNumber));
                CurrentLineNumber = _functionNameToDefinitionLine[functionName];
            }

            Command CreateCommandFromCurrentLineParts()
            {
                if (currentLineParts.Length <= 2)
                    return new Command(currentLineParts[0], currentLineParts[1], CurrentLineNumber);

                var commandValue = int.Parse(currentLineParts[2]);
                return new CommandWithValue(currentLineParts[0], currentLineParts[1], commandValue,
                    CurrentLineNumber);
            }
        }

        private void ParseCurrentLineOfFunction()
        {
            var currentFunctionName = CallStack.Peek().FunctionName;
            var currentFunctionDefinitionLine = _functionNameToDefinitionLine[currentFunctionName];

            if (CurrentLineNumber == currentFunctionDefinitionLine)
            {
                CurrentLineNumber++;
            }
            else if (IsCurrentLineInsideCode && CurrentLineOfCode.StartsWith("    "))
            {
                ParseCommandOfCurrentLine();
            }
            else
            {
                var executedFunction = CallStack.Pop();
                CurrentLineNumber = executedFunction.CallLine + 1;
            }
        }

        public void PrintVarsToWriter()
        {
            _commandExecutor.PrintVariablesToWriter();
        }

        public void Reset()
        {
            CurrentLineNumber = 0;
            _commandExecutor.Reset();
        }
    }

    internal class Command
    {
        public string Name { get; }
        public string VariableName { get; }
        public int Line { get; }

        public Command(string name, string variableName, int line)
        {
            Name = name;
            VariableName = variableName;
            Line = line;
        }

        // для удобства при дебаге
        public override string ToString() => $"{Name} {VariableName}";
    }

    internal class CommandWithValue : Command
    {
        public int Value { get; }

        public CommandWithValue(string name, string variableName, int value, int line)
            : base(name, variableName, line)
        {
            Value = value;
        }

        public override string ToString() => $"{Name} {VariableName} {Value}";
    }

    internal class CommandExecutor
    {
        public bool IsPreviousCommandExecuted { get; private set; } = true;

        private readonly VariablesStorage _variablesStorage = new VariablesStorage();

        private readonly TextWriter _writer;


        public CommandExecutor(TextWriter writer)
        {
            _writer = writer;
        }

        public void Execute(Command command)
        {
            if (command.Name != "set" && !MemoryContainsVariableWithName(command.VariableName))
            {
                IsPreviousCommandExecuted = false;
                return;
            }

            if (command is CommandWithValue commandWithValue)
                ExecuteCommandWithValue(commandWithValue);
            else
                ExecuteCommand(command);
        }

        private void ExecuteCommandWithValue(CommandWithValue commandWithValue)
        {
            switch (commandWithValue.Name)
            {
                case "set":
                    _variablesStorage.SetValueOfVariableWithName(commandWithValue.VariableName, commandWithValue.Value);
                    break;
                case "sub":
                    var currentValue = _variablesStorage.GetValueOfVariableWithName(commandWithValue.VariableName);
                    var valueAfterSub = currentValue - commandWithValue.Value;
                    _variablesStorage.SetValueOfVariableWithName(commandWithValue.VariableName, valueAfterSub);
                    break;
            }

            _variablesStorage.SetLastChangeLineOfVariableWithName(commandWithValue.VariableName, commandWithValue.Line);
        }

        private void ExecuteCommand(Command command)
        {
            switch (command.Name)
            {
                case "rem":
                    _variablesStorage.RemoveVariableWithName(command.VariableName);
                    break;
                case "print":
                    var value = _variablesStorage.GetValueOfVariableWithName(command.VariableName);
                    _writer.WriteLine(value);
                    break;
            }
        }

        private bool MemoryContainsVariableWithName(string variableName)
        {
            if (_variablesStorage.ContainsVariableWithName(variableName)) return true;

            _writer.WriteLine("Переменная отсутствует в памяти");
            return false;
        }

        public void PrintVariablesToWriter()
        {
            var variablesNames = _variablesStorage.GetNamesOfVariablesInMemory();
            foreach (var variableName in variablesNames)
            {
                var variableValue = _variablesStorage.GetValueOfVariableWithName(variableName);
                var lastChangeLine = _variablesStorage.GetLastChangeLineOfVariableWithName(variableName);
                _writer.WriteLine($"{variableName} {variableValue} {lastChangeLine}");
            }
        }

        public void Reset()
        {
            IsPreviousCommandExecuted = true;
            _variablesStorage.Clear();
        }
    }

    internal class VariablesStorage
    {
        private readonly Dictionary<string, int> _variableNameToValue = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _variableNameToLastChangeLine = new Dictionary<string, int>();

        public string[] GetNamesOfVariablesInMemory()
        {
            return _variableNameToValue.Keys.ToArray();
        }

        public bool ContainsVariableWithName(string variableName)
        {
            return _variableNameToValue.ContainsKey(variableName);
        }

        public int GetValueOfVariableWithName(string variableName)
        {
            return _variableNameToValue[variableName];
        }

        public void SetValueOfVariableWithName(string variableName, int value)
        {
            if (value <= 0)
                throw new ArgumentException("Значениями переменных могут быть только натуральные числа");

            _variableNameToValue[variableName] = value;
        }

        public void RemoveVariableWithName(string variableName)
        {
            _variableNameToValue.Remove(variableName);
            _variableNameToLastChangeLine.Remove(variableName);
        }

        public void SetLastChangeLineOfVariableWithName(string variableName, int lastChangeLine)
        {
            _variableNameToLastChangeLine[variableName] = lastChangeLine;
        }

        public int GetLastChangeLineOfVariableWithName(string variableName)
        {
            return _variableNameToLastChangeLine[variableName];
        }

        public void Clear()
        {
            _variableNameToValue.Clear();
            _variableNameToLastChangeLine.Clear();
        }
    }
}