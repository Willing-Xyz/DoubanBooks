using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoubanBooks
{
    public class BookEntity
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }
        public float Score { get; set; }
   
        public int ScoreNumber { get; set; }
        public DateTime PublishDate { get; set; }
        public string PublishHouse { get; set; }
        public string Desc { get; set; }
        public string Tags { get; set; }
    }
}
