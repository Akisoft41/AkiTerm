//
// AkiTerm
// =======
//
// A serial terminal in command line mode.
//
// Run well in Windows Terminal
// https://github.com/microsoft/terminal
//
//
// v0.1 08.01.2020  first version
//


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Vanara.PInvoke;

// TODO:
// set Handshake: None, XOnXOff, RTS, RTS+XOnXOff
// set escape char
// set DtrEnable
// set RtsEnable
// set BreakState
// set Encoding
// set alias for function keys
// display in hexa

namespace AkiTerm
{
    class Program
    {
        static bool _terminalMode = false;
        static bool _quit = false;
        static SerialPort _serialPort;
        static HFILE _handleIn;
        static HFILE _handleOut;

        // Options
        static bool _backspaceasbs = true;
        //static bool _bsasdel = false;
        //static bool _delasbs = false;
        static bool _crlf = true;
        static string _escape = "\x001"; // CTRL-A
        static bool _localecho = false;


        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to AkiTerm, a serial terminal in command line mode.");
            Console.WriteLine();
            Console.WriteLine($"Escape Character is 'CTRL+A'");
            Console.WriteLine();

            _serialPort = new SerialPort
            {
                BaudRate = 115200,
                Parity = Parity.None,
                StopBits = StopBits.One,
                DataBits = 8,
                Handshake = Handshake.None,
                Encoding = System.Text.Encoding.UTF8
            };
            _serialPort.DataReceived += _serialPort_DataReceived;
            _serialPort.ErrorReceived += _serialPort_ErrorReceived;
            _serialPort.PinChanged += _serialPort_PinChanged;

            _handleIn = Kernel32.GetStdHandle(Kernel32.StdHandleType.STD_INPUT_HANDLE);
            _handleOut = Kernel32.GetStdHandle(Kernel32.StdHandleType.STD_OUTPUT_HANDLE);
            SetConsoleModeOut();

            if (args.Length > 0)
            {
                _serialPort.PortName = args[0];
                if (args.Length > 1 && int.TryParse(args[1], out int b)) _serialPort.BaudRate = b;
                _serialPort.Open();
                Console.WriteLine($"{_serialPort.PortName} opened at {_serialPort.BaudRate}bps.");
                _terminalMode = true;
            }

            while (!_quit)
            {
                try
                {
                    if (_terminalMode)
                    {
                        // Terminal Mode
                        SetConsoleModeInTerminal();
                        Terminal();
                    }
                    else
                    {
                        // Command Mode
                        SetConsoleModeInCommand();
                        Command();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.WriteLine();
                    Debug.WriteLine($"ERROR: {ex.Message}");
                    Debug.WriteLine($"{ex.StackTrace}");
                }


            }
        }

        private static void Command()
        {
            StringBuilder buf = new StringBuilder(256);

            while (!_quit && !_terminalMode)
            {
                try
                {
                    Console.Write("AkiTerm>");

                    //string cmd = Console.ReadLine();
                    //var cmds = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    buf.Clear();
                    var readSuccess = Kernel32.ReadConsole(_handleIn, buf, 256, out uint numRead);
                    if (!readSuccess) continue;
                    buf.Length = (int)numRead - 1;
                    var cmd = buf.ToString();

                    var cmds = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine();
                    if (cmds.Length == 0) continue;
                    Debug.WriteLine($"Command: {cmd}");

                    switch (cmds[0].ToLower())
                    {
                        case "st":
                        case "status":
                            if (_serialPort.IsOpen)
                            {
                                Console.WriteLine($"Connected to {_serialPort.PortName} {GetPortConfig()}");
                            }
                            else
                            {
                                Console.WriteLine($"Not Connected, default: {_serialPort.PortName} {GetPortConfig()}");
                            }
                            break;

                        case "l":
                        case "list":
                            Console.WriteLine("Comm Port list:");
                            var ports = new List<int>();
                            foreach (var port in System.IO.Ports.SerialPort.GetPortNames())
                            {
                                ports.Add(int.Parse(port.Substring(3)));
                            }
                            ports.Sort();
                            foreach (var port in ports) Console.WriteLine($"  COM{port}");
                            Console.WriteLine();
                            break;

                        case "o":
                        case "open":
                            if (!_serialPort.IsOpen)
                            {
                                if (cmds.Length > 1) _serialPort.PortName = cmds[1];
                                if (cmds.Length > 2 && int.TryParse(cmds[2], out int b)) _serialPort.BaudRate = b;
                                _serialPort.Open();
                                Console.WriteLine($"{_serialPort.PortName} opened at {GetPortConfig()}");
                            }
                            _terminalMode = true;
                            break;

                        case "c":
                        case "close":
                            if (_serialPort.IsOpen)
                            {
                                _serialPort.Close();
                                Console.WriteLine($"{_serialPort.PortName} closed.");
                            }
                            break;

                        case "d":
                        case "display":
                            Console.WriteLine("Escape Character is 'CTRL+A'");
                            if (_localecho) Console.WriteLine("Local echo on");
                            else Console.WriteLine("Local echo off");
                            if (_crlf) Console.WriteLine("New line mode - Causes return key to send CR & LF");
                            else Console.WriteLine("New line mode - Causes return key to send CR");
                            if (_backspaceasbs) Console.WriteLine("Backspace key will be sent as BS (\\b)");
                            else Console.WriteLine("Backspace key will be sent as delete (127)");
                            //if (_bsasdel) Console.WriteLine("Backspace will be sent as delete");
                            //if (_delasbs) Console.WriteLine("Delete will be sent as backspace");
                            Console.WriteLine();
                            break;

                        case "s":
                        case "set":
                            if (cmds.Length < 2)
                            {
                                Console.WriteLine("Invalid Command. Type 'set ?' for help");
                                break;
                            }
                            switch (cmds[1].ToLower())
                            {
                                case "?":
                                case "h":
                                case "help":
                                    Console.WriteLine(
                                        "baudrate x      Set Baud Rate at x\n" +
                                        "parity x        Set Parity, x: None, Odd, Even, Mark or Space\n" +
                                        "stopbits x      Set StopBits, x: 1, 1.5 or 2 bits\n" +
                                        //"databits x      Set DataBits, x: between 5 and 8 bits\n" +
                                        "backspaceasbs   Backspace key will be sent as BS (\\b)\n" +
                                        //"bsasdel         Backspace will be sent as delete\n" +
                                        //"delasbs         Delete will be sent as backspace\n" +
                                        "crlf            New line mode - Causes return key to send CR & LF\n" +
                                        //"escape x        x is an escape charater to enter telnet client prompt\n" +
                                        "localecho       Turn on localecho.\n");
                                    break;

                                case "b":
                                case "baudrate":
                                    if (cmds.Length < 3)
                                    {
                                        Console.WriteLine("Invalid Command. Type 'set ?' for help");
                                        break;
                                    }
                                    if (!int.TryParse(cmds[2], out int b))
                                    {
                                        Console.WriteLine($"Invalid Parameter: '{cmds[2]}'");
                                        break;
                                    }
                                    _serialPort.BaudRate = b;
                                    Console.WriteLine($"Baud Rate set to {_serialPort.BaudRate}");
                                    break;

                                case "p":
                                case "parity":
                                    if (cmds.Length < 3)
                                    {
                                        Console.WriteLine("Invalid Command. Type 'set ?' for help");
                                        break;
                                    }
                                    switch (cmds[2].ToLower())
                                    {
                                        case "n":
                                        case "none":
                                            _serialPort.Parity = Parity.None;
                                            break;
                                        case "o":
                                        case "odd":
                                            _serialPort.Parity = Parity.Odd;
                                            break;
                                        case "e":
                                        case "even":
                                            _serialPort.Parity = Parity.Even;
                                            break;
                                        case "m":
                                        case "mark":
                                            _serialPort.Parity = Parity.Mark;
                                            break;
                                        case "s":
                                        case "space":
                                            _serialPort.Parity = Parity.Space;
                                            break;
                                        default:
                                            Console.WriteLine("Invalid Parameter. Value are None, Odd, Even, Mark or Space");
                                            break;
                                    }
                                    Console.WriteLine($"Parity set to {_serialPort.Parity}");
                                    break;

                                case "s":
                                case "stopbits":
                                    if (cmds.Length < 3)
                                    {
                                        Console.WriteLine("Invalid Command. Type 'set ?' for help");
                                        break;
                                    }
                                    switch (cmds[2])
                                    {
                                        case "1":
                                            _serialPort.StopBits = StopBits.One;
                                            Console.WriteLine($"StopBits set to 1");
                                            break;
                                        case "1.5":
                                            _serialPort.StopBits = StopBits.OnePointFive;
                                            Console.WriteLine($"StopBits set to 1.5");
                                            break;
                                        case "2":
                                            _serialPort.StopBits = StopBits.Two;
                                            Console.WriteLine($"StopBits set to 2");
                                            break;
                                        default:
                                            Console.WriteLine($"Invalid StopBits Parameter: '{cmds[2]}' need 0, 1, 1.5 or 2");
                                            break;
                                    }
                                    break;

                                case "d":
                                case "databits":
                                    if (cmds.Length < 3)
                                    {
                                        Console.WriteLine("Invalid Command. Type 'set ?' for help");
                                        break;
                                    }
                                    if (!int.TryParse(cmds[2], out int d) || d < 5 || d > 8)
                                    {
                                        Console.WriteLine($"Invalid DataBits Parameter: '{cmds[2]}' need between 5 and 8");
                                        break;
                                    }
                                    _serialPort.DataBits = d;
                                    Console.WriteLine($"DataBits set to {_serialPort.DataBits}");
                                    break;

                                case "bs":
                                case "backspaceasbs":
                                    _backspaceasbs = true;
                                    Console.WriteLine("Backspace key will be sent as BS (\\b)");
                                    break;

                                //case "bs":
                                //case "bsasdel":
                                //    _bsasdel = true;
                                //    Console.WriteLine("Backspace will be sent as delete");
                                //    break;

                                //case "del":
                                //case "delasbs":
                                //    _delasbs = true;
                                //    Console.WriteLine("Delete will be sent as backspace");
                                //    break;

                                case "crlf":
                                    _crlf = true;
                                    Console.WriteLine("New line mode - Causes return key to send CR & LF");
                                    break;

                                case "e":
                                case "localecho":
                                    _localecho = true;
                                    Console.WriteLine("Local echo on");
                                    break;

                                default:
                                    Console.WriteLine("Invalid Command. Type 'set ?' for help");
                                    break;
                            }
                            break;

                        case "u":
                        case "unset":
                            if (cmds.Length < 2)
                            {
                                Console.WriteLine("Invalid Command. Type ?/help for help");
                                break;
                            }
                            switch (cmds[1].ToLower())
                            {
                                case "?":
                                case "h":
                                case "help":
                                    Console.WriteLine(
                                        "backspaceasbs   Backspace key will be sent as DEL (127)\n" +
                                        //"bsasdel         Backspace will be sent as backspace\n" +
                                        //"delasbs         Delete will be sent as delete\n" +
                                        "crlf            Line feed mode - Causes return key to send CR\n" +
                                        "escape          No escape character is used\n" +
                                        "localecho       Turn off localecho.\n");
                                    break;

                                case "bs":
                                case "backspaceasbs":
                                    _backspaceasbs = false;
                                    Console.WriteLine("Backspace key will be sent as DEL (7f)");
                                    break;

                                //case "bs":
                                //case "bsasdel":
                                //    _bsasdel = false;
                                //    Console.WriteLine("Backspace will be sent as backspace");
                                //    break;

                                //case "del":
                                //case "delasbs":
                                //    _delasbs = false;
                                //    Console.WriteLine("Delete will be sent as delete");
                                //    break;

                                case "crlf":
                                    _crlf = false;
                                    Console.WriteLine("New line mode - Causes return key to send CR");
                                    break;

                                case "e":
                                case "localecho":
                                    _localecho = false;
                                    Console.WriteLine("Local echo off");
                                    break;

                                default:
                                    Console.WriteLine("Invalid Command. Type ?/help for help");
                                    break;
                            }
                            break;

                        case "q":
                        case "quit":
                            _quit = true;
                            break;

                        case "?":
                        case "h":
                        case "help":
                            Console.WriteLine(
                                "Commands may be abbreviated. Supported commands are:\n\n" +
                                "st - status                  print status information\n" +
                                "l  - list                    print comm port aviables\n" +
                                "o  - open [COMx [BaudRate]]  connect to comm port x.\n" +
                                "c  - close                   close current connection\n" +
                                "d  - display                 display operating parameters\n" +
                                "s  - set                     set options(type 'set ?' for a list)\n" +
                                "u  - unset                   unset options(type 'unset ?' for a list)\n" +
                                "q  - quit                    exit\n" +
                                "?/h - help                   print help information\n");
                            break;

                        default:
                            if (cmds[0] == _escape && _serialPort.IsOpen) _terminalMode = true;
                            else Console.WriteLine("Invalid Command. Type ?/help for help");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.WriteLine();
                    Debug.WriteLine($"ERROR: {ex.Message}");
                    Debug.WriteLine($"{ex.StackTrace}");
                }
            }
        }

        private static string GetPortConfig()
        {
            var sb = new StringBuilder();
            sb.Append(_serialPort.BaudRate);
            switch (_serialPort.Parity)
            {
                case Parity.None:
                    sb.Append("N");
                    break;
                case Parity.Odd:
                    sb.Append("O");
                    break;
                case Parity.Even:
                    sb.Append("E");
                    break;
                case Parity.Mark:
                    sb.Append("M");
                    break;
                case Parity.Space:
                    sb.Append("S");
                    break;
                default:
                    break;
            }
            switch (_serialPort.StopBits)
            {
                case StopBits.None:
                    sb.Append("0");
                    break;
                case StopBits.One:
                    sb.Append("1");
                    break;
                case StopBits.OnePointFive:
                    sb.Append("1.5");
                    break;
                case StopBits.Two:
                    sb.Append("2");
                    break;
                default:
                    break;
            }
            return sb.ToString();
        }

        private static void Terminal()
        {
            var records = new Kernel32.INPUT_RECORD[128];

            Console.WriteLine("Terminal Mode:");
            while (_terminalMode)
            {
                var readSuccess = Kernel32.ReadConsoleInput(_handleIn, records, 128, out uint recordsRead);
                if (readSuccess && recordsRead > 0)
                {
                    for (var index = 0; index < recordsRead; index++)
                    {
                        var record = records[index];

                        if (record.EventType == Kernel32.EVENT_TYPE.KEY_EVENT)
                        {
                            // skip key up events - if not, every key will be duped in the stream
                            if (!record.Event.KeyEvent.bKeyDown ) continue;
                            if ((ushort)record.Event.KeyEvent.uChar == 0)
                                Debug.WriteLine($"Serial Write: {(ushort)record.Event.KeyEvent.uChar} ''  {record.Event.KeyEvent.dwControlKeyState} {record.Event.KeyEvent.wVirtualKeyCode} {record.Event.KeyEvent.wVirtualScanCode}");
                            else
                                Debug.WriteLine($"Serial Write: {(ushort)record.Event.KeyEvent.uChar} '{record.Event.KeyEvent.uChar}'  {record.Event.KeyEvent.dwControlKeyState} {record.Event.KeyEvent.wVirtualKeyCode} {record.Event.KeyEvent.wVirtualScanCode}");
                            if ((ushort)record.Event.KeyEvent.uChar == 0) continue;

                            var c = record.Event.KeyEvent.uChar.ToString();

                            if (c == _escape)
                            {
                                _terminalMode = false;
                                Console.WriteLine();
                                break;
                            }

                            //if (c == "\b" && _bsasdel) c = "\x07f";
                            //else if (c == "\x07f" && _delasbs) c = "\b";
                            if (c == "\x07f" && _backspaceasbs) c = "\b"; // Backspace Key
                            else if (c == "\b" && _backspaceasbs) c = "\x07f"; // Ctrl-Backspace Key
                            if (c == "\r" && _crlf) c = "\r\n";
                            for (var n = 0; n < record.Event.KeyEvent.wRepeatCount; n++)
                            {
                                try
                                {
                                    _serialPort.Write(c);
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine(ex.Message);
                                    Console.WriteLine();
                                    Console.WriteLine($"COM ERROR: {ex.Message}");
                                    if (!_serialPort.IsOpen)
                                    {
                                        try
                                        {
                                            _serialPort.Open();
                                            Console.WriteLine($"{_serialPort.PortName} opened at {GetPortConfig()}");
                                        }
                                        catch (Exception ex2)
                                        {
                                            Debug.WriteLine(ex2.Message);
                                            Console.WriteLine();
                                            _terminalMode = false;
                                        }
                                    }
                                }
                                if (_localecho) Console.Write(c);
                            }
                        }
                        else
                        {
                                Debug.WriteLine($"Event {record.EventType} {record.Event}");
                        }
                    }

                }
            }
        }

        private static void _serialPort_PinChanged(object sender, SerialPinChangedEventArgs e)
        {
            Debug.WriteLine($"PinChanged Event: {e.EventType.ToString()}");
        }

        private static void _serialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine();
            Console.WriteLine($"COM ERROR: {e.EventType.ToString()}");
            if (!_serialPort.IsOpen) _terminalMode = false;
        }

        private static void SetConsoleModeInTerminal()
        {
            if (!Kernel32.GetConsoleMode(_handleIn, out Kernel32.CONSOLE_INPUT_MODE modeIn))
                throw NativeMethods.GetExceptionForLastWin32Error();
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_PROCESSED_INPUT;
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_LINE_INPUT;
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_ECHO_INPUT;
            modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_WINDOW_INPUT;
            modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_MOUSE_INPUT;
            modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_VIRTUAL_TERMINAL_INPUT;
            if (!Kernel32.SetConsoleMode(_handleIn, modeIn))
                throw NativeMethods.GetExceptionForLastWin32Error();
        }

        private static void SetConsoleModeInCommand()
        {
            if (!Kernel32.GetConsoleMode(_handleIn, out Kernel32.CONSOLE_INPUT_MODE modeIn))
                throw NativeMethods.GetExceptionForLastWin32Error();
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_PROCESSED_INPUT;
            modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_LINE_INPUT;
            modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_ECHO_INPUT;
            //modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_EXTENDED_FLAGS;
            //modeIn |= Kernel32.CONSOLE_INPUT_MODE.ENABLE_QUICK_EDIT_MODE;
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_WINDOW_INPUT;
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_MOUSE_INPUT;
            modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_VIRTUAL_TERMINAL_INPUT;
            //modeIn &= ~Kernel32.CONSOLE_INPUT_MODE.ENABLE_QUICK_EDIT_MODE;
            if (!Kernel32.SetConsoleMode(_handleIn, modeIn))
                throw NativeMethods.GetExceptionForLastWin32Error();
        }

        private static void SetConsoleModeOut()
        {
            if (!Kernel32.GetConsoleMode(_handleOut, out Kernel32.CONSOLE_OUTPUT_MODE modeOut))
                throw NativeMethods.GetExceptionForLastWin32Error();
            modeOut |= Kernel32.CONSOLE_OUTPUT_MODE.ENABLE_PROCESSED_OUTPUT;
            modeOut |= Kernel32.CONSOLE_OUTPUT_MODE.ENABLE_WRAP_AT_EOL_OUTPUT;
            modeOut |= Kernel32.CONSOLE_OUTPUT_MODE.ENABLE_VIRTUAL_TERMINAL_PROCESSING;
            modeOut &= ~Kernel32.CONSOLE_OUTPUT_MODE.DISABLE_NEWLINE_AUTO_RETURN;
            if (!Kernel32.SetConsoleMode(_handleOut, modeOut))
                throw NativeMethods.GetExceptionForLastWin32Error();
        }

        private static void _serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            var buf = new byte[128];
            var bufChar = new char[128];

            while (_serialPort.BytesToRead > 0)
            {

                //int num = _serialPort.BytesToRead;
                //if (num > 128) num = 128;
                //_serialPort.Read(buf, 0, num);

                //// ?? decode UTF8 ??
                //for (int i = 0; i < num; i++)
                //{
                //    bufChar[i] = (char)buf[i];
                //    Debug.WriteLine($"Serial Read: {buf[i]} '{(char)buf[i]}'");
                //}
                //var writeSuccess = Kernel32.WriteConsole(_handleOut, bufChar, (uint)num, out uint numWrite);

                var str = _serialPort.ReadExisting();
                var dec = new StringBuilder();
                foreach (var c in str.ToCharArray()) dec.Append($"{(ushort)c} ");
                Debug.WriteLine($"Serial Read: {dec.ToString()}'{str}'");
                if (_terminalMode) Console.Write(str);
            }
        }
    }
}
