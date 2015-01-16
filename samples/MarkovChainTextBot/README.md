Overview
========

This sample makes use of a well-known mathematical model called the Markov chain
<http://en.wikipedia.org/wiki/Markov_chain> to implement an IRC bot that
performs Markov text generation. The bot can, on request, generate pseudo-random
sentences with moderately high levels of grammatical correctness, based on
actual channel messages it has received over time. The more the bot has trained
using human messages sent over IRC, the better it typically performs at
generating meaningful sentences of its own.

The bot is capable of connecting to multiple servers and multiple channels on
each server, from which it constantly monitors messages sent in the channel to
build up the Markov chain and thus train the Markov text generator. Users can
command the bot to generate random sentences (based off training data, but
typically not identical to any), and write them to the channel.

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

Talk
----

Asks the bot to generate a random message, composed of one or more sentences,
and send it to the channel.

Syntax:

  .talk [ <number of sentences> [ <highlight nick name> ] ]

If <number of sentences> is not specified, then the number is chosen randomly
between 1 and 3.
If <highlight nick name> is specified, the message sent by the bot is prefixed
with the given nick name.

Examples:

  .talk
  .talk 5
  .talk 3 SomeNickName

Statistics
----------

Asks the bot to report various statistics about its usage to the channel.

Syntax:

  .stats

Examples:

  .stats
