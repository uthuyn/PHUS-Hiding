﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
static class ProgramConsole
{
    static PHUSHiding algo = new PHUSHiding();
    static void Main(string[] args)
    {
        string txtInputFile = args[0];
        if (string.IsNullOrEmpty(txtInputFile))
        {
            Console.WriteLine("Dataset file is required");
            Console.ReadKey();
            return;
        }
        string txtminUtl = args[1];
        if (string.IsNullOrEmpty(txtminUtl))
        {
            Console.WriteLine("Minimum utility file is required");
            Console.ReadKey();
            return;
        }
        string txtMaxLength = args[2];
        float MIN_UTL = float.Parse(txtminUtl);
        float minPdc = float.Parse(args[3].ToString());
        float maxPdc = float.Parse(args[4].ToString());
        float minAvg = float.Parse(args[5].ToString());
        float maxAvg = float.Parse(args[6].ToString());

        var path = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        var dataPath = string.Empty;
        dataPath = path.Substring(0, path.LastIndexOf("PHUS-Hiding")) + "Data";
#if DEBUG
        dataPath = path.Substring(0, path.IndexOf("\\PHUS-Hiding\\")) + "\\Data";
#endif

        string external = txtInputFile + "_ExternalUtility";

        var filePath = string.Format(@"{0}\{1}.txt", dataPath, txtInputFile);
        external = string.Format(@"{0}\{1}.txt", dataPath, external);

        // the path for saving the patterns found
        var uid = DateTime.Now.ToString("ddMMyyyy_hhmmss");
        Console.WriteLine($"***** Start hiding [{uid}]: {string.Join("\t", args)}");

        algo.startTimestamp = DateTimeHelperClass.CurrentUnixTimeMillis();
        var database = algo.loadDataWithInternalExternal(external, filePath, (int)MIN_UTL);

        if (txtMaxLength != "0")
            algo.setMaxPatternLength(int.Parse(txtMaxLength));

        Console.WriteLine("\t> Sequence count: " + database.Count);
        string output = ".//output.txt";
        algo.FirstWriteData = true;
        algo.WillWriteModifiedData = false;
        algo.runAlgorithm(database, output, minPdc, maxPdc, minAvg, maxAvg, uid);

        double runTime = (algo.endTimestamp - algo.startTimestamp);
        long memory = (algo.currentProc.PrivateMemorySize64 / 1024 / 1024);
        if (!File.Exists(@".//PHUS-Hiding.Results.txt"))
        {
            File.AppendAllText(@".//PHUS-Hiding.Results.txt", $"TIME\tUID\tDATASET\tHUSPs\tEXEC TIME\tHIDING TIME\tMEM\tMC\tMIN UTL\tMAX_LENGTH\r\n");
        }
        File.AppendAllText(@".//PHUS-Hiding.Results.txt", $"{DateTime.Now.ToString("ddMMyyyy_hhmmss")}\t{uid}\t{txtInputFile}\t{algo.PHUSPCount}\t{runTime.ToString("n0")}\t{algo.HidingTotalTime}\t{memory}\t{algo.DbDelta.ToString("n0")}\t{MIN_UTL.ToString("n0")}\t{txtMaxLength}\r\n");
        Console.WriteLine($"\t> Done [{uid}]");

#if DEBUG
        Console.ReadKey();
#endif
    }
}