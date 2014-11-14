using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OkCNaggerBot
{
    public static class Helpers
    {
        public static string RandomString(int size, Random rand)
        {
            string seed = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";

            return new string(Enumerable.Repeat(seed, size)
                .Select(s => s[rand.Next(s.Length)])
                .ToArray()
                );
        }

        public static string GetAuthUrl(string oauthBaseUri, string clientId, string state, string redirectUri, string duration, string scope)
        {
            return string.Format("{0}client_id={1}&response_type=code&state={2}&redirect_uri={3}&duration={4}&scope={5}", oauthBaseUri, clientId, state, redirectUri, duration,scope);
        }
    }
}
