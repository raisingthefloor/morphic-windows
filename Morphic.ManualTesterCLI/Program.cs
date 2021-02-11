using System;
using System.Threading.Tasks;

namespace Morphic.ManualTesterCLI
{
    class Program
    {
        static RegistryManager manager = new RegistryManager();
        static string appname = "morphictest";
        static async Task Main(string[] args)
        {
            if (args.Length > 1)
            {
                try
                {
                    if (!manager.Load(args[0]))
                    {
                        Console.WriteLine("[ERROR]: Could not load file {0} as a valid solutions registry JSON file. Check filename and try again.", args[0]);
                        return;
                    }
                    switch (args[1])
                    {
                        case "list":
                            switch (args.Length)
                            {
                                case 2:
                                    manager.List();
                                    break;
                                case 3:
                                    manager.ListSpecific(args[2]);
                                    break;
                                default:
                                    Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: {0} <filename> list [solution]", appname);
                                    break;
                            }
                            break;
                        case "listsol":
                            manager.ListSolutions();
                            break;
                        case "info":
                            if (args.Length == 4)
                            {
                                manager.Info(args[2], args[3]);
                            }
                            else
                            {
                                Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: {0} <filename> info <solution> <preference>", appname);
                            }
                            break;
                        case "get":
                            switch (args.Length)
                            {
                                case 2:
                                    await manager.Get();
                                    break;
                                case 3:
                                    await manager.Get(args[2]);
                                    break;
                                case 4:
                                    await manager.Get(args[2], args[3]);
                                    break;
                                default:
                                    Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: {0} <filename> get [solution] [preference]", appname);
                                    break;
                            }
                            break;
                        case "set":
                            if (args.Length == 5)
                            {
                                await manager.Set(args[2], args[3], args[4]);
                            }
                            else
                            {
                                Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: {0} <filename> set <solution> <preference> <value>", appname);
                            }
                            break;
                        case "help":
                            helpdoc(true);
                            break;
                        default:
                            Console.WriteLine("[ERROR]: Unrecognized command. Commands: list, listsol, info, get, set, help");
                            break;
                    }
                }
                catch
                {
                    Console.WriteLine("[ERROR]: Could not load file {0} as a valid solutions registry JSON file. Check filename and try again.", args[0]);
                }
            }
            else if (args.Length == 1)
            {
                if (args[0] == "help")
                {
                    helpdoc(true);
                }
                else
                {
                    try
                    {
                        try
                        {
                            if (!manager.Load(args[0]))
                            {
                                Console.WriteLine("[ERROR]: Could not load file {0} as a valid solutions registry JSON file. Check filename and try again.", args[0]);
                                return;
                            }
                        }
                        catch
                        {
                            Console.WriteLine("[ERROR]: Could not load file {0} as a valid solutions registry JSON file. Check filename and try again.", args[0]);
                            return;
                        }
                        Console.Clear();
                        Console.WriteLine("Solutions file loaded successfully.");
                        Console.WriteLine("Welcome to the Morphic Manual Solutions Registry Tester.");
                        Console.WriteLine("Morphic is Copyright 2020 Raising the Floor - International\n");
                        var loop = true;
                        while (loop)
                        {
                            Console.WriteLine("Please enter a command, type 'help' to list all commands:");
                            Console.Write("> ");
                            var line = Console.ReadLine();
                            var nargs = line.Split(" ");
                            if (nargs.Length > 0)
                            {
                                switch (nargs[0])
                                {
                                    case "list":
                                        switch (nargs.Length)
                                        {
                                            case 1:
                                                manager.List();
                                                break;
                                            case 2:
                                                manager.ListSpecific(nargs[1]);
                                                break;
                                            default:
                                                Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: list [solution]");
                                                break;
                                        }
                                        break;
                                    case "listsol":
                                        manager.ListSolutions();
                                        break;
                                    case "info":
                                        if (nargs.Length == 3)
                                        {
                                            manager.Info(nargs[1], nargs[2]);
                                        }
                                        else
                                        {
                                            Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: info <solution> <preference>");
                                        }
                                        break;
                                    case "get":
                                        switch (nargs.Length)
                                        {
                                            case 1:
                                                await manager.Get();
                                                break;
                                            case 2:
                                                await manager.Get(nargs[1]);
                                                break;
                                            case 3:
                                                await manager.Get(nargs[1], nargs[2]);
                                                break;
                                            default:
                                                Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: get [solution] [preference]");
                                                break;
                                        }
                                        break;
                                    case "set":
                                        if (nargs.Length == 4)
                                        {
                                            await manager.Set(nargs[1], nargs[2], nargs[3]);
                                        }
                                        else
                                        {
                                            Console.WriteLine("[ERROR]: Incorrect number of parameters. Use: set <solution> <preference> <value>");
                                        }
                                        break;
                                    case "help":
                                        helpdoc(false);
                                        break;
                                    case "quit":
                                    case "exit":
                                        loop = false;
                                        break;
                                    default:
                                        Console.WriteLine("[ERROR]: Invalid command");
                                        break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        Console.WriteLine("[ERROR]: There was a problem reading data from file {0}.", args[0]);
                    }
                }
            }
            else
            {
                Console.WriteLine("[ERROR]: Valid solutions registry file path required. Use: {0} <filename>", appname);
            }
        }

        private static void helpdoc(bool cmdline)
        {
            Console.Clear();
            var header = "";
            if (cmdline)
            {
                header = appname + " <filename> ";
            }
            Console.WriteLine("\t{0}list [solution]:", header);
            Console.WriteLine("Lists all solutions and settings from the registry, or if provided a solution, only lists settings for that solution\n");
            Console.WriteLine("\t{0}listsol:", header);
            Console.WriteLine("Lists all solutions without their settings for quick lookup\n");
            Console.WriteLine("\t{0}info <solution> <preference>:", header);
            Console.WriteLine("Gives you verbose info on a particular setting in the registry\n");
            Console.WriteLine("\t{0}get [solution] [preference]:", header);
            Console.WriteLine("Lists the current value of a setting, all settings in a solution, or all settings in the registry, depending on provided parameters\n");
            Console.WriteLine("\t{0}set <solution> <preference> <value>:", header);
            Console.WriteLine("Changes the value of a setting, if possible\n");
            if (cmdline)
            {
                return;
            }
            Console.WriteLine("\texit:");
            Console.WriteLine("Ends the program\n");
        }
    }
}
