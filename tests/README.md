Running Tests
=============

The test projects in this directory should be run using the Visual Studio Unit
Testing framework (MSTest).

The `.vsmdi` file contains metadata for all tests in the solution.

IrcDotNet
---------

All tests in this project presume the availability of a fully-functional and
compliant IRC server. The server to which the domain `irc.freenode.net` resolves
is used by default.

### Important

Do not run any individual tests in this project, as most will fail because they
are not begun in the required state. You should only run the `CompleteIrcClient`
ordered test, which is designed to execute the unit tests in a fixed order with
pre-defined conditions (expected states) and timeouts.

### Note

When running the ordered test in Debug mode, asynchronous tests never time out.

### Note

In rare situations, the `CompleteIrcClient` test may fail despite the library
operating correctly in every way. This could be due to a number of reasons, most
likely that an element of the server is not functioning properly, or extreme lag
on the client or server end (though the timeouts used should be sufficient to
accommodate for such delays).

If after verifying that the IRC server/network used for the tests is operating
correctly and without excessive lag, and a particular unit test has failed on
several occasions, then the issue should be reported as a bug.
