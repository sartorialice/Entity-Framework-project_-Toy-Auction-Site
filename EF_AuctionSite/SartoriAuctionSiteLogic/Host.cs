using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Sartori
{
    internal class Host : IHost
    {
        private readonly string ConnectionString;
        private readonly IAlarmClockFactory AlarmClockFactory;

        public Host(string connectionString, IAlarmClockFactory alarmClockFactory)
        {
            ConnectionString = connectionString;
            AlarmClockFactory = alarmClockFactory;
        }

        //Returns information for all sites 
        public IEnumerable<(string Name, int TimeZone)> GetSiteInfos()
        {
            List<Site> sites;
            using (var c = new DBContext(ConnectionString))
            {
                try
                {
                    sites  = c.Sites.ToList();
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Malformed connection string or the DB server unavailable", e);
                }
            }
            foreach (var site in sites)
            {
                yield return (site.Name, site.Timezone);
            }
        }

        //Creates a site
        public void CreateSite(string name, int timezone, int sessionExpirationTimeInSeconds,
            double minimumBidIncrement)
        {
            Utilities.CheckNotNull(name);
            Utilities.StringInsideRange(name, DomainConstraints.MinSiteName, DomainConstraints.MaxSiteName);
            if (timezone < DomainConstraints.MinTimeZone || timezone > DomainConstraints.MaxTimeZone)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(timezone), timezone,
                    "Timezone not valid: it must be between -12 and 12");
            if (sessionExpirationTimeInSeconds <= 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(sessionExpirationTimeInSeconds),
                    sessionExpirationTimeInSeconds, "Expiration time cannot be negative");
            if (minimumBidIncrement <= 0)
                throw new AuctionSiteArgumentOutOfRangeException(nameof(minimumBidIncrement),
                    minimumBidIncrement, "Minimum bid increment cannot be negative");

            var site = new Site(name, timezone, sessionExpirationTimeInSeconds, minimumBidIncrement,
                AlarmClockFactory.InstantiateAlarmClock(timezone), ConnectionString);

            using (var c = new DBContext(ConnectionString))
            {
                c.Sites.Add(site);
                try
                {
                    c.SaveChanges();
                }
                catch (AuctionSiteNameAlreadyInUseException e)
                {
                    throw new AuctionSiteNameAlreadyInUseException(name, "Name of the site already in use", e);
                }
            }
        }

        //Loads the site
        public ISite LoadSite(string name)
        {
            Utilities.CheckNotNull(name);
            Utilities.StringInsideRange(name, DomainConstraints.MinSiteName, DomainConstraints.MaxSiteName);
            using (var c = new DBContext(ConnectionString))
            {
                try
                {
                    var site = c.Sites.FirstOrDefault(s => s.Name == name);
                    if (site != null)
                    return new Site(site.Name, site.Timezone, site.SessionExpirationInSeconds, site.MinimumBidIncrement,
                              AlarmClockFactory.InstantiateAlarmClock(site.Timezone), ConnectionString) { SiteId = site.SiteId };
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Malformed connection string or the DB server unavailable", e);
                }
            }
            throw new AuctionSiteInexistentNameException(name, "No site with given name");
        }
    }
}

    



