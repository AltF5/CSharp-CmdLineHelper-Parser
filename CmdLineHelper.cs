
#region Notes & Ver history

// -- Version History & Location --
//
// Public code location: https://github.com/AltF5/CSharp-CmdLineHelper-Parser
//
//
// Update History:
//
// 2015.8.20    - Inception
// 2016.10.27   - Instance class so that there can be an instance for the App and an instance for any other string
//                GetAnyArgName
//                Fixed quoting issues, quotes disappearing, and now can supply [A] or [ALL] to have an entire command line be passed through and not parsed out int various args
// 2021.6.16    - Various fixes
// 2020.9.30    - .NET 3.5 compat
// 2020.10.28   - Added GetArgValueStartsWith & WasArgSuppliedStartsWith. 
//                Also double "--" in front of special commands --disableSlash OR --disableDash
//
// 2020.12      - WasAtLeast1ArgSupplied - Determines if any argument was passed to this application (excluding the current executable as an argument)
//                This is good for cases when the user may pass an argument but with no switch "-" or "/", since in that case Args.count would return 0 (if that was the checking condition instead, and hence not being expected).
//
// 2021.11.4   - Added 2 quick check methods not requiring the caller to create an instance of this class
//               WasArgSupplied_QuickCheck, GetArgValue_QuickCheck
//
// 2024.1.28    - File paths are now checked for a valid file (WHEN SET) for encountering special File args
//                  
//                Exmaple use case:
//                CmdLineHelper cmdline = new CmdLineHelper(argsToTreatAsFinalFilePath: new string[] { "-a", "-app" },   checkForFilePaths_DontRequireAtEnd: true);
//
//               This adds the ability to treat an argument as a file path, without needing to specially surround it like with [a] [a] or |  |
//               so long as it (1) Resolves to a file  and (2) is at the very end (is the last argument supplied), since it constructs this by concatenting all of the remaining parsed out arguments to the right
//               See code: 'argsToTreatAsFinalFilePath' in ctors
//               Why:
//               This allows the ability to say (for exmaple) have the CmdLineHelper.cs implementer here to obtain a valid file to execute (properly resolves in an argument), to simply have this 
//                  Now works
//                      -a C:\Apps\test -dir\ToRun.exe
//
//                  When previosuly it required [a]  [a]  or  |   | since the path had a dash in it
//                      -a [a] C:\Apps\test -dir\ToRun.exe [a]
//
//                      Note that this is specifically for handling when a - is preceded by a space, since   -a C:\Apps\test-dir\ToRun.exe    works fine, but a " -xyz" in a directory is the problem resolving as a user convenience
//
//                  Note that [a] [a] or |  | are still required if it is NOT the final argument, since the logic below assembles/constructs this based on ALL remaining arguments 
//                      -a [a] C:\Apps\test -dir\ToRun.exe [a] -test
//
//                      Otherwise without [a] [a] it will fail to resolve this as a file, since its checking for   C:\Apps\test -dir\ToRun.exe -test
//                      -a C:\Apps\test -dir\ToRun.exe -test
//
//
//              - Further implemetnation above:
//                checkForFilePaths_DontRequireAtEnd: true (default to true), then this is not required to exist at the end and can be anywhere in the commandline (as long as its flagged as a special arg)
//
//
//
//              - Region cleanup, comments added, and some variables renamed
//
// ------------------------------
//
// Special Notes:
//    Passing a command line with "-" or "/" through:
//       It is necessary to place [A] or [ALL] surrounding a command line which contains the special characters "-" or "/"
//       Ex:   -cmdline [a] -cmd calc.exe [a]
//
// ------------------------------
//
// Special commands:
//      [a] or [all]        Surround a string with switches in this
//      -disableSlash       "/" will NOT be treated as a new arg
//      -disableDash        "-" will NOT be treated as a new arg
//
//
//  *** Welcome to use this class as needed, but please credit source back to here. ***

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// 	How to use [Example use cases]
// 	
// 		// Utilizes Environment.CommandLine for its parsing
// 		CmdLineHelper cmdline = new CmdLineHelper();
// 		CmdLineHelper cmdline2 = new CmdLineHelper("some custom command line");
// 		
//
// 		// Creates an accessible list of args
// 		// List<CmdArg> Args
// 		foreach(CmdArg in cmdline.Args) { //... }
// 	
//
// 		// Switch only
// 		bool noExit = cmdline.WasArgSupplied("-noexit");
// 	
//
// 		// Multiple switch aliases
// 		if(cmdline.WasArgSupplied(new string[] {"-con", "-console", "-showConsole"}))
// 		{
// 			showConsole = true;
// 		}
// 		
//
// 		// Retreive the value of this -num argument name. Ex: -num 5, will return "5"
// 		string numberOfTimes = cmdline.GetArgValue("-num")
// 		
//
// 		// Get a list of which argument name swtotcjes were supplied
// 		List<string> swSpecial = cmdline.GetAllArgNamesSupplied(string[] SW_SPECIAL_ARG_NAMES = { "-hide", "-min", "-max" });
// 		
// 		
// 		// Full command line if desired (Environment.CommandLine less the current .exe path)
// 		string all = CmdLineHelper.SupportMethods.CurrentArgsStrWithoutExe
// 		
// 		// Split out the commandline and arguments into 2 separate strings:
// 		string fileName = "";
// 	    string cmdLineBuilt = "";
// 	    CmdLineHelper.SupportMethods.SplitCommandLineInto(runCommand, out fileName, out cmdLineBuilt);
// 		 
//
// 		// Traditional CommandLineToArgvW API call with some features to preserve quotes or not
// 		string[] args = CmdLineHelper.SupportMethods.CommandLineToArgs(Environment.CommandLine)

public class CmdLineHelper
{
    /// <summary>
    /// FYI -- These are case insensitive
    /// </summary>
    public static string[] PASS_ALL_INDICATORS = { "[A]", "[ALL]", "|" };
    public bool DisableDash = false;
    public bool DisableSlash = false;

    #region 'thisExe' Support string

    // 2019-3-3
    //      Note that using System.Reflection.Assembly.GetExecutingAssembly().Location will return "" in the event that this assembly is loaded from memory (such as dynamic loading through "obfuscating")
    //
    //      Either of these work instead:       AppDomain.CurrentDomain.BaseDirectory
    //                               OR         Path.GetDirectoryName((new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase)).LocalPath)
    //
    //      See here: blog.adilakhter.com/2007/12/17/get-current-executing-assemblys-directory/
    //          Beware that Assembly.Location property returns the location of the assembly file after it is shadow-copied. In addition , if it is dynamically loaded using Load method, it returns empty string .
    //          As stated in the comments below, if you are interested in retrieving the location of the assembly before it is shadow-copied or loaded,  use Assembly.CodeBase property instead. 

    // Alternative
    //
    //
    public static string thisExe2 = System.Reflection.Assembly.GetExecutingAssembly().Location;
    public static string thisExeDir2 = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

    // Fix: 2020-2-16
    //      Do not use new Uri(_).LocalPath which will break for dirs with # char in them. Instead conver the file:/// forward slash URI to an actual Windows path
    //      For example do NOT do:   (new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase)).LocalPath
    //      System.Reflection.Assembly.GetExecutingAssembly().CodeBase works just fine
    public static string thisExe = System.Reflection.Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "").Replace("/", "\\");
    public static string thisExeName = Path.GetFileNameWithoutExtension(thisExe);

    #endregion

    #region Properties (public)

    /// <summary>
    /// Checker variable, which determines if any argument was passed to this application (excluding the current executable as an argument)
    /// This is good for cases when the user may pass an argument but with no switch "-" or "/", since in that case Args.count would return 0 (if that was the checking condition instead, and hence not being expected).
    /// </summary>
    private bool wasAtLeast1ArgSupplied = false;
    public bool WasAtLeast1ArgSupplied
    {
        get
        {
            return wasAtLeast1ArgSupplied;
        }

        private set
        {
            wasAtLeast1ArgSupplied = value;
        }
    }

    private bool wasAtLeast1ArgSWITCHSupplied = false;
    public bool WasAtLeast1ArgSWITCHSupplied
    {
        get
        {
            return wasAtLeast1ArgSWITCHSupplied;
        }

        private set
        {
            wasAtLeast1ArgSWITCHSupplied = value;
        }
    }

    private bool wasDashOrSlashDisabled = false;
    public bool WasDashOrSlashDisabled
    {
        get
        {
            return wasDashOrSlashDisabled;
        }

        private set
        {
            wasDashOrSlashDisabled = value;
        }
    }

    #endregion

    #region CmdArg UDT Structure (Class)

    public class CmdArg
    {
        /// <summary>
        /// (not letting this be null)
        /// </summary>
        public string Name = "";

        /// <summary>
        /// (not letting this be null)
        /// </summary>
        public string Value = "";

        /// <summary>
        /// Was the argument surrounded by [A] or [ALL] to ensure all subcommand line switches are preserved
        /// </summary>
        public bool WasFullArgSupplied;

        /// <summary>
        /// Stores what was utilized to delimit the argument: - or /
        /// </summary>
        public string DelimiterUsed;
    }

    #endregion

    #region 'Args' List accessor (public)

    /// <summary>
    /// Internal list of command line arguments.
    /// This is a List of Key-Values representing an arugment. Populated via PopulateArgData_InternalList
    /// </summary>
    private List<CmdArg> allCmdLines;

    /// <summary>
    /// Returns a list of CmdArgs each holding the Name and Value for every Argument
    /// This public accessor will allow for the utilization of Linq querying of certain criteria
    /// </summary>
    public List<CmdArg> Args
    {
        get
        {
            // Ensure that the arguments have already been initialized
            if (allCmdLines == null)
            {
                PopulateArgData_InternalList();
            }

            return allCmdLines;
        }
    }


    #endregion





    #region Constructors - ctors

    public CmdLineHelper()
        : this(Environment.CommandLine)
    {

    }

    public CmdLineHelper(string[] argsToTreatAsFinalFilePath, bool checkForFilePaths_DontRequireAtEnd = true)
    {
        PopulateArgData_InternalList(
            Environment.CommandLine, 

            // These 2 arguments are related. See ParseArgs
            argsToTreatAsFinalFilePath:         argsToTreatAsFinalFilePath,
            checkForFilePaths_DontRequireAtEnd: checkForFilePaths_DontRequireAtEnd
            );
    }

    /// <summary>
    /// Allows for manual population for custom parsing of any commandline string.
    /// When allowSpecialCommandInstructions is supplied, then -disabledash and -disableslash is ignored
    /// </summary>
    public CmdLineHelper(
        string customCommandLine, 
        bool removeFirstArgumentAsExePath = true, 
        bool disableDash = false, 
        bool disableSlash = false, 
        bool allowSpecialCommandInstructions = true,
        string[] argsToTreatAsFinalFilePath = null, 
        bool checkForFilePaths_DontRequireAtEnd = true
        )
    {
        DisableDash = disableDash;
        DisableSlash = disableSlash;

        PopulateArgData_InternalList(customCommandLine, removeFirstArgumentAsExePath, allowSpecialCommandInstructions, argsToTreatAsFinalFilePath, checkForFilePaths_DontRequireAtEnd);
    }

    #endregion

    #region Public Arg query methods

    public bool WasAnyArgSupplied_Except(params string[] args)
    {
        // First trim off any "-" if added
        List<string> argsMod = new List<string>();
        foreach(string a in args)
        {
            if ((a.StartsWith("-") || a.StartsWith("/")) && a.Length > 1)
            {
                argsMod.Add(a.Substring(1));
            }
            else
            {
                argsMod.Add(a);
            }
        }

        // NOT IN Linq - stackoverflow.com/a/27498081
        //      MainList needs to go on the outside:    MainList.Where(x => !List2.Any ...
        var argsNotInExclusionList = Args.Where(a => !argsMod.Any(b => b.ToLower() == a.Name.ToLower()));

        // If this list is populated, then more args than just the excluded ones were supplied
        return (argsNotInExclusionList.Count() != 0);
    }

    public CmdArg GetArgInfo(params string[] argAliases)
    {
        foreach (string s in argAliases)
        {
            CmdArg argInfo;
            string value = GetArgValue(s, out argInfo);

            if (argInfo != null)
            {
                return argInfo;
            }
        }

        return null;
    }

    /// <summary>
    /// For argument aliases (like -n or -num)
    /// </summary>
    public string GetArgValue(params string[] argAliases)
    {
        foreach (string s in argAliases)
        {
            CmdArg argInfo;
            string value = GetArgValue(s, out argInfo);

            if (!StringSupportMethods.IsStringNullOrWhitespace(value))
            {
                return value;
            }
        }

        return "";
    }

    /// <summary>
    /// Returns the Name of the first argument Names supplied, if any.
    /// </summary>
    public string GetAnyArgName(params string[] args)
    {
        foreach (string arg in args)
        {
            string modArgName = "";
            if (arg.StartsWith("-"))
            {
                modArgName = arg.Replace("-", "");
            }

            CmdArg argFound = allCmdLines.Where(x => x.Name.ToLower() == modArgName.ToLower()).FirstOrDefault();
            if (argFound != null)
            {
                return argFound.Name;
            }
        }

        return "";
    }

    /// <summary>
    /// Returns the list of Names of all of the arguments supplied
    /// </summary>
    public List<string> GetAllArgNamesSupplied(params string[] args)
    {
        List<string> ret = new List<string>();

        foreach (string arg in args)
        {
            string modArgName = "";
            if ((arg.StartsWith("-") || arg.StartsWith("/")) && arg.Length > 1)
            {
                //modArgName = arg.Replace("-", "");
                modArgName = arg.Substring(1);
            }

            CmdArg argFound = allCmdLines.Where(x => x.Name.ToLower() == modArgName.ToLower()).FirstOrDefault();
            if (argFound != null)
            {
                ret.Add(argFound.Name);
            }
        }

        return ret;
    }

    public string GetAnyArgName_OrDefault(string defaultRet, params string[] args)
    {
        string ret = GetAnyArgName(args);

        if (StringSupportMethods.IsStringNullOrWhitespace(ret))
        {
            return defaultRet;
        }
        else
        {
            return ret;
        }
    }

    /// <summary>
    /// Returns the argument's Value given the argument Name
    /// If the argument doesn't exist the value with be a blank string
    /// </summary>
    /// <param argName="arg">The argument argName. Ex: "v" for when "-v" is specified</param>
    /// <returns></returns>
    public string GetArgValue(string argName, out CmdArg argInfo, bool startsWith = false)
    {
        string ret = "";

        // Trim off any leading argument qualifier char
        if ((argName.StartsWith("-") || argName.StartsWith("/")) && argName.Length > 1)
        {
            argName = argName.Substring(1);
        }

        // Ensure that the arguments have already been initialized
        if (allCmdLines == null)
        {
            PopulateArgData_InternalList();
        }

        // Perform lookup...

        CmdArg argFound = null;
            
        if(!startsWith)
        {
            // typical path - Match exact name
            argFound = allCmdLines.FirstOrDefault(x => x.Name.ToLower() == argName.ToLower());
        }
        else
        {
            // atypical - StartsWith
            argFound = allCmdLines.FirstOrDefault(x => x.Name.StartsWith(argName, StringComparison.CurrentCultureIgnoreCase));
        }

        if (argFound != null)
        {
            ret = argFound.Value;
        }

        argInfo = argFound;

        return ret;
    }

    /// <summary>
    /// Obtain an arguments value if the name starts with something. Such as -imp, -impers, -impersonate, -impersonation, by just checking if starts with "imp"
    /// </summary>
    public string GetArgValueStartsWith(string argNameStartsWith)
    {
        return GetArgValue(argNameStartsWith, out CmdArg ignore, true);
    }

    /// <summary>
    /// Obtain an arguments value if the name starts with something. Such as -imp, -impers, -impersonate, -impersonation, by just checking if starts with "imp"
    /// </summary>
    public string GetArgValueStartsWith(string argNameStartsWith, out string exactArgNameSupplied)
    {
        exactArgNameSupplied = "";

        string val = GetArgValue(argNameStartsWith, out CmdArg info, true);
        if(info != null)
        {
            exactArgNameSupplied = info.Name;
        }

        return val;
    }

    public string GetArgValue_OrDefault(string argName, string defaultVal)
    {
        if (!WasArgSupplied(argName))
        {
            return defaultVal;
        }
        else
        {
            return GetArgValue(argName);
        }
    }

    public bool WasArgSupplied(params object[] argAliases)
    {
        List<string> paramsAsArray = new List<string>();

        foreach (string s in argAliases)
        {
            paramsAsArray.Add(s);
        }

        return WasArgSupplied(paramsAsArray.ToArray());
    }

    /// <summary>
    /// Checks to see if an argument was specified or not
    /// </summary>
    /// <param argName="argName">The argument Name. Ex: "v" for when "-v" is specified</param>
    /// <returns></returns>
    public bool WasArgSupplied(string argName)
    {
        return WasArgSupplied(new string[] { argName });
    }

    /// <summary>
    /// Checks to see if an argument was specified or not, based on it starting with some string. Such as -imp, -impers, -impersonate, -impersonation, by just checking if starts with "imp"
    /// </summary>
    public bool WasArgSuppliedStartsWith(string argNameStartsWith)
    {
        return WasArgSuppliedStartsWith(argNameStartsWith, out string ignore);
    }

    /// <summary>
    /// Checks to see if an argument was specified or not, based on it starting with some string. Such as -imp, -impers, -impersonate, -impersonation, by just checking if starts with "imp"
    /// </summary>
    public bool WasArgSuppliedStartsWith(string argNameStartsWith, out string exactArgNameSupplied)
    {
        exactArgNameSupplied = "";

        bool yes = WasArgSupplied(new string[] { argNameStartsWith }, out CmdArg info, true);
        if (info != null)
        {
            exactArgNameSupplied = info.Name;
        }

        return yes;
    }

    /// <summary>
    /// Checks to see if any argument in the string array was specified or not
    /// </summary>
    /// <param argNames="argNames">An array of argument names. Ex: new string[] = { "1", "2" }</param>
    public bool WasArgSupplied(string[] argNames, bool startWith = false)
    {
        return WasArgSupplied(argNames, out CmdArg ignore, startWith);
    }

    /// <summary>
    /// Checks to see if any argument in the string array was specified or not
    /// </summary>
    /// <param argNames="argNames">An array of argument names. Ex: new string[] = { "1", "2" }</param>
    public bool WasArgSupplied(string[] argNames, out CmdArg info, bool startWith = false)
    {
        bool ret = false;
        info = new CmdArg();

        // Ensure that the arguments have already been initialized
        if (allCmdLines == null)
        {
            PopulateArgData_InternalList();
        }

        // See if any of the supplied arguments were specified
        foreach (string arg in argNames)
        {
            string argName = arg;

            // Trim off any leading argument qualifier char, in case it was accidently added
            if ((argName.StartsWith("-") || argName.StartsWith("/")) && argName.Length > 1)
            {
                argName = argName.Substring(1);
            }

            // Perform lookup...


            CmdArg argFound = null;
                
            if(!startWith)
            {
                // Typical path
                argFound = allCmdLines.FirstOrDefault(x => x.Name.ToLower() == argName.ToLower());
            }
            else
            {
                // Atypical - starts with check
                argFound = allCmdLines.FirstOrDefault(x => x.Name.StartsWith(argName, StringComparison.CurrentCultureIgnoreCase));
            }
                
            info = argFound;

            // Once one was found, then exit
            if (argFound != null)
            {
                ret = true;
                break;
            }
        }

        return ret;
    }

    #endregion

    #region Private Parser Methods

    /// <summary>
    /// Initalizes this class' Argument list (accessed publically via the 'Args' property)
    /// </summary>
    private void PopulateArgData_InternalList()
    {
        // NOTE: Do NOT use Environment.GetCommandLineArgs() or else it will strip out quotes in the command line, and they will need to be escapted via 3 quotes"""
        // Use Environment.CommandLine
        allCmdLines = ParseArgs_NestingCheck(Environment.CommandLine);
    }

    private void PopulateArgData_InternalList(
        string customCommandLine, 
        bool removeFirstArgumentAsExePath = true, 
        bool allowSpecialCommandInstructions = true,
        string[] argsToTreatAsFinalFilePath = null,
        bool checkForFilePaths_DontRequireAtEnd = true)
    {
        allCmdLines = ParseArgs_NestingCheck(customCommandLine, removeFirstArgumentAsExePath, allowSpecialCommandInstructions, argsToTreatAsFinalFilePath, checkForFilePaths_DontRequireAtEnd);
    }

    /// <summary>
    /// Initializes an argument list (which will be returned) given a single linear string as the command line
    /// </summary>
    private List<CmdArg> ParseArgs_NestingCheck(
        string commandLine, 
        bool removeFirstArgumentAsExePath = true, 
        bool allowSpecialCommandInstructions = true,
        string[] argsToTreatAsFinalFilePath = null,
        bool checkForFilePaths_DontRequireAtEnd = true)
    {
        bool isAll = StringSupportMethods.ContainsNoCase(commandLine, "[ALL1]");
        bool nestingSupplied = isAll || StringSupportMethods.ContainsNoCase(commandLine, "[A1]");
        //bool all2 = ContainsNoCase(commandLine, "[ALL2]") || ContainsNoCase(commandLine, "[A2]");


        // Pre-check and Pre-parse
        //
        if (nestingSupplied)
        {
            string toFind = "";
            if (isAll)
            {
                toFind = "[ALL1]";
            }
            else
            {
                toFind = "[A1]";
            }

            int startPos = commandLine.IndexOf(toFind, StringComparison.CurrentCultureIgnoreCase);
            if (startPos >= 0)
            {
                startPos += toFind.Length;          // Skip over this

                int endPos = commandLine.LastIndexOf(toFind, StringComparison.CurrentCultureIgnoreCase);
                if (endPos > startPos)
                {
                    int posOfSwitchStart = commandLine.LastIndexOf("-", startPos, StringComparison.CurrentCultureIgnoreCase);
                    int posOfWhiteSpace = commandLine.IndexOf(" ", posOfSwitchStart);
                    if (posOfSwitchStart >= 0 && posOfWhiteSpace >= 0)
                    {
                        string[] switchExtracts = StringSupportMethods.ExtractAround(commandLine, posOfSwitchStart, posOfWhiteSpace, 1);
                        if (switchExtracts.Length == 3)
                        {
                            string[] extracts = StringSupportMethods.ExtractAround(commandLine, startPos, endPos, toFind.Length);
                            string before = StringSupportMethods.ReplaceNoCase(extracts[0], toFind, "");
                            string nestedCommandLine = extracts[1];
                            string after = StringSupportMethods.ReplaceNoCase(extracts[2], toFind, "");

                            // Remove the switch, which will be stored manually below (and should be kept out of what will be parsed next)
                            before = StringSupportMethods.SubstringIndex(before, 0, posOfSwitchStart - 1);

                            // Remove this process as part of the command line
                            before = SupportMethods.SplitOutCmdLine(before);


                            //
                            // Parse args
                            //
                            List<CmdArg> list = ParseArgs(SupportMethods.CommandLineToArgs(before.Trim() + " " + after.Trim()), allowSpecialCommandInstructions, argsToTreatAsFinalFilePath, checkForFilePaths_DontRequireAtEnd);

                            string argName = switchExtracts[1].Replace("-", "");

                            list.Add(new CmdArg
                            {
                                WasFullArgSupplied = true,               // Set indicator
                                Name = argName.Substring(1).Trim(),
                                Value = nestedCommandLine
                            });

                            return list;
                        }


                    }

                }
            }
        }





        string[] args = SupportMethods.CommandLineToArgs(commandLine, removeFirstArgAsExePath: removeFirstArgumentAsExePath);
        wasAtLeast1ArgSupplied = (args.Length >= 1);

        //
        // Parse args
        //
        var ret = ParseArgs(args, allowSpecialCommandInstructions, argsToTreatAsFinalFilePath: argsToTreatAsFinalFilePath, checkForFilePaths_DontRequireAtEnd: checkForFilePaths_DontRequireAtEnd);

        wasAtLeast1ArgSWITCHSupplied = ret.Count > 0;

        return ret;
    }

    /// <summary>
    /// Core method - Initalizes an argument list (which will be returned) given a string array of command line arguments ('argsArray'
    /// 
    ///     Note that:
    ///     'argArray' may or may not be the true arugments constructed.
    ///     This method takes this array (from say CommandLineToArgvW which splits based on spaces (and quotes)) and then smartly constructs it based on logic it is looking for (to combine or treat as separate)
    ///     until it finds another token '-' or '/'   OR handles specially with [a] [a]  and  |  |
    ///     
    /// 
    ///     'checkForFilePaths_DontRequireAtEnd' only matters if 'argsToTreatAsFinalFilePath' isn't null
    ///     This controls the implementation of "how far" to check for File Existance. When true, it will not require that this is only performed for all remaining args, 
    ///     but rather proceed to the next args, where this File.Exists was found (meaning these can then occur in the middle)
    /// </summary>
    private List<CmdArg> ParseArgs(string[] argArray, bool allowSpecialCommandInstructions = true, string[] argsToTreatAsFinalFilePath = null, bool checkForFilePaths_DontRequireAtEnd = true)
    {
        // Note:
        //      'argArray' needs to be a linear list of Arg Name (ex: "-hello") followed next by its Arg Value (ex: "Hello"), Name, Value, ...
        //      Essentially argArray is a split apart string of tokens based on spaces (and quotes), but this method 'reconstructs' this back together
        //
        // Now adding support to [A] nesting like [A1] [A1], [A2] [A2] (possibly add support for any number later)...
        //      See the parent method ParseArgs_NestingCheck
        //

        List<CmdArg> retList = new List<CmdArg>();

        CmdArg currArg = null;
        bool currentArgAdded = true;                // (true: start out like a previous argument was successfully added)


        // Flag used to prevent checking for "-" or "/" in parameters that are nested and meant to be passed on to the next application's command line, 
        // which will also use the same special characters.
        bool passEverything = false;

        bool startingPassAllIndicator = false;
        bool startedPassAllRightNow = false;



        // Arguments here within the argArray will be separated out by spaces (like a split on " ")
        // So the logic in this loop determines which arguments to combine, and which ones are separate based on special argument characters encountered:
        // Those are "-" or "/". 
        bool startOfNewArgument = true;

        bool turnOffSlashSwitch = false;
        bool turnOffDashSwitch = false;

        for (int indexOn = 0; indexOn < argArray.Length; indexOn++)
        {
            var argStr = argArray[indexOn].Trim();

            // 8-9-17
            // Always reset -- only set on first encountered [a]
            if (startingPassAllIndicator)
            {
                // Reset this flag
                startingPassAllIndicator = false;

                string startingString = StringSupportMethods.FindStartingString(argStr, PASS_ALL_INDICATORS);
                if (!StringSupportMethods.IsStringNullOrWhitespace(startingString) &&
                     startingString.ToLower().Trim() == argStr.ToLower().Trim())
                {
                    // In this case, this will be the ending [a], so just stop passEverything, and this loop execution can be completly skipped
                    passEverything = false;
                    continue;
                }
            }


            // Always reset
            startedPassAllRightNow = false;


            // Always recheck to see if the current argument begins with the "include-everything", which is a single double-quote (but the argName / caller has to escape it via 3 double quotes: """)
            // This is re-evaluated below
            //
            if (passEverything == false)
            {
                // Special args:
                //      -disableSlash
                //      -disableDash
                // If found, then "-" or "/" will not be considered the start of a new argument
                // This is an alternative to specifying [a] or [all] around a string such as:  -a [a] cmd.exe /k ... [a]   -->  -noSlash -a cmd.exe /k ...
                if (allowSpecialCommandInstructions && (DisableSlash || argStr.ToLower() == "--disableslash"))
                {
                    turnOffSlashSwitch = true;
                    WasDashOrSlashDisabled = true;
                    continue;
                }
                else if (allowSpecialCommandInstructions && (DisableDash || argStr.ToLower() == "--disabledash"))
                {
                    turnOffDashSwitch = true;
                    WasDashOrSlashDisabled = true;
                    continue;
                }

                // If a quote is encountered, it means that the user escaped it in the command line by passing it as """ 
                // When this occurs it means to pass everything through in this current parameter (meaning I can nest in other single "-" or "/" as special chars to a sub-application that also uses these, like cmd.exe, or another application that I created which uses this parser)
                passEverything = StringSupportMethods.StartsWithAny_NoCase(argStr, PASS_ALL_INDICATORS);

                if (passEverything && currArg != null)
                {
                    currArg.WasFullArgSupplied = true;

                    startingPassAllIndicator = true;        // Prevention of setting disablePassEverything to true below when checking to see if the string is the ending [A]
                    startedPassAllRightNow = true;
                }


                //startOfNewArgument = true;
            }
            else
            {
                // Always disable startingPassAllIndicator unless it was just enabled for the first [A]
                if (!startedPassAllRightNow)
                {
                    startingPassAllIndicator = false;
                }
                
            }




            //
            // New arg or not?
            //

            // Start a new arg if the current string begins with a "-" or "/"
            if (passEverything == false &&
               ((!turnOffDashSwitch && argStr.StartsWith("-")) ||
                 (!turnOffSlashSwitch && argStr.StartsWith("/"))))
            {

                //
                // -- NEW --  argument to add
                //
                startOfNewArgument = true;

                // If a new arg was encountered before the previous argument's value, then simply add in the previous argument
                if (currentArgAdded == false)
                {
                    if (currArg != null)
                    {
                        retList.Add(currArg);

                        currentArgAdded = true;
                    }
                }

                // Start the new arg 
                //
                currArg = new CmdArg();
                if (argStr.Length > 1)
                {
                    // Store the argument Name
                    currArg.DelimiterUsed = argStr.Substring(0, 1);      //  the "-" or "/" char
                    currArg.Name = argStr.Substring(1);                     // Remove the "-" or "/" start of the argument Name
                    currentArgAdded = false;
                }


                // New 2024 - Check for file paths (File.Exists) IF a specially flagged (from the ctor) was encountered
                //
                if (argsToTreatAsFinalFilePath != null && argsToTreatAsFinalFilePath.FirstOrDefault(a => a.Equals(argStr, StringComparison.CurrentCultureIgnoreCase)) != null)
                {
                    //
                    // Jan 2024 - New support has now been added this special case check to see if encountering this special argument, if it resolves to a valid File Path (File.Exists) for all of the remaining arguments constructed to the right
                    //
                    //      If this is a specially-specific argument (as indicated in the ctor) to treat it as a File Path, then check if the file exists.  [MUST BE THE LAST ARGUMENT -- otherwis this check will not work]
                    //      This allows the ability to have - dashes in the file path are not split out into other arguments prematurely.
                    //      This is simply done by checking if the file exists for this string
                    //      Requires: This special arg to be the last one in the string
                    //

                    if(!checkForFilePaths_DontRequireAtEnd)
                    {
                        //
                        // Impl 1:
                        //      Read all the way to the end (All remaining args ... meaning this special File-check arg needs to be the last one in the list

                        // Advance the array to see if everything after this makes up a valid File name
                        string construtedToCheck = "";
                        for (int indexOn_Next = indexOn + 1; indexOn_Next < argArray.Length; indexOn_Next++)
                        {
                            construtedToCheck += argArray[indexOn_Next] + " ";
                        }

                        construtedToCheck = construtedToCheck.Trim();
                        if (File.Exists(construtedToCheck))
                        {
                            // FOUND a Valid File. Stop everything now, since this needs to be at the very end of a string, and thus is the 1 special case
                            currArg.Value = construtedToCheck;
                            retList.Add(currArg);
                            return retList;
                        }

                    }
                    else
                    {
                        //
                        // Impl 2:
                        //      Allow for a special arg like this to occur in the middle, and not at the end, to check all the next arguments until reaching a valid File (File.Exists)
                        //      or otherwise if not, then just proceed.
                        //
                        //      If yes, it was found, then set the next argument index to where this stopped

                        string construtedToCheck = "";
                        for (int indexOn_Next = indexOn + 1; indexOn_Next < argArray.Length; indexOn_Next++)
                        {
                            construtedToCheck += argArray[indexOn_Next] + " ";

                            if(File.Exists(construtedToCheck.Trim()))
                            {
                                currArg.Value = construtedToCheck.Trim();

                                // Advance the outer-loop index to the next.
                                // Break this inner loop
                                // Below, this argument will get added.
                                // Then the outer loop will just proceed as normal, using this index as the next one

                                //indexOn = indexOn_Next + 1;       // Advance it, but Do NOT advance it +1. The outer-loop will take care of that with indexOn++
                                indexOn = indexOn_Next;             //    rather, just set it equal to where we are now at.
                                break;
                            }
                        }
                    }


                }
            }
            else
            {
                //
                // Not a new argument (so append this onto the current one in progress)
                //

                // If this is not a new arg Name, then this string is added to the current argument's value
                if (currArg != null)
                {
                    // Note:
                    // If the current argument has data in it from the previous array value, then that means there was a space in the cmdline parsed from CommandLineToArgvW
                    //    and hence can be preserved by adding 1 space  
                    //    HOPEFULLY only is one space in the Name, if 2 spaces back-to-back, then this could be problematic, as we wouldn't know, unless implementing our own CommandLineToArgvW parser

                    bool disablePassEverything = false;


                    if (
                        !startOfNewArgument &&          // Prevent the starting [a] from being looked at as the ending [a]      (not sufficient, need the next check too: !startedPassAllRightNow)      
                        !startedPassAllRightNow &&      // Set to true if this is the starting [a]                              Added - 8-9-17
                        passEverything &&
                        StringSupportMethods.EndsWithAny_NoCase(argStr, PASS_ALL_INDICATORS)
                       )
                    {
                        // Stop the PassEverything sequence if this argument ends with the quote
                        disablePassEverything = true;


                        argStr = StringSupportMethods.ReplaceAny_NoCase(argStr, PASS_ALL_INDICATORS, "");
                    }
                    else if (currArg.WasFullArgSupplied)
                    {

                        // 8-9-17
                        // Passing something like this:     -u0 -s [a]-1[a] -a [a] C:\Program Files (x86)\Tweaking.com\Registry Backup\TweakingRegistryBackup.exe /silent [a]
                        //     Causes the pass-all to occur all the way to the end after the first set of [a] [a], since there is no space in that value (between the [a]s).
                        //     Hence its all grouped under -s, and no -a is seen as another argument
                        //     Therefore a check must be done to see if the string ends in [a] (excluding if its the starting [a] in the event that the user did pass in spaces)

                        string startingString = StringSupportMethods.FindStartingString(argStr, PASS_ALL_INDICATORS);
                        if (!StringSupportMethods.IsStringNullOrWhitespace(startingString) &&                           // MUST check for IsNullOrWhitespace, otherwise EndsWith will return true for ending with "" (a blank string)... probably the same for starts with
                             argStr.Length > startingString.Length)
                        {
                            // This shows that this scenario is possibly like this: [a]-1[a]        That is, [a]-1[a] length is greater than [a]
                            // Must check the length for it being grater, otherwise having the argStr just be [a] (that is a space was added between this and the actual full string), would incorrectly satisfy StartsWith and EndsWith 
                            // However its possible that a space was added, so therefore we now must check to see if the string ends with this indicator
                            if(argStr.EndsWith(startingString, StringComparison.CurrentCultureIgnoreCase))
                            {
                                // Go ahead and just disable Pass Everything now (we could just set startingPassAllIndicator = false to satisfy the condition below, but we truly just want disablePassEverything to disable it at the end of this method)
                                disablePassEverything = true;
                            }
                        }


                        // Fix / Check - 11-13-2016
                        // Before replacing [A] or [ALL], need to check if the string already ends with [A] or [ALL] in the event that no space was provided in the trailing [a] or [all]
                        // Ex: -s [a]-2[a] -d hello     -s would incorrectly include everything else
                        //     -s [a]-a [a] -d hello    This would work, but we don't want to rely on the user having to remember that a space in the trailing [a] is required
                        if(
                            passEverything &&
                            !startingPassAllIndicator &&                    // Ensures that this is not turned off on the first encountered [A]

                             // Using EndsWith in the event that no space was provided at the end and the [A] is right next to the last char and is included in this token
                            StringSupportMethods.EndsWithAny_NoCase(argStr, PASS_ALL_INDICATORS)
                          )
                        {
                            disablePassEverything = true;
                        }

                        argStr = StringSupportMethods.ReplaceAny_NoCase(argStr, PASS_ALL_INDICATORS, "");

                        startOfNewArgument = false;             // Toggle off now, since this is going to be a string of arguments until an ending [a] is found
                    }



                    //
                    // Update the arg value
                    //

                    // Always add a space between the args, which is possible until a new switch is encountered - 11-1-16
                    if (!StringSupportMethods.IsStringNullOrWhitespace(currArg.Value))        // Will be blank if this was a [a]
                    {
                        argStr = " " + argStr;
                    }

                    
                    // Need to trim off any quotes, which may have been supplied in order to pass in a negative value, like "-1", which will be undesirably returned when called GetArgValue (since it cannot be parsed into an int)
                    if (!passEverything)
                    {
                        argStr = StringSupportMethods.TrimQuotes(argStr);
                    }

                    // Store the Argument Value
                    currArg.Value += argStr;

                    // Optionally add it in the list -- Only add in this argument if its a brand new one
                    if (startOfNewArgument && !StringSupportMethods.IsStringNullOrWhitespace(currArg.Value.Trim()))
                    {
                        retList.Add(currArg);

                        // [Re]set flags:
                        currentArgAdded = true;
                        startOfNewArgument = false;
                    }

                    // Lastly, toggle off the special case flag once complete
                    if (disablePassEverything)
                    {
                        passEverything = false;
                    }

                }
            }
        }       // foreach arg







        //
        // Make this 1 arg official.
        //


        // If this loop finished (no more arguments), and the last argument wasn't added (it didn't have a value) then add it in
        if (currentArgAdded == false)
        {
            if (currArg != null)
            {
                retList.Add(currArg);
            }
        }

        // Ensure everything is tidy
        for (int i = 0; i < retList.Count; i++)
        {
            retList[i].Value = retList[i].Value.Trim();
        }

        return retList;
    }

    #endregion

    #region Quick check Public-Static methods (not requiring caller to create an instance of)


    static CmdLineHelper quickSelfInstance = null;

    /// <summary>
    /// A quick check, not requiring the caller to create an instance of CmdLineHelper if they want to simply check if an argument (switch) is present or not
    /// For anything more, the caller should instantiate it own instance
    /// </summary>
    public static bool WasArgSupplied_QuickCheck(params object[] argAliases)
    {
        if(quickSelfInstance == null)
        {
            quickSelfInstance = new CmdLineHelper(Environment.CommandLine);
        }

        return quickSelfInstance.WasArgSupplied(argAliases);
    }


    /// <summary>
    /// A quick Accessor, not requiring the caller to create an instance of CmdLineHelper if they want to simply check what an argument switch's value is (if present)
    /// For anything more, the caller should instantiate it own instance
    /// </summary>
    public string GetArgValue_QuickCheck(params string[] argAliases)
    {
        if (quickSelfInstance == null)
        {
            quickSelfInstance = new CmdLineHelper(Environment.CommandLine);
        }

        return quickSelfInstance.GetArgValue(argAliases);
    }

    #endregion

    #region Public Helper Class (StringSupportMethods)  for Support & Utility Methods


    #region Public static cmdline support methods (SupportMethods class)

    //
    //  These can also be used for creating different Argument lists, from some other supplied list of arguments other than this .exe's command line
    //

    public class SupportMethods
    {
        public static class APIs
        {
            // Added for support of method CommandLineToArgs
            [DllImport("shell32.dll", SetLastError = true)]
            public static extern IntPtr CommandLineToArgvW([MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);
        }

        public static string[] CurrentArgs { get { return Environment.GetCommandLineArgs(); } }     // String array of all arguments
        public static string CurrentArgsStr { get { return Environment.CommandLine; } }             // String of all arguments              (Basically a call to GetCommandLine())
        public static string CurrentArgsStrWithoutExe { get { return RemoveThisExeArg(Environment.CommandLine); } }

        public static string[] CommandLineToArgs(bool preserveQuotes = true)
        {
            return CommandLineToArgs(Environment.CommandLine, preserveQuotes);
        }

        /// <summary>
        /// Command line string --> string[]
        /// Utilizes CommandLineToArgvW win32 API if preserveQuotes = false
        /// </summary>
        /// <returns></returns>
        public static string[] CommandLineToArgs(string commandLine, bool preserveQuotes = true, bool removeFirstArgAsExePath = true)
        {
            if (preserveQuotes)
            {
                // Custom parsing to preserve quotes. Otherwise CommandLineToArgvW will remove them, and it will require quote escaping
                string[] ret = GetArgumentsPreserveQuotes(commandLine);
                
                if(removeFirstArgAsExePath)
                {
                    return RemoveThisExeArg(ret);
                }
                else
                {
                    return ret;
                }
            }
            else
            {
                // Win32 API removes the quotes

                //
                // From / based on: stackoverflow.com/a/749653
                //

                int argCount;
                var argv = APIs.CommandLineToArgvW(commandLine, out argCount);
                if (argv == IntPtr.Zero)
                {
                    //throw new System.ComponentModel.Win32Exception();
                    return new string[0];
                }

                try
                {
                    var args = new string[argCount];
                    for (var i = 0; i < args.Length; i++)
                    {
                        var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                        args[i] = Marshal.PtrToStringUni(p);
                    }

                    if (removeFirstArgAsExePath)
                    {
                        return RemoveThisExeArg(args);
                    }
                    else
                    {
                        return args;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(argv);
                }
            }
        }

        /// <summary>
        /// Splits out the combined File Name + Command Line into their own string
        /// </summary>
        public static void SplitCommandLineInto(string cmdLineAndFile, out string fileName, out string commandLine, bool expandEnvVar = true)
        {
            // Output params
            fileName = "";
            commandLine = "";

            // 1. To be nice, if the user didn't quote the path to the file with spaces in it, then
            // Systematically search for a file in existence with spaces, until either 1 is found, or none is found and the best case scenario is selected
            //
            int startingArrayIndexForCmdLine = 1;

            string fileNameChecking = "";
            string[] splitAppArgs = CmdLineHelper.SupportMethods.CommandLineToArgs(cmdLineAndFile, true, false);
            if (splitAppArgs.Length == 0)
            {
                // Only a file Name was supplied if there are no spaces
                fileName = cmdLineAndFile;
            }
            else
            {
                for (int i = 0; i < splitAppArgs.Length; i++)
                {
                    fileNameChecking += ExpandEnvVar(splitAppArgs[i].Replace("\"", ""), expandEnvVar);
                    if (File.Exists(fileNameChecking))
                    {
                        startingArrayIndexForCmdLine = i + 1;
                        fileName = fileNameChecking;
                        break;
                    }

                    fileNameChecking += " ";
                }
            }

            // Set the output parameter
            if (File.Exists(fileNameChecking))
            {
                fileName = fileNameChecking;
            }
            

            //
            // Otherwise, if the file name wasn't obtained, go select the first non-blank filename:
            //

            if (StringSupportMethods.IsStringNullOrWhitespace(fileName))
            {
                // Build up all the rest of the items in the array specified within the -a "cmd.exe /s" switch
                for (int i = 0; i < splitAppArgs.Length; i++)
                {
                    if (StringSupportMethods.IsStringNullOrWhitespace(fileName))
                    {
                        // skip to the first token and treat it as the file name
                        if (StringSupportMethods.IsStringNullOrWhitespace(splitAppArgs[i]))
                        {
                            continue;
                        }
                        else
                        {
                            startingArrayIndexForCmdLine = i + 1;
                            fileName = splitAppArgs[i];
                        }
                    }
                }
            }


            //
            // Build the command line manually
            //
            for (int i = startingArrayIndexForCmdLine; i < splitAppArgs.Length; i++)
            {
                // Added support for environment variables (such as for 7z which wont support them)
                commandLine += ExpandEnvVar(splitAppArgs[i], expandEnvVar) + " ";
            }


            fileName = fileName.Trim();

            // Necessary or else a space will be at the end of the CmdLine when specifying [a] [a] for example. Can cause issues with some applications like Tweaking Registry Backup
            commandLine = commandLine.Trim();

        }

        /// <summary>
        /// Removes the file name passed in
        /// </summary>
        public static string SplitOutCmdLine(string cmdLineAndFile, bool expandEnvVar = true)
        {
            string ret, dummy;
            SplitCommandLineInto(cmdLineAndFile, out dummy, out ret, expandEnvVar);
            return ret;
        }

        /// <summary>
        /// Removes the command line passed in
        /// </summary>
        public static string SplitOutFileName(string cmdLineAndFile, bool expandEnvVar = true)
        {
            string ret, dummy;
            SplitCommandLineInto(cmdLineAndFile, out ret, out dummy, expandEnvVar);
            return ret;
        }

        /// <summary>
        /// Removes the first argument if it is this exe
        /// </summary>
        public static string[] RemoveThisExeArg(string[] args)
        {
            string[] ret = args;
            List<string> retList = new List<string>();
            if (args.Length > 0 &&
                (args[0].Replace("\"", "").ToLower() == thisExe.ToLower() ||
                 args[0].Replace("\"", "").ToLower() == Path.GetFileName(thisExe).ToLower() ||      // could just be the .exe
                 System.Diagnostics.Debugger.IsAttached))        // The .exe will be named .vshost.exe so it won't match. Therefore must proceed anyway
            {
                retList = new List<string>(ret);
                retList.RemoveAt(0);
                ret = retList.ToArray();
            }

            return ret;
        }

        public static string RemoveThisExeArg(string args)
        {
            string[] argsAsArr = SupportMethods.CommandLineToArgs(args, true, true);        // 3rd arg calls RemoveThisExeArg accepting a string[] 

            // Glue back together the result
            string all = "";
            foreach(string s in argsAsArr)
            {
                all += s + " ";
            }

            return all;
        }

        #region Private Helper Methods

        /// <summary>
        /// Parses the arguments preserving quotes.
        /// To call this publically use CommandLineToArgs(true)
        /// </summary>
        private static string[] GetArgumentsPreserveQuotes(string commandLine)
        {
            // From:
            //      stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp 

            char[] parmChars = commandLine.ToCharArray();
            bool inQuote = false;
            for (int index = 0; index < parmChars.Length; index++)
            {
                if (parmChars[index] == '"')
                    inQuote = !inQuote;
                if (!inQuote && Char.IsWhiteSpace(parmChars[index]))
                    parmChars[index] = '\n';
            }

            string charsAsString = new string(parmChars);
            string[] arr = charsAsString.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return arr;
        }

        private static string ExpandEnvVar(string input, bool expand = true)
        {
            if (expand)
            {
                return Environment.ExpandEnvironmentVariables(input);
            }
            else
            {
                return input;
            }
        }

        #endregion

    }

    #endregion


    #region Public String Support Methods (StringSupportMethods class)

    public static class StringSupportMethods
    {
        /// <summary>
        /// .NET 3.5 compat
        /// </summary>
        public static bool IsStringNullOrWhitespace(string input)
        {
            // From: gist.github.com/jwwishart/858385
            return input == null || input.All(char.IsWhiteSpace);
        }

        public static string TrimQuotes(string str)
        {
            string ret = str;
            if (ret.StartsWith("\"") && ret.Length > 1)
            {
                ret = ret.Substring(1);
            }

            if (ret.EndsWith("\"") && ret.Length > 1)
            {
                ret = SubstringIndex(ret, 0, ret.Length - 1 - 1);       // -1 for 0-based, -1 for removing the quote
            }

            return ret;
        }

        public static string ReplaceNoCase(string str, string oldValue, string newValue, StringComparison comparison = StringComparison.CurrentCultureIgnoreCase)
        {
            // Allows for specification of Case-Insensitive replacement. .Replace is Case-sensitive
            //      From: stackoverflow.com/questions/244531/is-there-an-alternative-to-string-replace-that-is-case-insensitive

            StringBuilder sb = new StringBuilder();

            int previousIndex = 0;
            int index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

        public static bool ContainsNoCase(string str, string contains)
        {
            // stackoverflow.com/questions/444798/case-insensitive-containsstring
            return str.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string SubstringIndex(string str, int startingIndex, int endingIndex)
        {
            int charLengthToGo = endingIndex - startingIndex + 1;
            if (charLengthToGo >= 0)
            {
                return str.Substring(startingIndex, charLengthToGo);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Extracts two strings around a given starting and ending position of the delimiter to remove
        /// </summary>
        /// <returns>String array where 0 = start of string | 1 = extracted portion | 2 = end of string</returns>
        public static string[] ExtractAround(string input, int startPos, int endPos, int endingDelimLength)
        {
            string start = "", midRemoved = "", end = "";

            try
            {
                // Some basic inital checks
                if (startPos > 0 && endPos > 0 && endPos > startPos)
                {
                    //int length = (endPos - startPos);

                    start = SubstringIndex(input, 0, startPos - 1);                 // -1 to exclude last char
                    midRemoved = SubstringIndex(input, startPos, endPos - 1);       // -1 to exclude last char
                    end = input.Substring(endPos + endingDelimLength);
                }
            }
            catch (Exception ex)
            {
                string s = ex.Source;       // Supress warning
            }

            return new string[3] { start, midRemoved, end };
        }

        public static string ReplaceAny_NoCase(string input, string[] stringsToReplace, string replceAllWith)
        {
            string all = input;

            foreach (string toReplace in stringsToReplace)
            {
                all = ReplaceNoCase(all, toReplace, replceAllWith);
            }

            return all.Trim();
        }

        public static bool StartsWithAny_NoCase(string input, string[] startsWithStrings)
        {
            input = input.Trim();

            foreach (string s in startsWithStrings)
            {
                if (!StringSupportMethods.IsStringNullOrWhitespace(s))           // EndsWith returns true for ending with "" blank strings when the string has data. Doing this check just in case StartsWith does too
                {
                    if (input.StartsWith(s, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool EndsWithAny_NoCase(string input, string[] startsWithStrings)
        {
            input = input.Trim();

            foreach (string s in startsWithStrings)
            {
                if (!StringSupportMethods.IsStringNullOrWhitespace(s))           // EndsWith returns true for ending with "" blank strings when the string has data.
                {
                    if (input.EndsWith(s, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the specific string that the supplied string starts with
        /// </summary>
        public static string FindStartingString(string input, string[] startingPossibilites)
        {
            List<string> strings = new List<string>(startingPossibilites);
            List<string> stringsByLength = strings.OrderBy(x => x.Length).Reverse().ToList();

            foreach (string s in stringsByLength)
            {
                if (!StringSupportMethods.IsStringNullOrWhitespace(s))           // EndsWith returns true for ending with "" blank strings when the string has data. Doing this check just in case StartsWith does too
                {
                    if (input.StartsWith(s, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return s;
                    }
                }

            }

            return "";
        }
    }

    #endregion


    #endregion
}
