using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Sartori
{
    public class Session : ISession
    {
        public string Id { get; set; }
        public DateTime ValidUntil { get; set; }

        private readonly string ConnectionString;
        private IAlarmClock AlarmClock;

        //Navigation Properties
        public IUser User { get; set; }
        public int UserId { get; set; }
        public Site? Site { get; set; }
        public int SiteId { get; set; }

        private Session() { }

        public Session(int siteId, int userId, User user, DateTime validUntil, string connectionString, IAlarmClock alarmClock)
        {
            Id = userId.ToString();
            ConnectionString = connectionString;
            SiteId = siteId;
            UserId = user.UserId;
            ValidUntil = validUntil;
            Id = userId.ToString();
            User = user;
            AlarmClock = alarmClock;
        }

        //Deletes the session and its resources
        public void Logout()
        {
            using (var c = new DBContext(ConnectionString))
            {
                var session = c.Sessions.FirstOrDefault(s => s.Id == Id);
                if (session == null) throw new AuctionSiteInvalidOperationException("This session is expired or deleted");
                    c.Remove(session);
                    c.SaveChanges();
            }
        }

        //Creates an auction for given object/service
        public IAuction CreateAuction(string description, DateTime endsOn, double startingPrice)
        {
            if (CheckIfSessionIsDeleted())
                throw new AuctionSiteInvalidOperationException("This session has been deleted");
            if (AlarmClock.Now > ValidUntil) throw new AuctionSiteInvalidOperationException("This session is expired");
            Utilities.CheckNotNull(description);
            if (description == "")
                throw new AuctionSiteArgumentException("Description cannot be empty", nameof(description));
            if (endsOn < AlarmClock.Now)
                throw new AuctionSiteUnavailableTimeMachineException("End of auction cannot precede the current time");
            if (startingPrice < 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(startingPrice), "Starting price cannot be negative");

            using (var c = new DBContext(ConnectionString))
            {
                var user = c.Users.FirstOrDefault(u => u.SiteId == SiteId && u.Username == User.Username);
                if (user is null) throw new AuctionSiteInvalidOperationException("There is no user with this credentials");

                var auction = new Auction(0, user.UserId, user, SiteId, description, endsOn, startingPrice, ConnectionString, AlarmClock);
                c.Auctions.Add(auction);

                var site = c.Sites.FirstOrDefault(s => s.SiteId == SiteId);
                var session = c.Sessions.FirstOrDefault(s => s.Id == Id);
                if (session == null) throw new AuctionSiteArgumentException("The session is not valid");
                ValidUntil = AlarmClock.Now.AddSeconds(site!.SessionExpirationInSeconds);

                c.SaveChanges();
                return auction;
            }

        }

        //Checks if a session has already been deleted
        private bool CheckIfSessionIsDeleted()
        {
            using (var c = new DBContext(ConnectionString))
            {
                var session = c.Sessions.FirstOrDefault(s => s.Id == Id);
                if (session == null) return true;
                return false;
            }
        }

        public override bool Equals(object? obj)
        {
            var item = obj as Session;
            if (item == null)
                return false;
            return Id == item.Id;
        }
        public override int GetHashCode() { 
            return Id.GetHashCode(); 
        }
    }
}