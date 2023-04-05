using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AuctionSite.Interface;
using static System.Collections.Specialized.BitVector32;


namespace Sartori
{
    public class DBContext : TapDbContext
    {
        // Constructor that takes a connection string and calls base constructor
        public DBContext(string connectionString) : base(new DbContextOptionsBuilder<DBContext>().UseSqlServer(connectionString).Options) { }

        //DB Entities
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<Site> Sites { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var auction = modelBuilder.Entity<Auction>();
            auction.HasOne(a => a.Site)
                .WithMany(site => site.Auctions)
                .HasForeignKey(a => a.SiteId)
                .OnDelete(DeleteBehavior.NoAction);
            auction.HasOne(a => a.Seller as User);
            auction.HasOne(a => a.Winner);

            var session = modelBuilder.Entity<Session>();
            session.HasOne(s => s.Site)
                .WithMany(site => site.Sessions)
                .HasForeignKey(s => s.SiteId)
                .OnDelete(DeleteBehavior.NoAction);

            var user = modelBuilder.Entity<User>();
            user.HasOne(u => u.Session)
                .WithOne(s => s.User as User)
                .HasForeignKey<Session>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            user.HasMany(u => u.Selling)
                .WithOne(a => a.Seller as User)
                .HasForeignKey(a => a.SellerId)
                .OnDelete(DeleteBehavior.NoAction);
           
        }

        public override int SaveChanges()
        {
            try
            {
                return base.SaveChanges();
            }
            catch (SqlException e)
            {
                throw new AuctionSiteUnavailableDbException("Unavailable DB", e);
            }
            catch (DbUpdateException e)
            {
                var sqlException = e.InnerException as SqlException;
                if (sqlException == null) throw new AuctionSiteUnavailableDbException("Missing information from DB", e);
                if (e is DbUpdateConcurrencyException)
                    throw new AuctionSiteConcurrentChangeException("Failed to save an entity which has been concurrently modified", e);
               switch (sqlException.Number)
                {
                    case < 54: throw new AuctionSiteUnavailableDbException("Not available DB", e);
                    case 2601: throw new AuctionSiteNameAlreadyInUseException("Sql error:2601");
                    case 2627: throw new AuctionSiteNameAlreadyInUseException(null, "Primary key already in use", e);
                    case 547: throw new AuctionSiteInvalidOperationException("Foreign key not found", e);
                    default:
                        throw new AuctionSiteUnavailableDbException("Missing information form DB exception", e);
                }
            }
        }
        
    }
}