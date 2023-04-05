using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TAP22_23.AuctionSite.Interface;


namespace Sartori
{
    public static class Utilities
    {
        //Creates the hash for a password
        public static string CreateHash(string input)
        {
            using var sha256 = SHA256.Create();
            var byteArray = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(byteArray).ToLower();

        }

        //Checks if two passwords correspond
        public static bool CheckPassword(string check, string password)
        {
            if (check == Utilities.CreateHash(password)) return true;
            return false;
        }

        //Check if parameters are null
        public static void CheckNotNull(params object[] list)
        {
            foreach (var o in list)
                if (o == null)
                    throw new AuctionSiteArgumentNullException($"Parameter {nameof(o)} cannot be null");
        }

        //Checks if a given string is in a range
        public static void StringInsideRange(string s, int lengthMin = 0, int lengthMax = int.MaxValue)
        {
            if (s.Length < lengthMin || s.Length > lengthMax)
                throw new AuctionSiteArgumentException(nameof(s), $"String length out of range: min is {lengthMin}, max is {lengthMax}");
        }
    }
}