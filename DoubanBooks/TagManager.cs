using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoubanBooks
{

    public class TagManager
    {
        private static string COMFPLETE_TAGS_DIR = "Tags";
        private static string COMPLETED_TAGS_PATH = $"{COMFPLETE_TAGS_DIR}\\CompletedTags.json";
        private Dictionary<string, DateTime> _unProcessTags = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> _completeTags = new Dictionary<string, DateTime>();

        private object _sync = new object();

        public void Init()
        {
            lock (_sync)
            {
                if (File.Exists(COMPLETED_TAGS_PATH))
                {
                    var completedTagsStr = File.ReadAllText(COMPLETED_TAGS_PATH);
                    var completedTags = JsonConvert.DeserializeObject<List<Tag>>(completedTagsStr);
                    completedTags.ForEach(item => _completeTags.Add(item.TagName, item.CompletedTime));
                }

                var tagsStr = File.ReadAllText("Tags.json");
                var tags = JsonConvert.DeserializeObject<List<string>>(tagsStr);
                tags.ForEach(item =>
                {
                    this.AddUnProcessTag(item);
                });
            }
        }

        public void AddUnProcessTag(string tag)
        {
            lock (_sync)
            {
                if (_completeTags.ContainsKey(tag))
                {
                    _unProcessTags[tag] = _completeTags[tag];
                }
                else
                {
                    _unProcessTags[tag] = DateTime.MinValue;
                }
            }

        }

        public void AddCompleteTag(string tag, DateTime dateTime)
        {
            lock (_sync)
            {
                if (_unProcessTags.ContainsKey(tag))
                    _unProcessTags.Remove(tag);
                if (!_completeTags.ContainsKey(tag))
                {
                    _completeTags.Add(tag, dateTime);
                    var tags = new List<Tag>();
                    foreach (var pair in _completeTags)
                    {
                        tags.Add(new Tag
                        {
                            TagName = pair.Key,
                            CompletedTime = pair.Value
                        });
                    }
                    if (!Directory.Exists(COMFPLETE_TAGS_DIR))
                        Directory.CreateDirectory(COMFPLETE_TAGS_DIR);
                    File.WriteAllText(COMPLETED_TAGS_PATH, JsonConvert.SerializeObject(tags));
                }
            }
        }

        public Tag GetUnProcessTag()
        {
            lock (_sync)
            {
                if (_unProcessTags.Count == 0)
                    return null;
                var item = _unProcessTags.First();
                return new Tag
                {
                    CompletedTime = item.Value,
                    TagName = item.Key
                };
            }
        }

        public class Tag
        {
            public string TagName { get; set; }
            public DateTime CompletedTime { get; set; }
        }
    }
}
