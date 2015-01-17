# IRC.NET

*latest* documentation available on https://alexreg.github.io/IrcDotNet/.

## Build status

Devel Branch

[![Build Status](https://travis-ci.org/alexreg/IrcDotNet.svg?branch=develop)](https://travis-ci.org/alexreg/IrcDotNet)
[![Build status](https://ci.appveyor.com/api/projects/status/1kgumufg5xre9doo/branch/develop?svg=true)](https://ci.appveyor.com/project/alexreg/IrcDotNet/branch/develop)

Master Branch

[![Build Status](https://travis-ci.org/alexreg/IrcDotNet.svg?branch=master)](https://travis-ci.org/alexreg/IrcDotNet)
[![Build status](https://ci.appveyor.com/api/projects/status/1kgumufg5xre9doo/branch/master?svg=true)](https://ci.appveyor.com/project/alexreg/IrcDotNet/branch/master)

## Overview

IRC.NET is a complete IRC (Internet Relay Chat) client library for the
.NET Framework 4.0 and Silverlight 4.0. It aims to provide a complete and
efficient implementation of the protocol as described in RFCs 1459 and 2812,
as well as de-facto modern features of the protocol.

This project was formlery hosted on [Launchpad](https://launchpad.net/ircdotnet).

### Mono

The .NET Framework version of the library is also intended to compile and run
under Mono 2.6 and later.

The Silverlight version of the library is not guaranteed to compile or run on
the Moonlight framework, though it may be officially supported in the future.

### Non-RFC Features

* Parsing of [ISUPPORT parameters](http://www.irc.org/tech_docs/draft-brocklesby-irc-isupport-03.txt). Interpretation of parameters is left to the user.
 
* [CTCP (Client-To-Client Protocol)](http://www.irchelp.org/irchelp/rfc/ctcpspec.html) support. Most common commands are supported.

## Help

* Talk to us on our [IRC channel](irc://freenode.net/##irc.net).

* If you have confirmed that the behaviour is unexpected, submit an [issue](https://github.com/alexreg/ircdotnet/issues) on GitHub.
