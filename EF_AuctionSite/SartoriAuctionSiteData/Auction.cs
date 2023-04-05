using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;
using static System.Collections.Specialized.BitVector32;
# nullable enable

namespace Sartori
{
    public class Auction : IAuction
    { 
        public int Id { get; set; }
        public string Description { get; set; }
        public DateTime EndsOn { get; set; }
        public double Price { get; set; }
        public double HighestBid { get; set; }

        private readonly string ConnectionString;
        private readonly IAlarmClock AlarmClock;

        //Navigation Properties
        public int SellerId { get; set; }
        public User? Winner { get; set; }
        public int? WinnerId { get; set; }
        public Site? Site { get; set; }
        public int SiteId { get; }

        public IUser Seller { get; set; }

        private Auction() { }

        public Auction(int id, int sellingUserId, IUser seller, int siteId, string description, DateTime endsOn, double price, string connectionString, IAlarmClock alarmClock)
        {
            Id = id;
            SellerId = sellingUserId;
            Seller = seller;
            SiteId = siteId;
            Description = description;
            EndsOn = endsOn;
            ConnectionString = connectionString;
            Price = price;
            AlarmClock = alarmClock;
        }

        //Returns the user who made the highest bid
        public IUser? CurrentWinner()
        {
            CheckIfAuctionIsDeleted();
            using (var c = new DBContext(ConnectionString))
            {
                var user = c.Auctions.Include(a => a.Winner).FirstOrDefault(a => a.Id == Id)?.Winner;
                return user != null ? new User(SiteId, user.Username, user.Password, ConnectionString,
                    AlarmClock) : null;
            }
        }

        //Returns current price based on bid values
        public double CurrentPrice()
        {
            CheckIfAuctionIsDeleted();
            using (var c = new DBContext(ConnectionString))
            {
                var currentPrice = c.Auctions.FirstOrDefault(a => a.Id == Id)?.Price;
                return currentPrice ?? 0;
            }
        }

        //Deletes the auctions and its resources
        public void Delete()
        {
            CheckIfAuctionIsDeleted();
            using (var c = new DBContext(ConnectionString))
            {
                var auction = c.Auctions.FirstOrDefault(a => a.Id == Id);
                if (auction != null)
                {
                    c.Remove(auction);
                    c.SaveChanges();
                }
            }
        }

        //Makes a bid for this auction
        public bool Bid(ISession session, double offer)
        {
            CheckIfAuctionIsDeleted();
            if (offer < 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(offer), offer, "Offer cannot be negative");
            var userSession = session as Session;
            Utilities.CheckNotNull(session, userSession);
            using (var c = new DBContext(ConnectionString))
            {
                var checkSession = c.Sessions.FirstOrDefault(s => s.Id == session.Id);
                if (checkSession == null) throw new AuctionSiteArgumentException("The session is not valid", nameof(session));
            }
            if (session.ValidUntil < AlarmClock.Now)
                throw new AuctionSiteArgumentException("The session is expired", nameof(session));
            if (EndsOn < AlarmClock.Now) return false;

            using (var c = new DBContext(ConnectionString))
            {
                var user = c.Users.First(u => u.Username == ((User)session.User).Username);
                if (user == null) throw new AuctionSiteInvalidOperationException("This user has been deleted");

                var auction = c.Auctions.First(a => a.Id == Id);
                var site = c.Sites.FirstOrDefault(s => s.SiteId == SiteId);

                ((Session)session).ValidUntil = AlarmClock.Now.AddSeconds(site!.SessionExpirationInSeconds);

                if (auction.WinnerId != null && auction.WinnerId != user.UserId &&
                    offer < CurrentPrice() + site.MinimumBidIncrement)
                    return false;


                if (auction.WinnerId == null)
                {
                    if (offer < CurrentPrice()) return false;
                    auction.HighestBid = offer;
                    auction.WinnerId = user.UserId;
                }
                else if (auction.WinnerId == user.UserId)
                {
                    if (offer < auction.HighestBid + site.MinimumBidIncrement) return false;
                    auction.HighestBid = offer;
                }
                else
                {
                    if (offer < CurrentPrice() + site.MinimumBidIncrement) return false;
                    if (auction.HighestBid < offer)
                    {
                        auction.Price = Math.Min(auction.HighestBid + site.MinimumBidIncrement, offer);
                        auction.HighestBid = offer;
                        auction.WinnerId = user.UserId;
                    }
                    else
                    {
                        auction.Price = Math.Min(auction.HighestBid, offer + site.MinimumBidIncrement);
                    }
                }
                c.SaveChanges();
            }
            return true;
        }

        //Checks if an auction has already been deleted
        private void CheckIfAuctionIsDeleted()
        {
            using (var c = new DBContext(ConnectionString))
            {
                try
                {
                    c.Auctions!.First(u => u.Id == Id);
                }
                catch (InvalidOperationException e)
                {
                    throw new AuctionSiteInvalidOperationException("This auction has already been deleted", e);
                }
            }
        }

        public override bool Equals(object? obj)
        {
            var item = obj as Auction;

            if (item == null)
                return false;
            return SiteId == item.SiteId && Id == item.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }

}
