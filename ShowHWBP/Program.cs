namespace ShowHWBP
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;

    internal class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine($"Usage: {AppDomain.CurrentDomain.FriendlyName} pid|name");
                return;
            }

            Console.WriteLine($"Waiting for process by name or pid {args.First()} ...");
            Process process;
            do
            {
                process = Process.GetProcesses().FirstOrDefault(proc => Matches(proc, args.First()));
                Thread.Sleep(50);
            } while (process == null);

            Console.CursorVisible = false;

            var lastReport = string.Empty;
            var thisReport = new StringBuilder();
            while (!process.HasExited)
            {
                thisReport.AppendLine($"Watching process {process.ProcessName} ({process.Id} | {process.Id:X})");
                foreach (ProcessThread thread in process.Threads)
                {
                    thisReport.AppendLine($"Thread {thread.Id:X}");
                    var threadHandle = NativeFunctions.OpenThread(ThreadAccess.GET_CONTEXT, false, (uint) thread.Id);
                    if (threadHandle == IntPtr.Zero)
                    {
                        thisReport.AppendLine("- Access denied");
                        continue;
                    }

                    try
                    {
                        var context = new CONTEXT64 {ContextFlags = (CONTEXT_FLAGS) 0x10};

                        if (!NativeFunctions.GetThreadContext(threadHandle, ref context))
                        {
                            thisReport.AppendLine("- Context protected");
                            continue;
                        }

                        thisReport.AppendLine($"- Dr0: {context.Dr0,16:X} {ParseDr7(context.Dr7, 0)}");
                        thisReport.AppendLine($"- Dr1: {context.Dr1,16:X} {ParseDr7(context.Dr7, 1)}");
                        thisReport.AppendLine($"- Dr2: {context.Dr2,16:X} {ParseDr7(context.Dr7, 2)}");
                        thisReport.AppendLine($"- Dr3: {context.Dr3,16:X} {ParseDr7(context.Dr7, 3)}");
                        thisReport.AppendLine($"- Dr6: {context.Dr6,16:X}");
                        thisReport.AppendLine($"- Dr7: {context.Dr7,16:X}");
                    }
                    finally
                    {
                        NativeFunctions.CloseHandle(threadHandle);
                    }
                }

                var reportString = thisReport.ToString();
                thisReport.Clear();
                if (reportString != lastReport)
                {
                    Console.Clear();
                    Console.Write(reportString);
                    lastReport = reportString;
                }

                Thread.Sleep(0);
            }
        }

        private static string ParseDr7(ulong dr7, int registerIndex)
        {
            var enabled = (dr7 & (1ul << (registerIndex * 2))) > 0 ? "+" : "-";
            var kind = ((dr7 >> (16 + registerIndex * 4)) & 3ul) switch
            {
                0 => "X",
                1 => "W",
                2 => "#",
                3 => "A",
                _ => "?"
            };
            var len = ((dr7 >> (18 + registerIndex * 4)) & 3ul) switch
            {
                0 => "1",
                1 => "2",
                2 => "8",
                3 => "4",
                _ => "?"
            };
            return $"{enabled} {kind} {len}";
        }

        private static bool Matches(Process process, string filter)
        {
            if (process.ProcessName.StartsWith(filter, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (int.TryParse(filter, out var pid))
                return process.Id == pid;

            return false;
        }
    }
}