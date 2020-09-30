# C# CmdLineHelper.cs Command line Parser
A "no frills" 1 class-only, C# .NET command line parser with support for - and / args, switches only, and Name : Values

- Simple (no depends)
- Robust (many support methods)
- Self contained (Simply drop into your project and call it below)


-	How to use
 	
 		// Utilizes Environment.CommandLine for its parsing
 		CmdLineHelper cmdline = new CmdLineHelper();
   
		 // OR use your own.
 		CmdLineHelper cmdline2 = new CmdLineHelper("some custom command line");
 		

 		// From above, the class creates an accessible list of args, accessible like so...
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


- Moivation to publically upload: @FuzzySec, a great security research :D. He needed something .NET 3.5 compat, and simple to integrate
- Motivation to create: I like string manipulation, and necessity is the mother of invention. Didn't find any self-contained classes out there when needing this in 2015, so I started from the ground up, and slowly improved here and there over the years.
