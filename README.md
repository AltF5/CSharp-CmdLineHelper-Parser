# C# CmdLineHelper.cs Command line Parser
A "no frills" 1 class-only, C# .NET command line parser with support for - and / args, switches only, and Name : Values

- Simple
- Robust
- Self contained. Simply drop into your project and call it below.


-	How to use [Example use cases]
 	
 		// Utilizes Environment.CommandLine for its parsing
 		CmdLineHelper cmdline = new CmdLineHelper();
 		CmdLineHelper cmdline2 = new CmdLineHelper("some custom command line");
 		

 		// Creates an accessible list of args
 		// List<CmdArg> Args
 		foreach(CmdArg in cmdline.Args) { //... }
 	

 		// Switch only
 		bool noExit = cmdline.WasArgSupplied("-noexit");
 	

 		// Multiple switch aliases
 		if(cmdline.WasArgSupplied(new string[] {"-con", "-console", "-showConsole"}))
 		{
 			showConsole = true;
 		}
 		

 		// Retreive the value of this -num argument name. Ex: -num 5, will return "5"
 		string numberOfTimes = cmdline.GetArgValue("-num")
 		

 		// Get a list of which argument name swtotcjes were supplied
 		List<string> swSpecial = cmdline.GetAllArgNamesSupplied(string[] SW_SPECIAL_ARG_NAMES = { "-hide", "-min", "-max" });
 		
 		
 		// Full command line if desired (Environment.CommandLine less the current .exe path)
 		string all = CmdLineHelper.SupportMethods.CurrentArgsStrWithoutExe
 		
 		// Split out the commandline and arguments into 2 separate strings:
 		string fileName = "";
 	    string cmdLineBuilt = "";
 	    CmdLineHelper.SupportMethods.SplitCommandLineInto(runCommand, out fileName, out cmdLineBuilt);
 		 

 		// Traditional CommandLineToArgvW API call with some features to preserve quotes or not
 		string[] args = CmdLineHelper.SupportMethods.CommandLineToArgs(Environment.CommandLine)


- Moivation to publically upload: @FuzzySec, a great security research :D
