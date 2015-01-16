using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using IrcDotNet;
using IrcDotNet.Samples.Common;
using TweetSharp;

namespace TwitterBot
{
    public class TwitterBotUser
    {
        private const int defaultReplyTweetCount = 5;

        private const string twitterConsumerKey = "SqaDmCpB8sTAMYlHS6X6g";
        private const string twitterConsumerSecret = "9jsbyyxziNiO5XP68ro4Gbsd0ZqLHa2EbtDmafzrI";

        private TwitterService service;

        public TwitterBotUser(IrcUser ircUser)
            : this()
        {
            Debug.Assert(ircUser != null);

            this.IrcUser = ircUser;
        }

        public TwitterBotUser()
        {
            this.service = new TwitterService(new TwitterClientInfo()
                {
                    ClientName = ProgramInfo.AssemblyTitle,
                    ClientVersion = ProgramInfo.AssemblyVersion.ToString(),
                    ConsumerKey = twitterConsumerKey,
                    ConsumerSecret = twitterConsumerSecret,
                });

            this.IsAuthenticated = false;
        }

        public bool IsAuthenticated
        {
            get;
            private set;
        }

        public TwitterUser TwitterUser
        {
            get;
            private set;
        }

        public IrcUser IrcUser
        {
            get;
            private set;
        }

        public TwitterStatus SendTweet(string text)
        {
            return this.service.SendTweet(text);
        }

        public IEnumerable<TwitterStatus> ListTweetsMentioningMe(int tweetCount = defaultReplyTweetCount)
        {
            return this.service.ListTweetsMentioningMe(tweetCount);
        }

        public IEnumerable<TwitterStatus> ListTweetsOnHomeTimeline(int tweetCount = defaultReplyTweetCount)
        {
            return this.service.ListTweetsOnHomeTimeline(tweetCount);
        }

        // Returns whether log-in succeeds.
        public bool LogIn(string username, string password)
        {
            try
            {
                // Log in to Twitter service using xAuth authentication.
                var token = this.service.GetAccessTokenWithXAuth(username, password);
                this.service.AuthenticateWith(token.Token, token.TokenSecret);
                var user = this.service.GetUserProfile();
                if (user.Name == null)
                    return false;
                
                this.TwitterUser = user;
                this.IsAuthenticated = true;

                return true;
            }
            catch (WebException exWeb)
            {
                var httpResponse = exWeb.Response as HttpWebResponse;
                if (httpResponse != null)
                {
                    if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
                        return false;
                }
                throw;
            }
        }

        public void LogOut()
        {
            // Log out of Twitter service.
            this.service.EndSession();
            this.TwitterUser = null;
            this.IsAuthenticated = false;
        }
    }
}
