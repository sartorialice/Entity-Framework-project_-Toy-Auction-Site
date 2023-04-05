using System;
using Microsoft.Data.SqlClient;
using TAP22_23.AlarmClock.Interface;
using TAP22_23.AuctionSite.Interface;

namespace Sartori
{
    public class HostFactory : IHostFactory
    {
        //Creates an host
        public void CreateHost(string connectionString)
        {
            Utilities.CheckNotNull(connectionString);
            try
            {
                using (var c = new DBContext(connectionString))
                {
                    c.Database.EnsureDeleted();
                    c.Database.EnsureCreated();
                }
            }
            catch (SqlException e)
            {
                throw new AuctionSiteUnavailableDbException("Malformed connection string or the DB server unavailable", e);
            }
        }

        //Loads the host
        public IHost LoadHost(string connectionString, IAlarmClockFactory alarmClockFactory)
        {
            Utilities.CheckNotNull(connectionString, alarmClockFactory);
            using (var c = new DBContext(connectionString))
            {
                try
                {
                    if (!c.Database.CanConnect()) throw new AuctionSiteUnavailableDbException();
                    return new Host(connectionString, alarmClockFactory);
                }
                catch (SqlException e)
                {
                    throw new AuctionSiteUnavailableDbException("Malformed connection string or unavailable DB", e);
                }
            }
        }
    }
}
