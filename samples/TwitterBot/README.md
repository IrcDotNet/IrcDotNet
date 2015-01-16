Overview
========

This sample interacts with the Twitter API to provide Twitter
<http://twitter.com/> services to users on an IRC network. The bot provides the
ability for multiple IRC users to log in each on a separate Twitter account and
perform tasks such as retrieving recent tweets and posting new tweets, via
channel and private messages.

Credits
=======

Interaction with the Twitter service is done using the *TweetSharp* library
<https://github.com/danielcrenna/tweetsharp>.

Command-Line Commands
=====================

Exit the program
----------------

Syntax:

  exit

Connect to a server
-------------------

Syntax:

  connect <server address>

Examples:

  connect irc.freenode.net
  connect 192.168.2.3

Disconnect from a server
------------------------

Syntax:

  disconnect <server name regex>

Examples:

  disconnect freenode

Join a channel
--------------

Syntax:

  join <server name regex> <channel name>

Examples:

  join freenode #TestChannel
  
Leave a channel
---------------

Syntax:

  leave <server name regex> <channel name>

Examples:

  leave freenode #TestChannel

Chat Commands
=============

These commands may be sent by a user directly to the bot user or to a channel of
which the bot is a member.

Note that all chat commands are prefixed with a period ('.').

List Users
----------

Asks the bot to list all currently logged-in Twitter users and the corresponding
IRC users.

Syntax:

  .lusers

Examples:

  .lusers

Log In
------

Asks the bot to log in to Twitter using the specified account details.
The logged-in Twitter user is associated with the IRC user that sent the
command.

Syntax:

  .login <account username> <account password>

Examples:

  .login MyUsername MyPassword
 
Log Out
-------

Asks the bot to log out the Twitter user associated with the IRC user that sent
the command.

Syntax:

  .logout

Examples:

  .logout
 
Send Tweet
----------

Asks the bot to post the given text as a Tweet as the logged-in Twitter user.

Syntax:

  .send /<text>

Examples:

  .send /This is a test Tweet message.
 
List Home Tweets
----------------

Asks the bot to list recent Tweets on the home timeline of the logged-in Twitter
user.

Syntax:

  .home

Examples:

  .home
 
List Tweets Mentioning Me
-------------------------

Asks the bot to list recent Tweets that mention the logged-in Twitter user.

Syntax:

  .mentions

Examples:

  .mentions
