using HtmlAgilityPack;
using LoggerWindows;
using LogInterface;
using Newtonsoft.Json;
using NPOI.HSSF.UserModel;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoubanBooks
{
    class Program
    {
        static void Main(string[] args)
        {
            Logs.LogFactory = new NLogFactory();

            var logger = Logs.LogFactory.GetLogger<Program>();

            var tagsStr = File.ReadAllText("Tags.json");
            var tags = JsonConvert.DeserializeObject<List<string>>(tagsStr);

            var spider = new DoubanBookSpider();

            logger.Info("Start");
            var books = spider.CaptureAsync(tags).Result;
            //var books = new Dictionary<DoubanBook, bool> { { new DoubanBook { Title = "" }, true }, { new DoubanBook { Title = "" }, true } };
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
                row.CreateCell(5).SetCellValue(p.PublishDate);
            }

            var stream = new FileStream($"Books-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss.fff")}.xls", FileMode.CreateNew);
            work.Write(stream);
            stream.Close();

            logger.Info("Done");
            Console.ReadKey();
        }
    }

    public class DoubanBookSpider
    {
        private static ILogger _logger = Logs.LogFactory.GetLogger<DoubanBookSpider>();

        private const string BASE_URL = "https://book.douban.com/tag/";
        private const int MIN_SCORE = 5;

        private ConcurrentDictionary<DoubanBook, bool> _books { get; set; } = new ConcurrentDictionary<DoubanBook, bool>();
        private ConcurrentDictionary<string, bool> _tags { get; set; } = new ConcurrentDictionary<string, bool>();
        private int _bookCount = 0;
        private Random _random = new Random();

        public async Task<IDictionary<DoubanBook,bool>> CaptureAsync(List<string> tags)
        {
            foreach (var tag in tags)
            {
                this.ProcessSingleTag(tag);
            }

            return this._books;
        }

        private void ProcessSingleTag(string tag)
        {
            if (_tags.ContainsKey(tag))
            {
                return;
            }
            else
            {
                _tags.TryAdd(tag, false);
            }

            HtmlNode tagFirstPage = null;
            var index = 0;
            while (true)
            {
                // 防止ip被封
                var waitTime = _random.Next(1000, 3000);
                Task.Delay(waitTime).Wait();

                var htmlWeb = new HtmlWeb();
                var url = BASE_URL + tag + $"?start={index}";

                HtmlDocument doc = null;
                try
                {
                    doc = htmlWeb.Load(url);
                }
                catch (Exception e)
                {
                    _logger.Warn(e, "load tag page error.{0}", url);
                    Task.Delay(1000).Wait();
                    continue;
                }
                
                _logger.Info("parse {0}", url);

                var rootNode = doc.DocumentNode;
                if (tagFirstPage == null)
                    tagFirstPage = rootNode;

                var items = rootNode.SelectNodes("/html/body/div[3]/div[1]/div/div[1]/div/ul/li");

                if (items == null || items.Count == 0)
                    break;

                foreach (var item in items)
                {
                    DoubanBook book = null;
                    try
                    {
                        book = this.ProcessSingleBook(item);
                        if (book.Score < MIN_SCORE)
                            continue;
                    }
                    catch (Exception e)
                    {
                        _logger.Warn(e, "parse book error.{0}", url);
                        continue;
                    }
                    var success = this._books.TryAdd(book, false);
                    if (success)
                    {
                        _bookCount++;
                        _logger.Info($"add book:{_bookCount}.{book.Title}");
                    }
                    else
                        _logger.Info($"repeat.skip.{book.Title}");
                }

                index += items.Count;
            }
            this.ProcessRelatedTags(tagFirstPage);
        }

        private void ProcessRelatedTags(HtmlNode rootNode)
        {
            var relatedTagsNodes = rootNode.SelectNodes("/html/body/div[3]/div[1]/div/div[2]/div[2]/a");
            if (relatedTagsNodes == null)
            {
                _logger.Warn("未发现相关Tag。ip可能被豆瓣屏蔽了");
                return;
            }
            //var tags = new List<string>();
            foreach (var node in relatedTagsNodes)
            {
                var tag = node.InnerText.Trim();
                //tags.Add(tag);
                this.ProcessSingleTag(tag);
            }
        }

        private DoubanBook ProcessSingleBook(HtmlNode item)
        {
            var title = item.SelectSingleNode("div[2]/h2/a").GetAttributeValue("title", "none");

            var link = item.SelectSingleNode("div[2]/h2/a").GetAttributeValue("href", "none");

            var publishHouseAndTimeStr = item.SelectSingleNode("div[2]/div[1]").InnerText;
            var publishHouseAndTimeArr = publishHouseAndTimeStr.Split('/');
            string publishTime = "";
            string publishHouse = "";
            if (publishHouseAndTimeArr.Length == 2)
                publishTime = publishHouseAndTimeArr[0];
            else if (publishHouseAndTimeArr.Length == 4)
            {
                publishTime = publishHouseAndTimeArr[2];
                publishHouse = publishHouseAndTimeArr[1];
            }
            else if (publishHouseAndTimeArr.Length == 5)
            {
                publishTime = publishHouseAndTimeArr[3];
                publishHouse = publishHouseAndTimeArr[2];
            }

            var scoreNode = item.SelectSingleNode("div[2]/div[2]/span[2]");
            float score = -1;
            if (scoreNode != null)
                score = float.Parse(scoreNode.InnerText);

            var scoreNumberNode = item.SelectSingleNode("div[2]/div[2]/span[3]");
            int scoreNumber = -1;
            if (scoreNumberNode != null)
            {
                var scoreNumberStr = scoreNumberNode.InnerText.Trim();
                if (scoreNumberStr != "少于10人评价")
                    scoreNumber = int.Parse(scoreNumberStr.Substring(1, scoreNumberStr.IndexOf(scoreNumberStr.Last(o => char.IsDigit(o)))));
            }

            return new DoubanBook
            {
                Title = title,
                Link = link,
                Score = score,
                ScoreNumber = scoreNumber,
                PublishDate = publishTime,
                PublishHouse = publishHouse
            };
        
        }
    }

    public class DoubanBook : IEqualityComparer<DoubanBook>
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public float Score { get; set; }
        public int ScoreNumber { get; set; }
        public string PublishDate { get; set; }
        public string PublishHouse { get; set; }

        public bool Equals(DoubanBook x, DoubanBook y)
        {
            return x.Link == y.Link && x.Title == y.Title;
        }

        public int GetHashCode(DoubanBook obj)
        {
            return this.Link.GetHashCode() + this.Title.GetHashCode();
        }
    }
}
