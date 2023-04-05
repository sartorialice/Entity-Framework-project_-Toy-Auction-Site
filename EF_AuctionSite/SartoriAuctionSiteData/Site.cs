using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Sartori
{
    [Index(nameof(Name), IsUnique = true, Name = "NameUnique")]
    public class Site : ISite
    {
        public int SiteId { get; set; }
        [MinLength(DomainConstraints.MaxSiteName)]
        [MaxLength(DomainConstraints.MaxSiteName)]
        public string Name { get; }
        [Range(DomainConstraints.MinTimeZone, DomainConstraints.MaxTimeZone)]
        public int Timezone { get; set; }
        [Range(0, int.MaxValue)]
        public int SessionExpirationInSeconds { get; set; }
        [Range(0, double.MaxValue)]
        public double MinimumBidIncrement { get; set; }

        private readonly IAlarmClock AlarmClock;
        private readonly string ConnectionString;
        private IAlarm Alarm;

        //Navigation Properties
        public List<User> Users { get; set; }
        public List<Auction> Auctions { get; set; }
        public List<Session> Sessions { get; set; } 

        private Site() { }

        public Site(string name, int timeZone, int sessionExpirationInSeconds, double minimumBidIncrement, IAlarmClock alarmClock, string connectionString)
        {
            Name = name;
            Timezone = timeZone;
            SessionExpirationInSeconds = sessionExpirationInSeconds;
            MinimumBidIncrement = minimumBidIncrement;
            AlarmClock = alarmClock;
            ConnectionString = connectionString;
            Alarm = alarmClock.InstantiateAlarm(5 * 60 * 1000);
            Alarm.RingingEvent += DeleteExpiredSessions;
            DeleteExpiredSessions();

            Users = new();
            Auctions = new();
            Sessions = new(); 
        }

        //Returns all users of the site
        public IEnumerable<IUser> ToyGetUsers()
        {
            CheckIfSiteIsDeleted();
            return ToyGetUsersAux();

            IEnumerable<IUser> ToyGetUsersAux()
            {
                List<User>? users = new();
                using (var c = new DBContext(ConnectionString))
                {
                    users = c.Users.Where(u => u.SiteId == SiteId).ToList();
                }
                foreach (var user in users)
                    yield return new User(SiteId, user.Username, user.Password, ConnectionString, AlarmClock);
            }
        }

        //Returns all sessions of the site
        public IEnumerable<ISession> ToyGetSessions()
        {
            CheckIfSiteIsDeleted();
            return ToyGetSessionsAux();

            IEnumerable<ISession> ToyGetSessionsAux()
            {
                List<Session>? sessions = new();
                using (var c = new DBContext(ConnectionString))
                {
                    sessions = c.Sessions.Include(s => s.User).Where(s => s.SiteId == SiteId && s.ValidUntil > AlarmClock.Now).ToList();
                }
                foreach (var session in sessions)
                    yield return new Session(SiteId, session.UserId, (User)session.User, session.ValidUntil, ConnectionString, AlarmClock) { Id = session.Id };
            }
        }

        //Returns all active auctions of the site
        public IEnumerable<IAuction> ToyGetAuctions(bool onlyNotEnded)
        {
            CheckIfSiteIsDeleted();
            return ToyGetAuctionsAux();

            IEnumerable<IAuction> ToyGetAuctionsAux()
            {
                List<Auction> auctions = new();
                using (var c = new DBContext(ConnectionString))
                {
                    auctions = c.Auctions.Include(a => a.Seller)
                        .Where(a => a.SiteId == SiteId && (!onlyNotEnded || a.EndsOn > Now())).ToList();
                }
                foreach (var auction in auctions)
                    yield return new Auction(auction.Id, auction.SellerId, auction.Seller, SiteId, auction.Description, auction.EndsOn,
                                    auction.Price, ConnectionString, AlarmClock) { HighestBid = auction.HighestBid };
            }
        }

        //Returns the session for a user
        public ISession? Login(string username, string password)
        {
            CheckIfSiteIsDeleted();
            Utilities.CheckNotNull(username, password);
            Utilities.StringInsideRange(username, DomainConstraints.MinUserName, DomainConstraints.MaxUserName);
            Utilities.StringInsideRange(password, DomainConstraints.MinUserPassword);

            using (var c = new DBContext(ConnectionString))
            {
                var user = c.Users.Include(u => u.Session).FirstOrDefault(u => u.Username == username && u.SiteId == SiteId);
                if (user == null) return null;
                if (!Utilities.CheckPassword(user.Password, password)) return null;

                var session = user.Session;
                if (user.SessionId != null)
                {
                    session!.ValidUntil = AlarmClock.Now.AddSeconds(SessionExpirationInSeconds);
                    c.Update(session);
                    c.SaveChanges();
                    var ValidUntil = AlarmClock.Now.AddSeconds(SessionExpirationInSeconds);
                    return new Session(SiteId, user.UserId,user, ValidUntil, ConnectionString, AlarmClock) { Id = session.Id };
                }
                var newSession = new Session(SiteId, user.UserId, user, AlarmClock.Now.AddSeconds(SessionExpirationInSeconds), 
                    ConnectionString, AlarmClock) { Id = user.UserId.ToString() };
                user.SessionId = newSession.Id;
                c.Sessions.Add(newSession);
                c.SaveChanges();
                return newSession;
            }

        }

        //Creates a new user and adds it t the site
        public void CreateUser(string username, string password)
        {
            CheckIfSiteIsDeleted();
            Utilities.CheckNotNull(username, password);
            Utilities.StringInsideRange(username, DomainConstraints.MinUserName, DomainConstraints.MaxUserName);
            Utilities.StringInsideRange(password, DomainConstraints.MinUserPassword);

            using (var c = new DBContext(ConnectionString))
            {
                var user = c.Users.FirstOrDefault(u => u.Username == username && u.SiteId == SiteId);
                var newPassword = Utilities.CreateHash(password);
                var newUser = new User(SiteId, username, newPassword, ConnectionString, AlarmClock);
                c.Users.Add(newUser);
                try
                {
                    c.SaveChanges();
                }
                catch (AuctionSiteNameAlreadyInUseException e)
                {
                    throw new AuctionSiteNameAlreadyInUseException(username, "This username is already taken", e);
                }
            }
        }

        //Deletes the site and its resources
        public void Delete()
        {
            CheckIfSiteIsDeleted();
            using (var c = new DBContext(ConnectionString))
            {
                var site = c.Sites.SingleOrDefault(s => s.Name == Name);
                var users = ToyGetUsers();
                foreach (var user in users)
                {
                    user.Delete();
                }

                c.Remove(site);
                c.SaveChanges();
            }
        }

        //Returns current time
        public DateTime Now()
        {
            CheckIfSiteIsDeleted();
            return AlarmClock.Now;
        }

        //Checks if the site has already been deleted 
        private void CheckIfSiteIsDeleted()
        {
            using (var c = new DBContext(ConnectionString))
            {
                var site = c.Sites.SingleOrDefault(s => s.Name == Name);
                if (site == null) throw new AuctionSiteInvalidOperationException("This site is already deleted");
            }

        }

        //Deletes expired sessions
        private void DeleteExpiredSessions()
        {
            using (var c = new DBContext(ConnectionString))
            {
                var sessions = c.Sessions.ToList();
                var expired = sessions.Where(s => s.SiteId == SiteId && s.ValidUntil < Now()).ToList();
                c.RemoveRange(expired);
                c.SaveChanges();
            }
            Alarm = AlarmClock.InstantiateAlarm(5 * 60 * 1000);
        }
    }
}
