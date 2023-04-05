using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using static System.Collections.Specialized.BitVector32;

namespace Sartori
{
    [Index(nameof(SiteId), nameof(Username), IsUnique = true, Name = "UsernameUnique")]
    public class User : IUser
    {
        public int UserId { get; set; }
        [MinLength(DomainConstraints.MinUserName)]
        [MaxLength(DomainConstraints.MaxUserName)]
        public string Username { get; }
        public string Password { get; set; }

        //Navigation Properties
        public Site? Site { get; set; }
        public int SiteId { get; }
        public Session? Session { get; set; }
        public string? SessionId { get; set; }
        public List<Auction> Bidding { get; set; }
        public List<Auction> Selling { get; set; }

        private readonly string ConnectionString;
        private readonly IAlarmClock AlarmClock;

        private User() { }
        public User(int siteId, string username, string password, string connectionString, IAlarmClock alarmClock)
        {
            Username = username;
            Password = password;
            SiteId = siteId;
            ConnectionString = connectionString;
            AlarmClock = alarmClock;

            Bidding = new List<Auction>();
            Selling = new List<Auction>();

        }
        //Returns all winningAuctions won by the user
        public IEnumerable<IAuction> WonAuctions()
        {
            CheckIfUserIsDeleted();
            return WonAuctionsAux();

            IEnumerable<IAuction> WonAuctionsAux()
            {
                using (var c = new DBContext(ConnectionString))
                {
                    var user = c.Users.Include(u => u.Bidding).FirstOrDefault(u => u.Username == Username);
                    c.Auctions.Include(a => a.Seller);
                    var auctions = user.Bidding.Where(a => a.EndsOn <= AlarmClock.Now).ToList();

                    foreach (var auction in auctions)
                        yield return new Auction(auction.Id, auction.SellerId, auction.Seller, SiteId, auction.Description,
                            auction.EndsOn, auction.Price, ConnectionString, AlarmClock)
                        { HighestBid = auction.HighestBid };
                }
            }
        }

        //Deletes a user and its resources
        public void Delete()
        {
            CheckIfUserIsDeleted();
            using (var c = new DBContext(ConnectionString))
            {
                var winningAuctions = c.Auctions.Where(a => a.WinnerId == UserId).ToList();
                foreach (var auction in winningAuctions)
                {
                    if (auction.EndsOn >= AlarmClock.Now)
                        throw new AuctionSiteInvalidOperationException("This user is winning an auction, he cannot be deleted");
                }
                var user = c.Users.FirstOrDefault(u => u.Username == Username && u.SiteId == SiteId);
                var sellingAuctions = c.Auctions.Where(a => a.SellerId == UserId).ToList(); 

                foreach (var auction in sellingAuctions)
                {
                    auction.Delete();
                }

                c.Remove(user);
                c.SaveChanges();
            }
        }

        //Checks if a user has already been deleted
        private void CheckIfUserIsDeleted()
        {
            using (var c = new DBContext(ConnectionString))
            {
                var user = c.Users.FirstOrDefault(u => u.SiteId == SiteId && u.Username == Username);
                if (user == null) throw new AuctionSiteInvalidOperationException("This user is already deleted");
            }
        }

        public override bool Equals(object? obj)
        {
            var item = obj as User;
            if (item == null)
                return false;
            return SiteId == item.SiteId && Username == item.Username;
        }
        public override int GetHashCode()
        {
            return UserId.GetHashCode();
        }
    }
}