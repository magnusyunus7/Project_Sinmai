using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SEGADownloadTool_Sinmai;

public static class Ward
{
	private const int c0 = 4;

	private const int c1 = 7;

	private const int c2 = 30;

	private const int c3 = 31;

	private static readonly string[] h0 = new string[37]
	{
		"dnspy", "ilspy", "dotpeek", "reflector", "justdecompile", "de4dot", "ildasm", "x64dbg", "x32dbg", "ollydbg",
		"windbg", "ghidra", "ida", "cheatengine", "cheat engine", "megadumper", "mega dumper", "extremedumper", "scylla", "processhacker",
		"process hacker", "systeminformer", "system informer", "procmon", "procexp", "dotnet-dump", "procdump", "dumpbin", "pestudio", "cffexplorer",
		"cff explorer", "resourcehacker", "resource hacker", "hxd", "snoop", "binaryninja", "binary ninja"
	};

	private static readonly string[] h1 = new string[3] { "COR_PROFILER", "CORECLR_PROFILER", "DOTNET_STARTUP_HOOKS" };

	private static readonly string[] h3 = new string[2] { "COR_ENABLE_PROFILING", "CORECLR_ENABLE_PROFILING" };

	private static Timer? h2;

	[DllImport("kernel32.dll")]
	private static extern bool IsDebuggerPresent();

	[DllImport("kernel32.dll")]
	private static extern bool CheckRemoteDebuggerPresent(nint process, ref bool present);

	[DllImport("ntdll.dll")]
	private static extern int NtQueryInformationProcess(nint process, int infoClass, ref nint info, int length, out int returned);

	public static void V1()
	{
		if (V0())
		{
			Environment.Exit(0);
		}
	}

	public static void V2()
	{
		if (h2 == null)
		{
			h2 = new Timer(delegate
			{
				V1();
			}, null, TimeSpan.FromSeconds(4L), TimeSpan.FromSeconds(4L));
		}
	}

	private static bool V0()
	{
		if (V3())
		{
			return true;
		}
		if (!V4())
		{
			return V5();
		}
		return true;
	}

	private static bool V3()
	{
		nint handle;
		try
		{
			if (Debugger.IsAttached)
			{
				return true;
			}
			handle = Process.GetCurrentProcess().Handle;
		}
		catch
		{
			return false;
		}
		try
		{
			if (IsDebuggerPresent())
			{
				return true;
			}
		}
		catch
		{
		}
		try
		{
			bool present = false;
			if (CheckRemoteDebuggerPresent(handle, ref present) & present)
			{
				return true;
			}
		}
		catch
		{
		}
		if (!V6(handle, 7) && !V6(handle, 30))
		{
			return V7(handle);
		}
		return true;
	}

	private static bool V6(nint self, int infoClass)
	{
		try
		{
			nint info = IntPtr.Zero;
			int returned;
			return NtQueryInformationProcess(self, infoClass, ref info, IntPtr.Size, out returned) == 0 && info != IntPtr.Zero;
		}
		catch
		{
			return false;
		}
	}

	private static bool V7(nint self)
	{
		try
		{
			nint info = IntPtr.Zero;
			int returned;
			return NtQueryInformationProcess(self, 31, ref info, IntPtr.Size, out returned) == 0 && info == IntPtr.Zero;
		}
		catch
		{
			return false;
		}
	}

	private static bool V4()
	{
		try
		{
			Process[] processes = Process.GetProcesses();
			foreach (Process process in processes)
			{
				string processName;
				try
				{
					processName = process.ProcessName;
				}
				catch
				{
					continue;
				}
				if (!string.IsNullOrEmpty(processName) && V8(processName))
				{
					return true;
				}
			}
		}
		catch
		{
		}
		return false;
	}

	private static bool V8(string processName)
	{
		string text = processName.ToLowerInvariant();
		if (text.EndsWith(".exe", StringComparison.Ordinal))
		{
			string text2 = text;
			text = text2.Substring(0, text2.Length - 4);
		}
		string[] array = h0;
		foreach (string value in array)
		{
			if (text.StartsWith(value, StringComparison.Ordinal))
			{
				return true;
			}
		}
		return false;
	}

	private static bool V5()
	{
		string[] array = h1;
		for (int i = 0; i < array.Length; i++)
		{
			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(array[i])))
			{
				return true;
			}
		}
		array = h3;
		for (int i = 0; i < array.Length; i++)
		{
			string environmentVariable = Environment.GetEnvironmentVariable(array[i]);
			if (!string.IsNullOrEmpty(environmentVariable) && (environmentVariable == "1" || environmentVariable.Equals("true", StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}
		return false;
	}
}
