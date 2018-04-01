using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DoubanBooks.Repositories
{
    public interface IBookRepository
    {
        Task AddBooksAsync();
    }
}
