using LoggerWindows;
using LogInterface;
using Newtonsoft.Json;
using NPOI.HSSF.UserModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace DoubanBooks
{
    class Program
    {
        static void Main(string[] args)
        {
            Logs.LogFactory = new NLogFactory();

            var logger = Logs.LogFactory.GetLogger<Program>();

            var dic = new ConcurrentDictionary<DoubanBook, bool>();

            var files = Directory.GetFiles("Books");

            foreach (var file in  files)
            {
                var stream1 = File.OpenRead(file);
                var work1 = new HSSFWorkbook(stream1);

                var sheet1 = work1.GetSheetAt(0);

                for (int x = 0; x <= sheet1.LastRowNum; ++x)
                {
                    var row1 = sheet1.GetRow(x);
                    if (row1 == null)
                        continue;
                    var book1 = new DoubanBook
                    {
                        Title = row1.GetCell(0).ToString(),
                        Score = float.Parse(row1.GetCell(1).ToString()),
                        ScoreNumber = int.Parse(row1.GetCell(2).ToString()),
                        Link = row1.GetCell(3).ToString(),
                        PublishHouse = row1.GetCell(4).ToString(),
                        PublishDate = DateTime.Parse(row1.GetCell(5).ToString()),
                        Desc = row1.GetCell(6).ToString(),
                    };
                    dic.TryAdd(book1, false);
                }
                stream1.Close();
            }

            var tagManager = new TagManager();
            tagManager.Init();
            var spider = new DoubanBookSpider(tagManager, dic);

            logger.Info("Start");
            var _exitSync = new object();
            Monitor.Enter(_exitSync);
            var tokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => {
                logger.Info("Cancelling....");
                tokenSource.Cancel();

                Monitor.Enter(_exitSync);

                Monitor.Wait(_exitSync);

                Monitor.Exit(_exitSync);

                logger.Info("Cancelled");
            };
            var books = spider.CaptureAsync(tokenSource.Token).Result;
           
            //var books = new Dictionary<DoubanBook, bool> { { new DoubanBook { Title = "" }, true }, { new DoubanBook { Title = "4" }, true } };
            var work = new HSSFWorkbook();
            var sheet = work.CreateSheet();
            int i = 0;
            foreach (var p in books.Keys)
            {
                var row = sheet.CreateRow(i++);
                row.CreateCell(0).SetCellValue(p.Title);
                row.CreateCell(1).SetCellValue(p.Score);
                row.CreateCell(2).SetCellValue(p.ScoreNumber);
                row.CreateCell(3).SetCellValue(p.Link);
                row.CreateCell(4).SetCellValue(p.PublishHouse);
                row.CreateCell(5).SetCellValue(p.PublishDate.ToLongDateString());
                row.CreateCell(6).SetCellValue(p.Desc);
            }

            var stream = new FileStream($".\\Books\\Books-{DateTime.Now:yyyy-MM-dd-HH-mm-ss.fff}.xls", FileMode.CreateNew);
            work.Write(stream);
            stream.Close();

            Monitor.Pulse(_exitSync);
            Monitor.Exit(_exitSync);

            logger.Info("Done");
            Console.ReadKey();
        }
    }

}
