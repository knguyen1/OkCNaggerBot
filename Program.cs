using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace OkCNaggerBot
{
    class Program
    {
        private const string CLIENT_ID = "AnBipXYHpG0wog";
        private const string CLIENT_SECRET = "p2GQkwOtdkb31wu1h6bTg1hR4ko";
        private const string REFRESH_TOKEN = "dVmrzasWWkAMb8IYVqTWZZXN1-4";
        //private const string NAG_MESSAGE = "Hi.+It+looks+like+you+have+your+profile+set+as+private.++You'll+get+more+critiques+if+you+%5Bopen+it+up+in+your+settings%5D(http%3A%2F%2Fi.imgur.com%2FFOMq4AP.png).%0A%0A%0A%5E%5EI'm+%5E%5Ea+%5E%5EJSON+%5E%5Ebot+%5E%5Ewritten+%5E%5Ein+%5E%5EC%23+%5E%5E.NET.++%5B**%5E%5ESee+%5E%5Emy+%5E%5Eauthor+%5E%5Ehere.**%5D(https%3A%2F%2Fwww.google.com%2F%23q%3Dc%2523%2B.net%2Bdevelopers%2Bnew%2Byork%2Bcity)";
        //private static string TOKEN = "QhXGo0c-u3UeqQql832dVMyp03o";
        private const string REDIRECT_URI = @"http://www.reddit.com/r/okcupid";
        private const string ABOUT_URL = @"http://www.reddit.com/r/okcupid";
        private const string OAUTH_BASE_URI = @"https://ssl.reddit.com/api/v1/authorize?";
        private const string TOKEN_BASE_URI = @"https://ssl.reddit.com/api/v1/access_token";
        private const string OAUTH_RESOURCES_BASE = @"https://oauth.reddit.com";
        private const string LOG_PATH = @"G:\Prisma API\okcnb\logged_thingies.txt";
        //private const string LOG_PATH = @"C:\Users\knguyen\Documents\Visual Studio 2013\Projects\OkCNaggerBot\OkCNaggerBot\bin\Release\logged_thingies.txt";

        static void Main(string[] args)
        {
            //get the random seed, will be used later
            //Random rand = new Random((int)DateTime.Now.Ticks);

            //stop execution at current hour and 58 minutes... restart using sql server agent
            DateTime currentHour = Convert.ToDateTime(string.Format("{0}:00", DateTime.Now.Hour));
            DateTime nextHour = currentHour.AddHours(1).AddMinutes(-5);

            //read from a list of thingies already processed
            var loggedThingsArray = File.ReadAllLines(LOG_PATH);
            List<string> loggedThingsList = new List<string>(loggedThingsArray);

            //get a running list of thingIds
            List<string> thingIds = new List<string>();

            string token = "Q-y1LQu-XbJCJ4oH0BjvH09fOnk";

            while (currentHour < nextHour)
            {
                if (!CheckAuth(token))
                    token = GetToken("lazy", "lazy", true);

                var request = (HttpWebRequest)WebRequest.Create(string.Format(@"{0}/r/OkCupid/new.json?sort=new", OAUTH_RESOURCES_BASE));
                request.UserAgent = "okc_nagger_bot v1";
                request.Method = "GET";
                request.Headers[HttpRequestHeader.Authorization] = "bearer " + token;
                //request.ContentType = "application/json";

                using (var response = Retry.Do(() => request.GetResponse(), TimeSpan.FromSeconds(15)))
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream))
                {
                    //parse new posts
                    JObject responseJson = JObject.Parse(streamReader.ReadToEnd());

                    //get the posts array
                    JArray posts = responseJson["data"]["children"] as JArray;

                    if (posts == null)
                    {
                        posts = new JArray
                    {
                        responseJson["data"]["children"]
                    };
                    }

                    //limit the number of calls per sec
                    using (var rateGate = new RateGate(1, TimeSpan.FromMilliseconds(700)))
                    {
                        //parse posts individually
                        foreach (JToken post in posts)
                        {
                            //wait to proceed
                            rateGate.WaitToProceed();

                            JToken postContainer = post["data"];
                            string thingId = postContainer["name"].ToString();

                            Console.WriteLine("Processing post...");

                            //must not have been processed
                            if (!thingIds.Contains(thingId) && !loggedThingsList.Contains(thingId))
                            {
                                //only parse it if it's a post
                                //see https://www.reddit.com/dev/api for more info
                                if (post["kind"].ToString() == "t3")
                                {
                                    //is it a critique request?
                                    if (CheckIsCritiqueRequest(postContainer["selftext"].ToString(), postContainer["title"].ToString()))
                                    {
                                        string uri = postContainer["url"].ToString();
                                        bool selfPost = bool.Parse(postContainer["is_self"].ToString());

                                        if (selfPost)
                                        {
                                            string seftText = postContainer["selftext"].ToString();

                                            //use regex to search for the url
                                            string re1 = ".*?";	// Non-greedy match on filler
                                            string re2 = "((?:http|https)(?::\\/{2}[\\w]+)(?:[\\/|\\.]?)(?:[^\\s\"]*))";

                                            //match the regex
                                            Regex r = new Regex(re1 + re2, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                                            Match m = r.Match(seftText);

                                            //pull the url out
                                            if (m.Success)
                                            {
                                                string okCupidUrl = m.Groups[1].ToString();

                                                //got the url... go noag
                                                VisitAndNag(okCupidUrl, thingId, token);
                                            }
                                        }
                                        else
                                        {
                                            //does it contain a link to the profile?
                                            if (uri.ContainsIgnoreCase("okcupid"))
                                                VisitAndNag(uri, thingId, token);
                                        }
                                    }
                                }

                                //add thing id to running list
                                thingIds.Add(thingId);
                            }
                        }
                    }
                }

                //reset the current hour
                currentHour = DateTime.Now;

                Console.WriteLine("Sleeping...");

                //sleep for x minutes
                if (currentHour.Hour > 1 && currentHour.Hour < 9)
                    Thread.Sleep(TimeSpan.FromMinutes(10));
                else
                    Thread.Sleep(TimeSpan.FromMinutes(3));
            }

            //write thing ids to text file and exit
            File.WriteAllLines(LOG_PATH, thingIds);
        }

        public static void VisitAndNag(string uri, string thingId, string token)
        {
            //is it a link to a profile?
            if (uri.Length > 31)
            {
                bool canVisit = VisitOkCupid(uri);

                if (!canVisit)
                {
                    string nagPrivateProfile = "Hi.+It+looks+like+you+have+your+profile+set+as+private.++You'll+get+more+critiques+if+you+%5Bopen+it+up+in+your+settings%5D(http%3A%2F%2Fi.imgur.com%2FFOMq4AP.png).%0A%0A%0A%5E%5EI'm+%5E%5Ea+%5E%5EJSON+%5E%5Ebot+%5E%5Ewritten+%5E%5Ein+%5E%5EC%23+%5E%5E.NET.++%5B**%5E%5ESee+%5E%5Emy+%5E%5Eauthor+%5E%5Ehere.**%5D(https%3A%2F%2Fwww.google.com%2F%23q%3Dc%2523%2B.net%2Bdevelopers%2Bnew%2Byork%2Bcity)";

                    //nag them
                    PostNag(token, thingId, nagPrivateProfile);
                }
            }
            else
            {
                string nagMessageNoLink = "Hi.+You+didn't+actually+link+your+profile.%0A%0A%0A%5E%5EI'm+%5E%5Ea+%5E%5EJSON+%5E%5Ebot+%5E%5Ewritten+%5E%5Ein+%5E%5EC%23+%5E%5E.NET.++%5B**%5E%5ESee+%5E%5Emy+%5E%5Eauthor+%5E%5Ehere.**%5D(https%3A%2F%2Fwww.google.com%2F%23q%3Dc%2523%2B.net%2Bdevelopers%2Bnew%2Byork%2Bcity)";

                //nag them
                PostNag(token, thingId, nagMessageNoLink);
            }
        }

        public static void PostNag(string token, string thingId, string nagMessage)
        {
            //post submit string
            string postContent = string.Format("thing_id={0}&text={1}", thingId, nagMessage);

            var request = (HttpWebRequest)WebRequest.Create(string.Format(@"{0}/api/comment", OAUTH_RESOURCES_BASE));
            request.Headers[HttpRequestHeader.Authorization] = "bearer " + token;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "okc_nagger_bot v1";
            request.ContentLength = postContent.Length;

            //write to server
            using (var sWriter = new StreamWriter(request.GetRequestStream()))
            {
                sWriter.Write(postContent);
            }
        }

        public static bool VisitOkCupid(string uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            //fake being chrome and not attach any cookie (you're not logged in)
            request.Method = "GET";
            request.UserAgent = "Mozilla/5.0 (compatible; MSIE 9.0; Windows Phone OS 7.5; Trident/5.0; IEMobile/9.0)";

            try
            {
                using (var response = request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream))
                {
                    string html = streamReader.ReadToEnd();
                }
            }
            catch(WebException exc)
            {
                if(exc.Status == WebExceptionStatus.ProtocolError)
                {
                    using (var errorResp = exc.Response as HttpWebResponse)
                    {
                        if (errorResp != null)
                        {
                            //get the error response stream
                            using(var errorRespStream = errorResp.GetResponseStream())
                            using(var errorReader = new StreamReader(errorRespStream))
                            {
                                //read its html
                                string errorHtml = errorReader.ReadToEnd();

                                //if it contains this string, it means the profile is private
                                if (errorHtml.ContainsIgnoreCase(@"profile,mustlogin,redirect,meta_refresh"))
                                    return false;
                                else
                                    return true;
                            }
                        }
                    }
                }
            }

            return true;
        }

        public static bool CheckIsCritiqueRequest(string selfText, string title)
        {
            if (selfText.ContainsIgnoreCase("critique") || title.ContainsIgnoreCase("critique") || title.ContainsIgnoreCase("feedback"))
                return true;
            else
                return false;
        }

        public static bool CheckAuth(string token)
        {
            string response = ReturnWebResponse(string.Format(@"{0}/api/v1/me", OAUTH_RESOURCES_BASE), token);

            if (response != null)
                return true;
            else
                return false;
        }

        public static string GetToken(string code, string redirectUri, bool isRefresh = false)
        {
            //var grantString = string.Format("grant_type={0}&code={1}&redirect_uri={2}", System.Uri.EscapeDataString("authorization_code"), code, redirectUri);
            //var grantString = string.Format("grant_type={0}&code=CODE&redirect_uri={1}", code, redirectUri);
            
            string grantString = default(string);

            if (isRefresh == true)
                grantString = string.Format("grant_type=refresh_token&refresh_token={0}", REFRESH_TOKEN);
            else
                grantString = string.Format("grant_type={0}&code={1}&redirect_uri={2}", System.Uri.EscapeDataString("authorization_code"), code, redirectUri);
            
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(CLIENT_ID + ":" + CLIENT_SECRET));

            var request = (HttpWebRequest)WebRequest.Create(TOKEN_BASE_URI);
            request.Headers[HttpRequestHeader.Authorization] = "Basic " + credentials;
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "okc_nagger_bot v1";
            request.ContentLength = grantString.Length;

            //write to server
            using (var sWriter = new StreamWriter(request.GetRequestStream()))
            {
                sWriter.Write(grantString);
            }

            JToken responseJson = default(JObject);

            //get results
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var sReader = new StreamReader(response.GetResponseStream()))
            {
                responseJson = JObject.Parse(sReader.ReadToEnd());
            }

            return responseJson["access_token"].ToString();
        }

        public static string ReturnWebResponse(string uri, string token)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET";
            request.UserAgent = "okc_nagger_bot v1";
            request.Headers[HttpRequestHeader.Authorization] = "bearer " + token;
            request.AllowAutoRedirect = false;

            try
            {
                using (var response = request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var streamReader = new StreamReader(responseStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
            catch (WebException exc)
            {
                Console.WriteLine(exc.Message);

                return null;
            }
        }
    }
}
