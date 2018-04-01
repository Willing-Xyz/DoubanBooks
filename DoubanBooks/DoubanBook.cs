using System;
using System.Collections.Generic;

namespace DoubanBooks
{
    public class DoubanBook : IEqualityComparer<DoubanBook>, IEquatable<DoubanBook>
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public float Score { get; set; }
        //public float FiveScorePercentage { get; set; }
        //public float FourScorePercentage { get; set; }
        //public float ThreeScorePercentage { get; set; }
        //public float TwoScorePercentage { get; set; }
        //public float OneScorePercentage { get; set; }
        public int ScoreNumber { get; set; }
        public DateTime PublishDate { get; set; }
        public string PublishHouse { get; set; }
        public string Desc { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        public bool Equals(DoubanBook x, DoubanBook y)
        {
            return x.Link == y.Link && x.Title == y.Title;
        }

        public int GetHashCode(DoubanBook obj)
        {
            return this.Link.GetHashCode() & this.Title.GetHashCode();
        }

        public bool Equals(DoubanBook other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Title, other.Title) && string.Equals(Link, other.Link);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((DoubanBook) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Title != null ? Title.GetHashCode() : 0) * 397) ^ (Link != null ? Link.GetHashCode() : 0);
            }
        }
    }

}
