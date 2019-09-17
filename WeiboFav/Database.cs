using Microsoft.EntityFrameworkCore;
using WeiboFav.Model;

namespace WeiboFav
{
    internal class Database : DbContext
    {
        public DbSet<WeiboInfo> WeiboInfo { get; set; }
        public DbSet<Img> Img { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=data.db");
        }
    }
}