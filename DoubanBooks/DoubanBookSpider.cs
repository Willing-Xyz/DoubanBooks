using HtmlAgilityPack;
using LogInterface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static DoubanBooks.TagManager;

namespace DoubanBooks
{
    public class DoubanBookSpider
    {
        private static ILogger _logger = Logs.LogFactory.GetLogger<DoubanBookSpider>();

        private const string BASE_URL = "https://book.douban.com/tag/";
        private const int MIN_SCORE = int.MinValue;

        private ConcurrentDictionary<string, DoubanBook> _books { get; set; } = new ConcurrentDictionary<string, DoubanBook>();
        private TagManager _tagManager;

        private int _bookCount = 0;
        private Random _random = new Random();

        public event Action<IDictionary<string, DoubanBook>> OnDataChanged;

        public DoubanBookSpider(TagManager tagManager, ConcurrentDictionary<string, DoubanBook> books)
        {
            _books = books;
            _tagManager = tagManager;
        }

        public async Task<IDictionary<string, DoubanBook>> CaptureAsync(CancellationToken token)
        {
            Tag tag = null;
            while ((tag = _tagManager.GetUnProcessTag()) != null && !token.IsCancellationRequested)
            {
                this.ProcessSingleTag(tag, token);
                this.OnDataChanged?.Invoke(_books);
            }

            return this._books;
        }

        private void ProcessSingleTag(Tag tag, CancellationToken token)
        {
            _logger.Info($"process tag.{tag.TagName}");

            int pageSize = 0;
            try
            {
                pageSize = this.GetTagPageSize(tag);
            }
            catch (Exception e)
            {
                _logger.Error(e, "获取页面大小失败:{0}", tag.TagName);
                return;
            }

            DoubanBook lastBook = null;
            //HtmlNode tagFirstPage = null;
            while (!token.IsCancellationRequested && pageSize >= 0)
            {
                // 防止ip被封
             

                var items = this.GetItems(tag, pageSize);
                pageSize--;

                if (items == null)
                    continue;

                foreach (var item in items)
                {
                    DoubanBook bookBaseInfo = null;
                    try
                    {
                        bookBaseInfo = this.ProcessSingleBook(item);
                        lastBook = bookBaseInfo;
                        //if (bookBaseInfo.PublishDate < tag.CompletedTime)
                        //{
                        //    _logger.Info($"到达Tag:{tag.TagName}截止时间:{tag.CompletedTime.ToLongDateString()}");
                        //    _tagManager.AddCompleteTag(tag.TagName, lastBook.PublishDate);
                        //    return;
                        //}
                        if (bookBaseInfo.Score < MIN_SCORE)
                        {
                            _logger.Info($"ignore book.because score:{bookBaseInfo.Score}.title:{bookBaseInfo.Title}");
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Warn(e, "parse book error");
                        continue;
                    }
                    var success = this._books.TryAdd(bookBaseInfo.Link, bookBaseInfo);
                    if (success)
                    {
                        _bookCount++;
                        _logger.Info($"add book:{_bookCount}.{bookBaseInfo.Title}");
                    }
                    else
                        _logger.Info($"repeat.skip.{bookBaseInfo.Title}");

                    if (!_books[bookBaseInfo.Link].Tags.Contains(tag.TagName))
                        _books[bookBaseInfo.Link].Tags.Add(tag.TagName);
                }

                //index += items.Count;
            }
            //this.ProcessRelatedTags(tagFirstPage);
            if (lastBook != null)
                _tagManager.AddCompleteTag(tag.TagName, lastBook.PublishDate);
            else
                _tagManager.AddCompleteTag(tag.TagName, DateTime.MinValue);
        }

        private int GetTagPageSize(Tag tag)
        {
            var htmlWeb = new HtmlWeb();
            var url = BASE_URL + tag.TagName + $"?type=R";

            HtmlDocument doc = htmlWeb.Load(url);

            var divNode = doc.DocumentNode.SelectSingleNode(@"//*[@id=""subject_list""]/div[2]");
            if (divNode == null)
                return 0;
            var links = divNode.SelectNodes("a");
            if (links.Count > 0)
            {
                var pageSize = int.Parse(links.Last().InnerText);
                var page = pageSize;
                var upperPage = pageSize;
                var lowerPage = 0;
                while (true)
                {
                    var item = this.GetItems(tag,  page);
                    if (item == null || item.Count == 0)
                    {
                        if (page <= 0)
                            return 0;
                        upperPage = page;
                        page = Math.Max(page / 2, lowerPage);
                        continue;
                    }
                    var book = this.ProcessSingleBook(item.First());
                    if (book.PublishDate >= tag.CompletedTime)
                    {
                        lowerPage = Math.Max(lowerPage, page);
                        page = (int)((upperPage - page) / 2 + page);
                    }
                    else
                    {
                        upperPage = Math.Min(upperPage, page);
                        page = (int)((page - lowerPage) / 2 + lowerPage);
                    }
                    if (upperPage - lowerPage <= 2)
                        return upperPage;
                }
              
            }
            return 0;
        }

        private HtmlNodeCollection GetItems(Tag tag,  int pageSize)
        {
            //var waitTime = _random.Next(3000, 8000);
            //Task.Delay(waitTime).Wait();

            var htmlWeb = new HtmlWeb();
            var url = BASE_URL + WebUtility.UrlEncode(tag.TagName) + $"?type=R&start={pageSize * 20}";

            HtmlDocument doc = null;
            try
            {
                doc = htmlWeb.Load(url);
            }
            catch (Exception e)
            {
                _logger.Warn(e, "load tag page error.{0}", url);
                return null;
            }

            _logger.Info("parse {0}", url);

            var rootNode = doc.DocumentNode;
            //if (tagFirstPage == null)
            //{
            //    tagFirstPage = rootNode;
            //}

            return rootNode.SelectNodes("/html/body/div[3]/div[1]/div/div[1]/div/ul/li");
        }

        //private void ProcessRelatedTags(HtmlNode rootNode)
        //{
        //    var relatedTagsNodes = rootNode.SelectNodes("/html/body/div[3]/div[1]/div/div[2]/div[2]/a");
        //    if (relatedTagsNodes == null)
        //    {
        //        _logger.Warn("未发现相关Tag。ip可能被豆瓣屏蔽了");
        //        return;
        //    }
        //    //var tags = new List<string>();
        //    foreach (var node in relatedTagsNodes)
        //    {
        //        var tag = node.InnerText.Trim();
        //        //tags.Add(tag);

        //        this.ProcessSingleTag(tag);
        //    }
        //}

        private DoubanBook ProcessSingleBook(HtmlNode item)
        {
            var title = item.SelectSingleNode("div[2]/h2/a").GetAttributeValue("title", "none");

            var link = item.SelectSingleNode("div[2]/h2/a").GetAttributeValue("href", "none");

            var publishHouseAndTimeStr = item.SelectSingleNode("div[2]/div[1]").InnerText;
            var publishHouseAndTimeArr = publishHouseAndTimeStr.Split('/');
            string publishTime = "";
            string publishHouse = "";
            if (publishHouseAndTimeArr.Length == 2)
            {
                DateTime tmp;
                if (DateTime.TryParse(publishHouseAndTimeArr[0], out tmp))
                {
                    publishTime = publishHouseAndTimeArr[0];
                }
                else
                {
                    publishTime = publishHouseAndTimeArr[1];
                }
            }
            else if (publishHouseAndTimeArr.Length == 4)
            {
                DateTime tmp;
                if (DateTime.TryParse(publishHouseAndTimeArr[3], out tmp))
                {
                    publishTime = publishHouseAndTimeArr[3];
                    publishHouse = publishHouseAndTimeArr[2];
                }
                else
                {
                    publishTime = publishHouseAndTimeArr[2];
                    publishHouse = publishHouseAndTimeArr[1];
                }
            }
            else if (publishHouseAndTimeArr.Length == 5)
            {
                publishTime = publishHouseAndTimeArr[3];
                publishHouse = publishHouseAndTimeArr[2];
            }
            DateTime tmpTime;
            if (!DateTime.TryParse(publishTime, out tmpTime))
            {
                foreach (var x in publishHouseAndTimeArr)
                {
                    if (DateTime.TryParse(x, out tmpTime))
                    {
                        publishTime = x;
                        break;
                    }
                }
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

            var desc = item.SelectSingleNode("div[2]/p")?.InnerText;

            DateTime d;
            return new DoubanBook
            {
                Title = title,
                Link = link,
                Score = score,
                ScoreNumber = scoreNumber,
                PublishDate = string.IsNullOrEmpty(publishTime) ? DateTime.MinValue: DateTime.TryParse(publishTime, out d) ? d : DateTime.MinValue,
                PublishHouse = publishHouse,
                Desc = desc
            };
        
        }
    }

}
