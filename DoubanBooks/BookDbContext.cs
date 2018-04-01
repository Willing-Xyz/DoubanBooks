using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoubanBooks
{
    [DbConfigurationType(typeof(MySql.Data.Entity.MySqlEFConfiguration))]
    public class BookDbContext : DbContext
    {
        public BookDbContext(string nameOrConnectionString) : base(nameOrConnectionString) { }

        public DbSet<BookEntity> Books { get; set; }
    }
}
