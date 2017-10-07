using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;

namespace ASXData
{
    class Program
    {

        /*
         https://www.ing.com.au/pdf/superannuation/Shares_ETFs_LICs.pdf
         https://www.asxhistoricaldata.com/
         
         */
        static void Main(string[] args)
        {
            string ASXDailyFilesPath = ConfigurationManager.AppSettings["ASXDailyFilesPath"];
            string ASXProcessedFilesPath = ConfigurationManager.AppSettings["ASXProcessedFilesPath"];
            string ProcessOnlyList = ConfigurationManager.AppSettings["ProcessOnlyList"];
            ProcessOnlyList = "," + ProcessOnlyList + ",";

            List<string> ASXDailyFilesPathFiles = Directory.EnumerateFiles(ASXDailyFilesPath).OrderByDescending(filename => filename).ToList();
            List<string> ASXProcessedFilesPathFiles = Directory.EnumerateFiles(ASXProcessedFilesPath).OrderBy(filename => filename).ToList();

            //empty dest dir
            System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(ASXProcessedFilesPath);
            foreach (System.IO.FileInfo file in directory.GetFiles())
                file.Delete();

            foreach (var file in ASXDailyFilesPathFiles)
            {

                Console.WriteLine("Processing File: " + file);
                using (TextFieldParser parserOrig = new TextFieldParser(file))
                {
                    parserOrig.TextFieldType = FieldType.Delimited;
                    parserOrig.SetDelimiters(",");
                    while (!parserOrig.EndOfData)
                    {

                        var lineDest=parserOrig.ReadLine();
                        var FileName=lineDest.Substring(0, lineDest.IndexOf(','));
                        lineDest = lineDest.Replace(FileName + ",", "");

                        if (ProcessOnlyList != string.Empty)
                        {
                            if (!ProcessOnlyList.Contains(","+FileName+","))
                                continue;
                        }

                        var DateOrig = lineDest.Substring(0, lineDest.IndexOf(','));
                        var DateDest = string.Format("{0}-{1}-{2}", DateOrig.Substring(0, 4), DateOrig.Substring(4, 2), DateOrig.Substring(6, 2));
                        lineDest = lineDest.Replace(DateOrig, DateDest);

                        var FileNameDest = ASXProcessedFilesPath + @"\" + FileName + ".csv";
                        bool FileExists = File.Exists(FileNameDest);
                        TextWriter tw;
                        try
                        {
                            tw = new StreamWriter(FileNameDest, true);
                        }
                        catch (Exception)
                        {
                            FileNameDest = ASXProcessedFilesPath + @"\_" + FileName + ".csv";
                            tw = new StreamWriter(FileNameDest, true);
                        }


                        if (!FileExists)
                            tw.WriteLine("Date,Open,High,Low,Close,Volume");
                        tw.WriteLine(lineDest);
                        tw.Close(); 
                        
                    }
                }
            }

            //check that there is a minimum histroy of 2 years
            int MinNumRecords = int.Parse(ConfigurationManager.AppSettings["MinNumRecords"]);
            ASXProcessedFilesPathFiles = Directory.EnumerateFiles(ASXProcessedFilesPath).OrderBy(filename => filename).ToList();
            List<string> NotEnoughHistoryList= new List<string>();
            foreach (var file in ASXProcessedFilesPathFiles)
            {
                using (TextFieldParser parserOrig = new TextFieldParser(file))
                {
                    string content=parserOrig.ReadToEnd();
                    var contentsplit=content.Split('\r');
                    if (contentsplit.Count() < MinNumRecords)
                    {
                        int start = file.LastIndexOf("\\")+1;
                        int end = file.LastIndexOf(".");
                        string name=file.Substring(start, file.Length-start-4);
                        NotEnoughHistoryList.Add(name);
                            
                    }

                }
            }


            //Check that the items in the ING list exists in the ASX list
            List<string> NotInASXList= new List<string>();
            var ProcessOnlyListSplit = ProcessOnlyList.Split(',');
            using (TextFieldParser parserOrig = new TextFieldParser(ASXDailyFilesPathFiles[0]))
            {
                string content=parserOrig.ReadToEnd();
                foreach (var ticker  in ProcessOnlyListSplit)
                {
                    if(!string.IsNullOrEmpty(ticker))
                        if(!content.Contains(ticker + ","))
                            NotInASXList.Add(ticker);

                }

            }
            

            //create assetHistory
            string ZorroPath = ConfigurationManager.AppSettings["ZorroPath"];
            File.Delete(ZorroPath + "\\history\\AssetsZ9.csv");
            TextWriter tw2 = new StreamWriter(ZorroPath + "\\history\\AssetsZ9.csv", true);
            tw2.WriteLine("Name,Price,Spread,RollLong,RollShort,PIP,PIPCost,MarginCost,Leverage,LotAmount,Commission,Symbol,Type");
            foreach (var ticker  in ProcessOnlyListSplit)
            {
                if (!string.IsNullOrEmpty(ticker))
                    if (!NotInASXList.Contains(ticker) && !NotEnoughHistoryList.Contains(ticker))
                        tw2.WriteLine(ticker + ",50,0.1,0,0,0.01,0.01,0,1,1,0.2,,0");

            }
            tw2.Close();

            //copy all processed files
            FileSystem.CopyDirectory(ASXProcessedFilesPath, ZorroPath + "\\ASXProcessed");

        }
    }
}
